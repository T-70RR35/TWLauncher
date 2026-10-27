using Microsoft.EntityFrameworkCore;
using System.IO;
using TVLauncher.App.Data;
using TVLauncher.App.Models;

namespace TVLauncher.App.Services;

public sealed class LauncherRepository(IDbContextFactory<LauncherDbContext> dbFactory)
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _initializationLock.WaitAsync();
        try
        {
            if (_initialized) return;
            AppPaths.Ensure();
            await using var db = await dbFactory.CreateDbContextAsync();
            await db.Database.EnsureCreatedAsync();
            // Atualização não destrutiva para usuários que já possuem o SQLite da v0.1.
            // EnsureCreated não acrescenta tabelas em um banco preexistente.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS noticias_cache (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Url TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    Summary TEXT NOT NULL,
                    ImageUrl TEXT NULL,
                    PublishedAtUnix INTEGER NULL,
                    FetchedAtUnix INTEGER NOT NULL
                );
                """);
            await db.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_noticias_cache_Url ON noticias_cache (Url);");

            // Migração incremental segura: EnsureCreated() não adiciona tabelas em bases anteriores.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS destaques_automaticos (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ExternalKey TEXT NOT NULL, Title TEXT NOT NULL, Synopsis TEXT NOT NULL,
                    Category TEXT NOT NULL, Provider TEXT NOT NULL, Genres TEXT NOT NULL DEFAULT '', SynopsisLanguage TEXT NOT NULL DEFAULT 'en', TmdbId INTEGER NULL, WatchmodeSourceId INTEGER NOT NULL DEFAULT 0, DirectUrl TEXT NOT NULL,
                    TrailerVideoId TEXT NULL, BackdropUrl TEXT NULL,
                    LastSeenTrendingUnix INTEGER NOT NULL, LastValidatedUnix INTEGER NOT NULL,
                    TrendPosition INTEGER NOT NULL
                );
                """);
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_destaques_automaticos_ExternalKey ON destaques_automaticos (ExternalKey);");
            // Bases anteriores à v0.6.4 precisam da coluna sem apagar os dados existentes.
            await db.Database.OpenConnectionAsync();
            try
            {
                await using var pragma = db.Database.GetDbConnection().CreateCommand();
                pragma.CommandText = "SELECT COUNT(*) FROM pragma_table_info('destaques_automaticos') WHERE name = 'Genres'";
                var hasGenres = Convert.ToInt32(await pragma.ExecuteScalarAsync()) > 0;
                if (!hasGenres)
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE destaques_automaticos ADD COLUMN Genres TEXT NOT NULL DEFAULT '';");
                // Migrações incrementais, sem apagar filmes/links nem configurações antigas.
                pragma.CommandText = "SELECT COUNT(*) FROM pragma_table_info('destaques_automaticos') WHERE name = 'SynopsisLanguage'";
                if (Convert.ToInt32(await pragma.ExecuteScalarAsync()) == 0)
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE destaques_automaticos ADD COLUMN SynopsisLanguage TEXT NOT NULL DEFAULT 'en';");
                pragma.CommandText = "SELECT COUNT(*) FROM pragma_table_info('destaques_automaticos') WHERE name = 'WatchmodeSourceId'";
                if (Convert.ToInt32(await pragma.ExecuteScalarAsync()) == 0)
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE destaques_automaticos ADD COLUMN WatchmodeSourceId INTEGER NOT NULL DEFAULT 0;");
                pragma.CommandText = "SELECT COUNT(*) FROM pragma_table_info('destaques_automaticos') WHERE name = 'TmdbId'";
                if (Convert.ToInt32(await pragma.ExecuteScalarAsync()) == 0)
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE destaques_automaticos ADD COLUMN TmdbId INTEGER NULL;");
            }
            finally { await db.Database.CloseConnectionAsync(); }

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS destaques_exibicoes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, FeaturedKey TEXT NOT NULL,
                    ShownAtUnix INTEGER NOT NULL
                );
                """);
            await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_destaques_exibicoes_ShownAtUnix ON destaques_exibicoes (ShownAtUnix);");
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS catalogo_orcamento (
                    Month TEXT PRIMARY KEY,
                    Used INTEGER NOT NULL DEFAULT 0
                );
                """);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS catalogo_consultas (
                    Key TEXT NOT NULL PRIMARY KEY,
                    LastAttemptUnix INTEGER NOT NULL
                );
                """);

            // v0.6.6: mantém a Home dos usuários existentes intacta.
            // A coluna é acrescentada apenas se ainda não existir; a primeira migração
            // fixa os atalhos existentes e novos cadastros começam desafixados.
            await db.Database.OpenConnectionAsync();
            try
            {
                await using var cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('aplicativos') WHERE name = 'PinnedToHome'";
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0)
                {
                    await db.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE aplicativos ADD COLUMN PinnedToHome INTEGER NOT NULL DEFAULT 0;");
                    await db.Database.ExecuteSqlRawAsync("UPDATE aplicativos SET PinnedToHome = 1;");
                }
                cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('aplicativos') WHERE name = 'IncludeInFeatured'";
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0)
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE aplicativos ADD COLUMN IncludeInFeatured INTEGER NOT NULL DEFAULT 0;");
                cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('aplicativos') WHERE name = 'WatchmodeSourceId'";
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0)
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE aplicativos ADD COLUMN WatchmodeSourceId INTEGER NULL;");
                cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('aplicativos') WHERE name = 'IconDataUrl'";
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0)
                    await db.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE aplicativos ADD COLUMN IconDataUrl TEXT NULL;");
                cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('aplicativos') WHERE name = 'IconCheckedAtUnix'";
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0)
                    await db.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE aplicativos ADD COLUMN IconCheckedAtUnix INTEGER NOT NULL DEFAULT 0;");
            }
            finally { await db.Database.CloseConnectionAsync(); }

            if (!await db.Apps.AnyAsync())
            {
                db.Apps.AddRange(
                    new LauncherApp { Name = "Netflix", Kind = "web", Target = "https://www.netflix.com/br/", Icon = "netflix", SortOrder = 0, IsBuiltIn = true, PinnedToHome = true },
                    new LauncherApp { Name = "YouTube", Kind = "web", Target = "https://www.youtube.com/", Icon = "youtube", SortOrder = 1, IsBuiltIn = true, PinnedToHome = true },
                    new LauncherApp { Name = "Prime Video", Kind = "web", Target = "https://www.primevideo.com/", Icon = "prime", SortOrder = 2, IsBuiltIn = true, PinnedToHome = true },
                    new LauncherApp { Name = "Spotify", Kind = "web", Target = "https://open.spotify.com/", Icon = "spotify", SortOrder = 3, IsBuiltIn = true, PinnedToHome = true },
                    new LauncherApp { Name = "Navegador", Kind = "web", Target = "https://www.google.com/", Icon = "globe", SortOrder = 4, IsBuiltIn = true, PinnedToHome = true }
                );
            }

            // Sem exemplos fixos inseridos automaticamente. O catálogo vem da API;
            // títulos manuais permanecem opcionais para quem quiser utilizá-los.
            await db.SaveChangesAsync();
            _initialized = true;
        }
        finally { _initializationLock.Release(); }
    }

    public async Task<IReadOnlyList<LauncherApp>> GetAppsAsync()
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Apps.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync();
    }

    public async Task<IReadOnlyList<FeaturedTitle>> GetFeaturedAsync()
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Featured.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync();
    }

    public async Task<IReadOnlyList<FeaturedTitle>> GetHomeFeaturedAsync()
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeSeconds();
        var automatic = await db.AutoFeatured.AsNoTracking()
            .Where(x => x.LastSeenTrendingUnix >= cutoff && x.LastValidatedUnix >= cutoff)
            .OrderBy(x => x.TrendPosition).Take(100).ToListAsync();
        // Filtragem também no banco: cache antigo ou outro dispositivo não vaza
        // filmes de serviços que o usuário não selecionou.
        var selectedSources = await db.Apps.AsNoTracking()
            .Where(x => x.Kind == "web" && x.IncludeInFeatured && x.WatchmodeSourceId != null)
            .Select(x => x.WatchmodeSourceId!.Value).Distinct().ToListAsync();
        var allowedApps = await db.Apps.AsNoTracking()
            .Where(x => x.Kind == "web" && x.IncludeInFeatured && x.WatchmodeSourceId != null)
            .ToListAsync();
        var manual = await db.Featured.AsNoTracking().Where(x => !x.IsSample).ToListAsync();
        var approvedManual = manual.Where(title =>
            Uri.TryCreate(title.DirectUrl, UriKind.Absolute, out var titleUrl) &&
            allowedApps.Any(app => Uri.TryCreate(app.Target, UriKind.Absolute, out var appUrl) &&
                (titleUrl.Host.Equals(appUrl.Host, StringComparison.OrdinalIgnoreCase) ||
                 titleUrl.Host.EndsWith("." + appUrl.Host, StringComparison.OrdinalIgnoreCase))));
        return automatic.Where(x => selectedSources.Contains(x.WatchmodeSourceId))
            .Select(x => x.ToFeatured()).Concat(approvedManual).ToArray();
    }

    public async Task<DateTimeOffset?> GetCatalogCheckpointAsync(string key, CancellationToken ct)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var unix = await db.CatalogSyncCheckpoints.AsNoTracking()
            .Where(x => x.Key == key).Select(x => (long?)x.LastAttemptUnix).FirstOrDefaultAsync(ct);
        return unix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(unix.Value) : null;
    }

    public async Task SetCatalogCheckpointAsync(string key, DateTimeOffset when, CancellationToken ct)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var record = await db.CatalogSyncCheckpoints.FindAsync([key], ct);
        if (record is null)
            db.CatalogSyncCheckpoints.Add(new CatalogSyncCheckpoint
            { Key = key, LastAttemptUnix = when.ToUnixTimeSeconds() });
        else record.LastAttemptUnix = when.ToUnixTimeSeconds();
        await db.SaveChangesAsync(ct);
    }

    public async Task<DateTimeOffset?> GetLastCatalogAttemptAsync(CancellationToken ct = default)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var unix = await db.CatalogSyncCheckpoints.AsNoTracking()
            .Where(x => x.Key == "watchmode")
            .Select(x => (long?)x.LastAttemptUnix)
            .FirstOrDefaultAsync(ct);
        return unix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(unix.Value) : null;
    }

    public async Task RecordCatalogAttemptAsync(DateTimeOffset at, CancellationToken ct = default)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entry = await db.CatalogSyncCheckpoints.FindAsync(["watchmode"], ct);
        if (entry is null)
        {
            db.CatalogSyncCheckpoints.Add(new CatalogSyncCheckpoint
            {
                Key = "watchmode", LastAttemptUnix = at.ToUnixTimeSeconds()
            });
        }
        else entry.LastAttemptUnix = at.ToUnixTimeSeconds();
        await db.SaveChangesAsync(ct);
    }

    public async Task<DateTimeOffset?> GetLastCatalogSyncAsync()
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var unix = await db.AutoFeatured.MaxAsync(x => (long?)x.LastSeenTrendingUnix);
        return unix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(unix.Value) : null;
    }

    public async Task UpsertAutoFeaturedAsync(IEnumerable<AutoFeaturedTitle> items, CancellationToken cancellationToken)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        foreach (var candidate in items)
        {
            var existing = await db.AutoFeatured.FirstOrDefaultAsync(x => x.ExternalKey == candidate.ExternalKey, cancellationToken);
            if (existing is null) db.AutoFeatured.Add(candidate);
            else
            {
                existing.Title = candidate.Title;
                existing.Category = candidate.Category;
                // Não perder metadados enriquecidos ao atualizar disponibilidade.
                existing.Genres = string.IsNullOrWhiteSpace(candidate.Genres) ? existing.Genres : candidate.Genres;
                if (candidate.SynopsisLanguage == "pt-BR" || existing.SynopsisLanguage != "pt-BR")
                {
                    existing.Synopsis = candidate.Synopsis;
                    existing.SynopsisLanguage = candidate.SynopsisLanguage;
                }
                existing.TmdbId = candidate.TmdbId ?? existing.TmdbId;
                existing.WatchmodeSourceId = candidate.WatchmodeSourceId;
                existing.Provider = candidate.Provider;
                existing.DirectUrl = candidate.DirectUrl;
                existing.TrailerVideoId = candidate.TrailerVideoId;
                existing.BackdropUrl = candidate.BackdropUrl;
                existing.LastSeenTrendingUnix = candidate.LastSeenTrendingUnix;
                existing.LastValidatedUnix = candidate.LastValidatedUnix;
                existing.TrendPosition = candidate.TrendPosition;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        var olderThan = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeSeconds();
        await db.AutoFeatured.Where(x => x.LastSeenTrendingUnix < olderThan).ExecuteDeleteAsync(cancellationToken);
    }


    public async Task<IReadOnlyList<AutoFeaturedTitle>> GetMetadataPendingAsync(int limit, bool recoverTmdbId, CancellationToken ct)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeSeconds();
        return await db.AutoFeatured.AsNoTracking()
            .Where(x => x.LastValidatedUnix >= cutoff &&
                (x.Genres == "" || (recoverTmdbId && x.TmdbId == null && x.SynopsisLanguage != "pt-BR")))
            .OrderBy(x => x.TrendPosition).Take(limit).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AutoFeaturedTitle>> GetPortuguesePendingAsync(int limit, CancellationToken ct)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeSeconds();
        return await db.AutoFeatured.AsNoTracking()
            .Where(x => x.LastValidatedUnix >= cutoff && x.TmdbId != null && x.SynopsisLanguage != "pt-BR")
            .OrderBy(x => x.TrendPosition).Take(limit).ToListAsync(ct);
    }

    public async Task UpdateCatalogMetadataAsync(
        string key, string? genres, int? tmdbId, string? portugueseSynopsis, CancellationToken ct)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entry = await db.AutoFeatured.FirstOrDefaultAsync(x => x.ExternalKey == key, ct);
        if (entry is null) return;
        if (!string.IsNullOrWhiteSpace(genres)) entry.Genres = genres;
        if (tmdbId is > 0) entry.TmdbId = tmdbId;
        if (!string.IsNullOrWhiteSpace(portugueseSynopsis))
        {
            entry.Synopsis = portugueseSynopsis.Length > 450 ? portugueseSynopsis[..450] : portugueseSynopsis;
            entry.SynopsisLanguage = "pt-BR";
        }
        await db.SaveChangesAsync(ct);
    }

    // Controle local, por mês, gravado ANTES de cada chamada Watchmode.
    // Não substitui a quota oficial (outros apps da mesma chave podem consumir créditos).
    public async Task<bool> TryConsumeWatchmodeRequestAsync(int monthlyBudget, CancellationToken ct)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var month = DateTimeOffset.UtcNow.ToString("yyyy-MM");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT OR IGNORE INTO catalogo_orcamento (Month, Used) VALUES ({month}, 0)", ct);
        var affected = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE catalogo_orcamento SET Used = Used + 1 WHERE Month = {month} AND Used < {monthlyBudget}", ct);
        return affected == 1;
    }

    public async Task<IReadOnlyList<AutoFeaturedTitle>> GetAutoFeaturedForSharingAsync(CancellationToken ct)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var since = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeSeconds();
        return await db.AutoFeatured.AsNoTracking()
            .Where(x => x.LastValidatedUnix >= since && x.LastSeenTrendingUnix >= since)
            .OrderBy(x => x.TrendPosition).Take(100).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FeaturedViewEvent>> GetRecentFeaturedViewsAsync(CancellationToken token)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(token);
        var since = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
        return await db.FeaturedViews.AsNoTracking().Where(x => x.ShownAtUnix >= since)
            .OrderByDescending(x => x.Id).Take(250).ToListAsync(token);
    }

    public async Task RecordFeaturedViewAsync(string key, CancellationToken token)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(token);
        db.FeaturedViews.Add(new FeaturedViewEvent
        {
            FeaturedKey = key,
            ShownAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        await db.SaveChangesAsync(token);
        // Histórico limitado: sem crescimento ilimitado do SQLite.
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
        await db.FeaturedViews.Where(x => x.ShownAtUnix < cutoff).ExecuteDeleteAsync(token);
    }

    private static (string Name, string Kind, string Target) ValidateApp(
        string? name, string? kind, string? target)
    {
        name = name?.Trim() ?? "";
        kind = kind?.Trim() ?? "";
        target = target?.Trim() ?? "";
        if (kind == "web" && !target.Contains("://", StringComparison.Ordinal) && target.Contains('.'))
            target = "https://" + target;
        if (name.Length is < 2 or > 70)
            throw new ArgumentException("Informe um nome entre 2 e 70 caracteres.");
        if (kind == "web")
        {
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                throw new ArgumentException("Informe uma URL HTTP ou HTTPS válida.");
            target = uri.AbsoluteUri;
        }
        else if (kind == "desktop")
        {
            if (!Path.IsPathFullyQualified(target) || !File.Exists(target) ||
                !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Informe o caminho completo de um executável .exe existente.");
        }
        else throw new ArgumentException("Tipo de aplicativo inválido.");
        return (name, kind, target);
    }

    private static void ValidateFeaturedSelection(string kind, bool include, int? sourceId)
    {
        if (include && (kind != "web" || sourceId is null or <= 0))
            throw new ArgumentException("Escolha um serviço de streaming do Watchmode para incluir nos destaques.");
    }

    public async Task AddAppAsync(string name, string kind, string target,
        bool pinnedToHome, string? iconDataUrl, bool includeInFeatured = false, int? watchmodeSourceId = null)
    {
        await InitializeAsync();
        (name, kind, target) = ValidateApp(name, kind, target);
        ValidateFeaturedSelection(kind, includeInFeatured, watchmodeSourceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var maxOrder = await db.Apps.MaxAsync(x => (int?)x.SortOrder) ?? 0;
        db.Apps.Add(new LauncherApp
        {
            Name = name, Kind = kind, Target = target, PinnedToHome = pinnedToHome,
            IncludeInFeatured = includeInFeatured, WatchmodeSourceId = kind == "web" ? watchmodeSourceId : null,
            Icon = kind == "web" ? "globe" : "monitor", SortOrder = maxOrder + 1,
            IconDataUrl = iconDataUrl,
            IconCheckedAtUnix = iconDataUrl is not null
                ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0
        });
        await db.SaveChangesAsync();
    }

    public async Task UpdateAppAsync(int id, string name, string kind, string target,
        bool pinnedToHome, string? iconDataUrl, bool includeInFeatured = false, int? watchmodeSourceId = null)
    {
        await InitializeAsync();
        (name, kind, target) = ValidateApp(name, kind, target);
        ValidateFeaturedSelection(kind, includeInFeatured, watchmodeSourceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var app = await db.Apps.FindAsync(id) ?? throw new ArgumentException("Atalho não encontrado.");
        var changedTarget = app.Target != target || app.Kind != kind;
        app.Name = name;
        app.Kind = kind;
        app.Target = target;
        app.PinnedToHome = pinnedToHome;
        ValidateFeaturedSelection(kind, includeInFeatured, watchmodeSourceId);
        app.IncludeInFeatured = includeInFeatured;
        app.WatchmodeSourceId = kind == "web" ? watchmodeSourceId : null;
        if (changedTarget)
        {
            app.Icon = kind == "web" ? "globe" : "monitor";
            app.IconDataUrl = iconDataUrl;
            app.IconCheckedAtUnix = iconDataUrl is not null
                ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0;
        }
        else if (iconDataUrl is not null)
        {
            app.IconDataUrl = iconDataUrl;
            app.IconCheckedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        await db.SaveChangesAsync();
    }

    public async Task SetFeaturedStreamingAsync(int id, bool enabled)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var app = await db.Apps.FindAsync(id);
        if (app is null) return;
        ValidateFeaturedSelection(app.Kind, enabled, app.WatchmodeSourceId);
        app.IncludeInFeatured = enabled;
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<(int Id, string Name)>> GetSelectedWatchmodeSourcesAsync(CancellationToken ct = default)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var apps = await db.Apps.AsNoTracking()
            .Where(x => x.Kind == "web" && x.IncludeInFeatured && x.WatchmodeSourceId != null)
            .Select(x => new { x.WatchmodeSourceId, x.Name }).ToListAsync(ct);
        return apps.GroupBy(x => x.WatchmodeSourceId!.Value)
            .Select(g => (Id: g.Key, Name: g.First().Name))
            .OrderBy(x => x.Id).ToArray();
    }

    public async Task SetAppPinnedAsync(int id, bool pinned)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var app = await db.Apps.FindAsync(id);
        if (app is null) return;
        app.PinnedToHome = pinned;
        await db.SaveChangesAsync();
    }

    public async Task SaveAppIconAsync(int id, string target, string? imageDataUrl)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var app = await db.Apps.FindAsync(id);
        if (app is null || app.Target != target || app.Kind != "web") return;
        if (!string.IsNullOrEmpty(imageDataUrl)) app.IconDataUrl = imageDataUrl;
        app.IconCheckedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await db.SaveChangesAsync();
    }

    public async Task AddFeaturedAsync(string title, string synopsis, string directUrl, string? youtubeVideoId)
    {
        await InitializeAsync();
        title = title?.Trim() ?? "";
        synopsis = synopsis?.Trim() ?? "";
        directUrl = directUrl?.Trim() ?? "";
        youtubeVideoId = youtubeVideoId?.Trim();
        if (title.Length is < 2 or > 120) throw new ArgumentException("Informe um título com até 120 caracteres.");
        if (synopsis.Length > 450) throw new ArgumentException("A sinopse pode ter até 450 caracteres.");
        if (!Uri.TryCreate(directUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            throw new ArgumentException("Informe um link direto HTTP/HTTPS válido.");
        if (!string.IsNullOrWhiteSpace(youtubeVideoId) &&
            (youtubeVideoId.Length != 11 || youtubeVideoId.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-')))
            throw new ArgumentException("O ID de vídeo do YouTube deve conter 11 caracteres válidos.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var maxOrder = await db.Featured.MaxAsync(x => (int?)x.SortOrder) ?? 0;
        db.Featured.Add(new FeaturedTitle
        {
            Title = title, Synopsis = synopsis, DirectUrl = directUrl,
            TrailerVideoId = youtubeVideoId, Category = "Personalizado",
            Provider = uri.Host, SortOrder = maxOrder + 1, IsSample = false
        });
        await db.SaveChangesAsync();
    }

    public async Task RemoveAppAsync(int id)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var app = await db.Apps.FindAsync(id);
        if (app is null || app.IsBuiltIn) return;
        db.Apps.Remove(app);
        await db.SaveChangesAsync();
    }

    public async Task RemoveFeaturedAsync(int id)
    {
        await InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.Featured.FindAsync(id);
        if (item is null) return;
        db.Featured.Remove(item);
        await db.SaveChangesAsync();
    }
}
