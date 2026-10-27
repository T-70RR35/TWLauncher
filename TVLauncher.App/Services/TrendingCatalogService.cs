using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using TVLauncher.App.Models;

namespace TVLauncher.App.Services;

/// <summary>
/// Auto-descoberta dos títulos de APENAS streamings habilitados na aba Apps,
/// no Brasil, busca detalhes, trailers e URL Web de cada título. Uma chave Watchmode
/// configurada uma única vez é suficiente; TMDB é enriquecimento opcional.
/// </summary>
public sealed class TrendingCatalogService(
    IHttpClientFactory httpFactory,
    LauncherRepository repository,
    CatalogCredentialsService credentials,
    SharedCatalogService sharedCatalog,
    StreamingProviderDirectory providerDirectory)
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(72);
    private static readonly TimeSpan FailedAttemptCooldown = TimeSpan.FromHours(24);
    private static readonly TimeSpan ManualRefreshCooldown = TimeSpan.FromMinutes(30);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private DateTimeOffset _nextAttempt;
    private string _currentSelectionKey = "";

    public bool IsConfigured => credentials.Options.CatalogMode == "reader"
        ? !string.IsNullOrWhiteSpace(credentials.Options.SharedCatalogPath)
        : credentials.IsConfigured;
    public string? LastError { get; private set; }

    public async Task<int> RefreshAsync(bool force, CancellationToken ct)
    {
        if (!await _refreshLock.WaitAsync(0, ct)) return 0;
        try
        {
            // Leitores da casa não gastam NENHUM crédito externo, nem ao abrir o app.
            if (credentials.Options.CatalogMode == "reader")
            {
                var imported = await sharedCatalog.ImportAsync(ct);
                LastError = imported == 0 ? sharedCatalog.LastError : null;
                return imported;
            }
            // Compartilhe até o catálogo já existente, sem esperar nem gastar créditos
            // para criar o primeiro catalog.json depois de configurar o publicador.
            if (credentials.Options.CatalogMode == "publisher")
                await sharedCatalog.ExportAsync(ct);
            // Nunca consultar TODOS os streamings. Lista vazia = zero requisições.
            var selected = await repository.GetSelectedWatchmodeSourcesAsync(ct);
            if (selected.Count == 0)
            {
                LastError = "Escolha quais streamings alimentam os destaques na aba Apps.";
                return 0;
            }
            if (!credentials.IsConfigured) return 0;
            var ids = selected.Select(x => x.Id).Distinct().OrderBy(x => x).ToArray();
            var selectionKey = "streamings:" + string.Join("-", ids);
            if (_currentSelectionKey != selectionKey)
            {
                _currentSelectionKey = selectionKey;
                _nextAttempt = default;
            }
            var nowUtc = DateTimeOffset.UtcNow;
            if (!force && nowUtc < _nextAttempt) return 0;
            var last = await repository.GetCatalogCheckpointAsync(selectionKey, ct);
            if (!force && last.HasValue && nowUtc - last.Value < RefreshInterval)
            {
                _nextAttempt = last.Value.Add(RefreshInterval);
                return 0;
            }

            // O controle de tentativas é persistido no SQLite: reiniciar o app não
            // pode repetir uma sincronização cara se a API falhar ou não der resultados.
            var lastAttempt = await repository.GetLastCatalogAttemptAsync(ct);
            var cooldown = last.HasValue ? FailedAttemptCooldown : ManualRefreshCooldown;
            if (lastAttempt.HasValue && nowUtc - lastAttempt.Value < cooldown)
            {
                _nextAttempt = lastAttempt.Value.Add(cooldown);
                if (force)
                    LastError = "Aguarde o intervalo de proteção antes de consultar novamente a API.";
                return 0;
            }

            // Gravamos ANTES da primeira requisição para preservar a trava em falhas ou travamentos.
            await repository.RecordCatalogAttemptAsync(nowUtc, ct);
            _nextAttempt = nowUtc.Add(FailedAttemptCooldown);
            LastError = null;

            using var wm = httpFactory.CreateClient();
            wm.Timeout = TimeSpan.FromSeconds(18);
            wm.DefaultRequestHeaders.Add("X-API-Key", credentials.WatchmodeKey);

            using var tmdb = httpFactory.CreateClient();
            tmdb.Timeout = TimeSpan.FromSeconds(12);
            if (!string.IsNullOrWhiteSpace(credentials.TmdbKey))
                tmdb.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", credentials.TmdbKey);

            // Disponibilidade por região BR, em serviços de assinatura ou gratuitos.
            // Popularidade Watchmode é aproximação, NÃO ranking oficial dos streamings.
            var options = credentials.Options;
            List<PopularTitle> candidates;
            try { candidates = await GetPopularAsync(wm, options.MonthlyWatchmodeRequestBudget, ids, ct); }
            catch (WatchmodeBudgetExceededException)
            {
                LastError = "Orçamento mensal local de requisições Watchmode atingido.";
                return 0;
            }
            if (candidates.Count > 0 && !string.IsNullOrWhiteSpace(credentials.TmdbKey))
            {
                try
                {
                    // TMDB também pesquisa somente serviços selecionados no Brasil.
                    var trendRanks = await DiscoverTmdbTrendingAsync(tmdb, selected, await providerDirectory.GetAsync(ct), ct);
                    if (trendRanks.Count > 0)
                        candidates = candidates.OrderBy(x => trendRanks.GetValueOrDefault(
                            (x.TmdbId ?? -1, x.Type.Contains("tv", StringComparison.OrdinalIgnoreCase) ? "tv" : "movie"),
                            100_000)).ToList();
                }
                catch (HttpRequestException) { /* Watchmode continua como segunda fonte de descoberta. */ }
                catch (JsonException) { }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }
            }
            if (candidates.Count == 0)
            {
                LastError = "A fonte não retornou títulos populares disponíveis no Brasil.";
                return 0;
            }

            var saved = new List<AutoFeaturedTitle>();
            var position = 0;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Dictionary<int, string>? watchmodeGenreMap = null;
            // Orçamento pequeno por sincronização; quantidade configurável via JSON.
            // O ranking do TMDB orienta a descoberta, mas só publicamos links diretos válidos.
            var offset = (DateTimeOffset.UtcNow.DayOfYear / 3 * 7) % candidates.Count;
            // Metade vinda do topo do ranking semanal, metade deslocada pelo calendário.
            // Assim o cache cresce com novos títulos em vez de buscar os mesmos sempre.
            var topCount = options.MaxTitlesPerSync / 2;
            var dailyCandidates = candidates.Take(topCount)
                .Concat(candidates.Skip(offset).Concat(candidates.Take(offset)))
                .DistinctBy(x => x.Id).Take(options.MaxTitlesPerSync);
            foreach (var item in dailyCandidates)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    // Consultar a disponibilidade primeiro evita gastar uma segunda
                    // chamada com detalhes de títulos sem link direto utilizável.
                    using var sources = await GetWatchmodeJsonAsync(wm,
                        $"https://api.watchmode.com/v1/title/{item.Id}/sources/?regions=BR",
                        options.MonthlyWatchmodeRequestBudget, ct);
                    var stream = SelectStreaming(sources.RootElement, ids);
                    if (stream is null) continue;
                    using var details = await GetWatchmodeJsonAsync(wm,
                        $"https://api.watchmode.com/v1/title/{item.Id}/details/",
                        options.MonthlyWatchmodeRequestBudget, ct);
                    var detail = details.RootElement;
                    var name = FirstString(detail, "title", "name");
                    if (string.IsNullOrWhiteSpace(name)) name = item.Name;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var image = FirstString(detail, "backdrop", "backdrop_url", "poster", "poster_url");
                    var trailer = YoutubeVideoId(FirstString(detail, "trailer", "trailer_url", "youtube_trailer"));
                    var tmdbId = GetNumber(detail, "tmdb_id") ?? item.TmdbId;
                    var genres = ExtractGenreNames(detail);
                    var kind = item.Type.Contains("tv", StringComparison.OrdinalIgnoreCase) ? "tv" : "movie";
                    string? localizedSynopsis = null;
                    if (!string.IsNullOrWhiteSpace(credentials.TmdbKey) && tmdbId is > 0)
                    {
                        try
                        {
                            var extra = await EnrichFromTmdbAsync(tmdb, kind, tmdbId.Value, ct);
                            if (!string.IsNullOrWhiteSpace(extra.Image)) image = extra.Image;
                            if (!string.IsNullOrWhiteSpace(extra.VideoId)) trailer = extra.VideoId;
                            if (extra.Genres.Count > 0) genres = extra.Genres;
                            if (!string.IsNullOrWhiteSpace(extra.Synopsis)) localizedSynopsis = extra.Synopsis;
                        }
                        catch (HttpRequestException) { /* Mantém dados do Watchmode. */ }
                        catch (JsonException) { }
                        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }
                    }

                    if (genres.Count == 0 && HasNumericGenres(detail))
                    {
                        // A fonte pode fornecer apenas IDs; obtém o catálogo de gêneros
                        // uma vez por sincronização, sem fazer consultas por categoria.
                        if (watchmodeGenreMap is null)
                        {
                            try
                            {
                                using var genreResponse = await GetWatchmodeJsonAsync(wm,
                                    "https://api.watchmode.com/v1/genres/",
                                    options.MonthlyWatchmodeRequestBudget, ct);
                                watchmodeGenreMap = ReadGenreIdMap(genreResponse.RootElement);
                            }
                            catch (HttpRequestException) { watchmodeGenreMap = new(); }
                            catch (JsonException) { watchmodeGenreMap = new(); }
                            catch (WatchmodeBudgetExceededException) { watchmodeGenreMap = new(); }
                        }
                        genres = ExtractGenreNames(detail, watchmodeGenreMap);
                    }
                    // Sem URL direta HTTPS, nunca mostramos o título como clicável.
                    if (string.IsNullOrWhiteSpace(image) || !Uri.TryCreate(image, UriKind.Absolute, out var imageUri)
                        || imageUri.Scheme != Uri.UriSchemeHttps) continue;
                    var synopsis = localizedSynopsis ?? FirstString(detail, "plot_overview", "overview", "description");
                    saved.Add(new AutoFeaturedTitle
                    {
                        ExternalKey = $"watchmode:{item.Id}",
                        Title = name.Length > 120 ? name[..120] : name,
                        Synopsis = synopsis.Length > 450 ? synopsis[..450] : synopsis,
                        SynopsisLanguage = localizedSynopsis is null ? "en" : "pt-BR",
                        TmdbId = tmdbId,
                        Category = kind == "tv" ? "Série popular" : "Filme popular",
                        Genres = ContentGenres.Serialize(genres),
                        Provider = stream.Value.Provider,
                        WatchmodeSourceId = stream.Value.SourceId,
                        DirectUrl = stream.Value.Url,
                        TrailerVideoId = trailer,
                        BackdropUrl = image,
                        LastSeenTrendingUnix = now,
                        LastValidatedUnix = now,
                        TrendPosition = position++
                    });
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                {
                    LastError = "A chave não tem permissão para consultar esse catálogo.";
                    break;
                }
                catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.TooManyRequests)
                {
                    LastError = "Limite de consultas da API atingido. Nova tentativa mais tarde.";
                    break;
                }
                catch (WatchmodeBudgetExceededException)
                {
                    LastError = "Orçamento mensal local de requisições Watchmode atingido.";
                    break;
                }
                catch (HttpRequestException) { /* Falha isolada de título. */ }
                catch (JsonException) { }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }
            }
            if (saved.Count > 0)
            {
                await repository.UpsertAutoFeaturedAsync(saved, ct);
                await repository.SetCatalogCheckpointAsync(selectionKey, DateTimeOffset.UtcNow, ct);
                await sharedCatalog.ExportAsync(ct); // Somente o publicador escreve o snapshot LAN.
                _nextAttempt = DateTimeOffset.UtcNow.Add(RefreshInterval);
                LastError = null;
            }
            else LastError ??= "Nenhum link direto com imagem e provedor reconhecido foi encontrado no momento.";
            return saved.Count;
        }
        finally { _refreshLock.Release(); }
    }

    /// <summary>
    /// Completa o catálogo antigo sem baixar novamente imagens, links e disponibilidade.
    /// Rodar uma vez a cada 24 h quando houver gêneros faltando, no máximo 24 detalhes
    /// por execução e respeitando o mesmo orçamento mensal do Watchmode.
    /// Com token TMDB, também localiza em pt-BR, sem consumir créditos Watchmode adicionais.
    /// </summary>
    public async Task<int> EnrichExistingCatalogAsync(CancellationToken ct)
    {
        if (!credentials.CanFetch || !await _refreshLock.WaitAsync(0, ct)) return 0;
        try
        {
            var selectedIds = (await repository.GetSelectedWatchmodeSourcesAsync(ct))
                .Select(x => x.Id).ToHashSet();
            if (selectedIds.Count == 0) return 0;
            var pending = (await repository.GetMetadataPendingAsync(24,
                !string.IsNullOrWhiteSpace(credentials.TmdbKey), ct))
                .Where(x => selectedIds.Contains(x.WatchmodeSourceId)).ToArray();
            IReadOnlyList<AutoFeaturedTitle> pendingPortuguese = !string.IsNullOrWhiteSpace(credentials.TmdbKey)
                ? (await repository.GetPortuguesePendingAsync(40, ct))
                    .Where(x => selectedIds.Contains(x.WatchmodeSourceId)).ToArray()
                : Array.Empty<AutoFeaturedTitle>();
            if (pendingPortuguese.Count > 0)
            {
                var lastPortuguese = await repository.GetCatalogCheckpointAsync("ptbr-backfill", ct);
                if (lastPortuguese.HasValue && DateTimeOffset.UtcNow - lastPortuguese.Value < TimeSpan.FromHours(24))
                    pendingPortuguese = Array.Empty<AutoFeaturedTitle>();
                else await repository.SetCatalogCheckpointAsync("ptbr-backfill", DateTimeOffset.UtcNow, ct);
            }
            if (pending.Length == 0 && pendingPortuguese.Count == 0) return 0;
            // Evita gastar créditos Watchmode a cada inicialização da aplicação.
            if (pending.Length > 0)
            {
                var last = await repository.GetCatalogCheckpointAsync("metadata-backfill", ct);
                if (last.HasValue && DateTimeOffset.UtcNow - last.Value < TimeSpan.FromHours(24))
                    pending = Array.Empty<AutoFeaturedTitle>();
                else await repository.SetCatalogCheckpointAsync("metadata-backfill", DateTimeOffset.UtcNow, ct);
            }
            var count = 0;
            using var watchmode = httpFactory.CreateClient();
            watchmode.Timeout = TimeSpan.FromSeconds(15);
            watchmode.DefaultRequestHeaders.Add("X-API-Key", credentials.WatchmodeKey);
            using var tmdb = httpFactory.CreateClient();
            tmdb.Timeout = TimeSpan.FromSeconds(12);
            if (!string.IsNullOrWhiteSpace(credentials.TmdbKey))
                tmdb.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.TmdbKey);
            Dictionary<int, string>? genreMap = null;
            foreach (var entry in pending)
            {
                ct.ThrowIfCancellationRequested();
                if (!entry.ExternalKey.StartsWith("watchmode:") ||
                    !int.TryParse(entry.ExternalKey[10..], out var watchmodeId)) continue;
                try
                {
                    using var detailDoc = await GetWatchmodeJsonAsync(watchmode,
                        $"https://api.watchmode.com/v1/title/{watchmodeId}/details/",
                        credentials.Options.MonthlyWatchmodeRequestBudget, ct);
                    var detail = detailDoc.RootElement;
                    var genres = ExtractGenreNames(detail);
                    if (genres.Count == 0 && HasNumericGenres(detail))
                    {
                        if (genreMap is null)
                        {
                            using var genresResponse = await GetWatchmodeJsonAsync(watchmode,
                                "https://api.watchmode.com/v1/genres/",
                                credentials.Options.MonthlyWatchmodeRequestBudget, ct);
                            genreMap = ReadGenreIdMap(genresResponse.RootElement);
                        }
                        genres = ExtractGenreNames(detail, genreMap);
                    }
                    var tmdbId = GetNumber(detail, "tmdb_id") ?? entry.TmdbId;
                    string? synopsisPt = null;
                    if (!string.IsNullOrWhiteSpace(credentials.TmdbKey) && tmdbId is > 0)
                    {
                        try
                        {
                            var kind = entry.Category.Contains("Série", StringComparison.OrdinalIgnoreCase) ? "tv" : "movie";
                            var extra = await EnrichFromTmdbAsync(tmdb, kind, tmdbId.Value, ct);
                            synopsisPt = extra.Synopsis;
                            if (extra.Genres.Count > 0) genres = extra.Genres;
                        }
                        catch (HttpRequestException) { /* Conserva gêneros Watchmode mesmo sem TMDB. */ }
                        catch (JsonException) { }
                        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }
                    }
                    await repository.UpdateCatalogMetadataAsync(entry.ExternalKey,
                        ContentGenres.Serialize(genres), tmdbId, synopsisPt, ct);
                    count++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (WatchmodeBudgetExceededException) { break; }
                catch (HttpRequestException) { }
                catch (JsonException) { }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }
            }
            foreach (var entry in pendingPortuguese.Where(x => !pending.Any(p => p.ExternalKey == x.ExternalKey)))
            {
                ct.ThrowIfCancellationRequested();
                if (entry.TmdbId is not > 0) continue;
                try
                {
                    var kind = entry.Category.Contains("Série", StringComparison.OrdinalIgnoreCase) ? "tv" : "movie";
                    var extra = await EnrichFromTmdbAsync(tmdb, kind, entry.TmdbId.Value, ct);
                    if (extra.Genres.Count > 0 || !string.IsNullOrWhiteSpace(extra.Synopsis))
                    {
                        await repository.UpdateCatalogMetadataAsync(entry.ExternalKey,
                            ContentGenres.Serialize(extra.Genres), entry.TmdbId, extra.Synopsis, ct);
                        count++;
                    }
                }
                catch (HttpRequestException) { }
                catch (JsonException) { }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }
            }
            return count;
        }
        finally { _refreshLock.Release(); }
    }

    private async Task<List<PopularTitle>> GetPopularAsync(HttpClient client, int budget,
        IReadOnlyList<int> selectedIds, CancellationToken ct)
    {
        // API oficial: GET /v1/list-titles/ ; os resultados NÃO possuem imagens.
        if (selectedIds.Count == 0) return [];
        var sourceFilter = string.Join(",", selectedIds);
        using var doc = await GetWatchmodeJsonAsync(client,
            $"https://api.watchmode.com/v1/list-titles/?types=movie,tv_series&regions=BR&source_types=sub,free&source_ids={sourceFilter}&sort_by=popularity_desc&page=1&limit=70",
            budget, ct);
        if (!doc.RootElement.TryGetProperty("titles", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];
        var result = new List<PopularTitle>();
        foreach (var row in arr.EnumerateArray())
        {
            var id = GetNumber(row, "id");
            if (id is null or <= 0) continue;
            result.Add(new PopularTitle(id.Value, FirstString(row, "title", "name"),
                FirstString(row, "type"), GetNumber(row, "tmdb_id")));
        }
        // Variar entre atualizações diárias, sem fixar sempre os 24 primeiros títulos.
        // A rotação entre visitas continua sendo feita pelo FeaturedRotationService.
        return result.DistinctBy(x => x.Id).Take(70).ToList();
    }

    // Metadados e ranking opcionais, filtrados por provedores do Brasil.
    // O TMDB identifica disponibilidade aproximada, mas links continuam a ser
    // confirmados no Watchmode pelo ID da fonte escolhida.
    private static async Task<Dictionary<(int Id, string Kind), int>> DiscoverTmdbTrendingAsync(
        HttpClient client, IReadOnlyList<(int Id, string Name)> selected,
        IReadOnlyList<StreamingProvider> watchmodeProviders, CancellationToken ct)
    {
        var ranking = new Dictionary<(int, string), int>();
        var selectedNames = selected.Select(x =>
            StreamingProviderDirectory.Normalize(watchmodeProviders.FirstOrDefault(p => p.Id == x.Id)?.Name ?? x.Name))
            .Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal);
        var rank = 0;
        foreach (var kind in new[] { "movie", "tv" })
        {
            using var providerJson = await GetJsonAsync(client,
                $"https://api.themoviedb.org/3/watch/providers/{kind}?watch_region=BR&language=pt-BR", ct);
            if (!providerJson.RootElement.TryGetProperty("results", out var providers) ||
                providers.ValueKind != JsonValueKind.Array) continue;
            var ids = providers.EnumerateArray()
                .Where(x => x.TryGetProperty("provider_name", out var name) &&
                    selectedNames.Contains(StreamingProviderDirectory.Normalize(name.GetString())))
                .Select(x => GetNumber(x, "provider_id") ?? 0).Where(x => x > 0).Distinct().ToArray();
            if (ids.Length == 0) continue;
            var providerFilter = Uri.EscapeDataString(string.Join("|", ids));
            using var json = await GetJsonAsync(client,
                $"https://api.themoviedb.org/3/discover/{kind}?language=pt-BR&watch_region=BR&with_watch_providers={providerFilter}&with_watch_monetization_types=flatrate%7Cfree%7Cads&sort_by=popularity.desc&page=1", ct);
            if (!json.RootElement.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array) continue;
            foreach (var title in results.EnumerateArray())
            {
                var id = GetNumber(title, "id");
                if (id.HasValue) ranking.TryAdd((id.Value, kind), rank++);
            }
        }
        return ranking;
    }

    private async Task<JsonDocument> GetWatchmodeJsonAsync(HttpClient client, string url, int budget,
        CancellationToken ct)
    {
        if (!await repository.TryConsumeWatchmodeRequestAsync(budget, ct))
            throw new WatchmodeBudgetExceededException();
        return await GetJsonAsync(client, url, ct);
    }

    private sealed class WatchmodeBudgetExceededException : Exception { }

    private static (int SourceId, string Provider, string Url)? SelectStreaming(
        JsonElement sources, IReadOnlyCollection<int> selectedIds)
    {
        if (sources.ValueKind != JsonValueKind.Array) return null;
        foreach (var item in sources.EnumerateArray())
        {
            if (FirstString(item, "region") is string region && region.Length > 0 && region != "BR") continue;
            if (FirstString(item, "type") is not ("sub" or "free")) continue;
            var id = GetNumber(item, "source_id") ?? GetNumber(item, "id");
            if (!id.HasValue || !selectedIds.Contains(id.Value)) continue;
            var provider = FirstString(item, "name");
            var url = FirstString(item, "web_url");
            // O ID deve coincidir com o stream selecionado e o link ser HTTPS.
            if (!string.IsNullOrWhiteSpace(provider) && IsSecurePublicUrl(url))
                return (id.Value, provider, url);
        }
        return null;
    }

    private static async Task<(string? Image, string? VideoId, List<string> Genres, string? Synopsis)> EnrichFromTmdbAsync(
        HttpClient client, string kind, int tmdbId, CancellationToken ct)
    {
        using var doc = await GetJsonAsync(client,
            $"https://api.themoviedb.org/3/{kind}/{tmdbId}?language=pt-BR&append_to_response=videos", ct);
        var root = doc.RootElement;
        var path = FirstString(root, "backdrop_path");
        var image = path.StartsWith('/') ? $"https://image.tmdb.org/t/p/original{path}" : null;
        string? trailer = null;
        if (root.TryGetProperty("videos", out var videos) &&
            videos.TryGetProperty("results", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var video in list.EnumerateArray().OrderBy(x => FirstString(x, "type") == "Trailer" ? 0 : 1))
            {
                if (FirstString(video, "site") != "YouTube") continue;
                trailer = YoutubeVideoId(FirstString(video, "key"));
                if (trailer is not null) break;
            }
        }
        return (image, trailer, ExtractGenreNames(root), FirstString(root, "overview"));
    }

    private static bool HasNumericGenres(JsonElement root)
    {
        foreach (var key in new[] { "genre_ids", "genres", "genre_names" })
        {
            if (!root.TryGetProperty(key, out var values) || values.ValueKind != JsonValueKind.Array) continue;
            if (values.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.Number ||
                (x.ValueKind == JsonValueKind.String && int.TryParse(x.GetString(), out _)))) return true;
        }
        return false;
    }

    private static Dictionary<int, string> ReadGenreIdMap(JsonElement root)
    {
        var genres = root.ValueKind == JsonValueKind.Array ? root :
            root.ValueKind == JsonValueKind.Object && root.TryGetProperty("genres", out var arr) ? arr : default;
        var map = new Dictionary<int, string>();
        if (genres.ValueKind != JsonValueKind.Array) return map;
        foreach (var entry in genres.EnumerateArray())
        {
            var id = GetNumber(entry, "id");
            var name = FirstString(entry, "name", "genre");
            if (id.HasValue && !string.IsNullOrWhiteSpace(name)) map[id.Value] = name;
        }
        return map;
    }

    private static List<string> ExtractGenreNames(JsonElement root, Dictionary<int, string>? idMap = null)
    {
        var output = new List<string>();
        if (root.ValueKind != JsonValueKind.Object) return output;
        foreach (var property in new[] { "genre_names", "genres", "genre_ids" })
        {
            if (!root.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array) continue;
            foreach (var value in values.EnumerateArray())
            {
                string? name = value.ValueKind switch
                {
                    JsonValueKind.String when int.TryParse(value.GetString(), out var number) =>
                        idMap is not null && idMap.TryGetValue(number, out var resolvedNumber) ? resolvedNumber : null,
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Object => FirstString(value, "name", "genre"),
                    JsonValueKind.Number when value.TryGetInt32(out var id) && idMap is not null &&
                        idMap.TryGetValue(id, out var resolved) => resolved,
                    _ => null
                };
                if (ContentGenres.CanonicalName(name) is string canonical &&
                    !output.Contains(canonical, StringComparer.OrdinalIgnoreCase)) output.Add(canonical);
                if (name is not null && name.Contains("Action & Adventure", StringComparison.OrdinalIgnoreCase) &&
                    !output.Contains("Aventura", StringComparer.OrdinalIgnoreCase)) output.Add("Aventura");
            }
        }
        return output;
    }

    private static string? YoutubeVideoId(string input)
    {
        if (Regex.IsMatch(input, "^[A-Za-z0-9_-]{11}$")) return input;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return null;
        if (uri.Host is not ("youtube.com" or "www.youtube.com" or "youtu.be" or "www.youtube-nocookie.com")) return null;
        if (uri.Host == "youtu.be")
        {
            var slug = uri.AbsolutePath.Trim('/');
            if (Regex.IsMatch(slug, "^[A-Za-z0-9_-]{11}$")) return slug;
        }
        var match = Regex.Match(uri.PathAndQuery, @"(?:[?&]v=|/embed/|/shorts/|youtu\.be/)([A-Za-z0-9_-]{11})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string FirstString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return "";
        foreach (var key in names)
            if (element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        return "";
    }

    private static int? GetNumber(JsonElement element, string key)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var num)) return num;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out num)) return num;
        }
        return null;
    }

    private static bool IsSecurePublicUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") return false;
        if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return false;
        if (System.Net.IPAddress.TryParse(uri.Host, out var address))
        {
            if (System.Net.IPAddress.IsLoopback(address)) return false;
            var bytes = address.GetAddressBytes();
            if (bytes.Length == 4 && (bytes[0] == 10 || bytes[0] == 127 ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31))) return false;
        }
        return true;
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string url, CancellationToken ct)
    {
        using var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private sealed record PopularTitle(int Id, string Name, string Type, int? TmdbId);
}
