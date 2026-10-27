using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TVLauncher.App.Services;

public sealed record StreamingProvider(int Id, string Name);

/// <summary>Diretório Watchmode para seleção explícita. Cache local evita consumo a cada visita.</summary>
public sealed class StreamingProviderDirectory(
    IHttpClientFactory clients, CatalogCredentialsService credentials, LauncherRepository repository)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static string CachePath => Path.Combine(AppPaths.Root, "watchmode-sources-br.json");
    private static string LastAttemptPath => Path.Combine(AppPaths.Root, "watchmode-sources-last-attempt.txt");
    private sealed record CacheEntry(DateTimeOffset FetchedAt, List<StreamingProvider> Providers);

    public async Task<IReadOnlyList<StreamingProvider>> GetAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            CacheEntry? cache = null;
            try
            {
                if (File.Exists(CachePath))
                    cache = JsonSerializer.Deserialize<CacheEntry>(await File.ReadAllTextAsync(CachePath, ct));
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
            if (cache?.Providers.Count > 0 && DateTimeOffset.UtcNow - cache.FetchedAt < TimeSpan.FromDays(30))
                return cache.Providers;
            if (!credentials.CanFetch) return cache?.Providers ?? [];
            // Falhas de rede também têm cooldown: não consumir um crédito a cada
            // visita do usuário à aba Apps.
            try
            {
                if (File.Exists(LastAttemptPath) &&
                    DateTimeOffset.TryParse(await File.ReadAllTextAsync(LastAttemptPath, ct), out var lastAttempt) &&
                    DateTimeOffset.UtcNow - lastAttempt < TimeSpan.FromHours(24))
                    return cache?.Providers ?? [];
            }
            catch (IOException) { }
            // Lista de serviços se altera raramente; uma consulta mensal é suficiente.
            if (!await repository.TryConsumeWatchmodeRequestAsync(
                credentials.Options.MonthlyWatchmodeRequestBudget, ct)) return cache?.Providers ?? [];
            try
            {
                await File.WriteAllTextAsync(LastAttemptPath, DateTimeOffset.UtcNow.ToString("O"), ct);
                using var client = clients.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("X-API-Key", credentials.WatchmodeKey);
                using var response = await client.GetAsync(
                    "https://api.watchmode.com/v1/sources/?regions=BR&types=sub,free", ct);
                response.EnsureSuccessStatusCode();
                await using var body = await response.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(body, cancellationToken: ct);
                var list = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement :
                    doc.RootElement.TryGetProperty("sources", out var sources) ? sources : default;
                if (list.ValueKind != JsonValueKind.Array) return cache?.Providers ?? [];
                var providers = new List<StreamingProvider>();
                foreach (var item in list.EnumerateArray())
                {
                    if (!item.TryGetProperty("id", out var id) &&
                        !item.TryGetProperty("source_id", out id)) continue;
                    if (!id.TryGetInt32(out var number) || number <= 0) continue;
                    if (!item.TryGetProperty("name", out var nameNode)) continue;
                    var name = nameNode.GetString();
                    if (!string.IsNullOrWhiteSpace(name)) providers.Add(new(number, name.Trim()));
                }
                providers = providers.DistinctBy(p => p.Id).OrderBy(p => p.Name).ToList();
                if (providers.Count == 0) return cache?.Providers ?? [];
                await File.WriteAllTextAsync(CachePath,
                    JsonSerializer.Serialize(new CacheEntry(DateTimeOffset.UtcNow, providers)), ct);
                return providers;
            }
            catch (Exception e) when (e is HttpRequestException or IOException or JsonException or TaskCanceledException)
            {
                if (ct.IsCancellationRequested) throw;
                return cache?.Providers ?? [];
            }
        }
        finally { _gate.Release(); }
    }

    public static int? MatchByName(string? appName, IEnumerable<StreamingProvider> sources)
    {
        var name = Normalize(appName);
        if (string.IsNullOrEmpty(name)) return null;
        var match = sources.FirstOrDefault(x => Normalize(x.Name) == name);
        return match?.Id;
    }

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var norm = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in norm)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark &&
                char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }
}
