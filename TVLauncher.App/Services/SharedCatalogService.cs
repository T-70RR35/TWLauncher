using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using TVLauncher.App.Models;

namespace TVLauncher.App.Services;

/// <summary>
/// Um PC publica o catálogo na pasta de rede; as outras TVs apenas o leem.
/// Nunca compartilha chaves, perfis WebView2, cookies ou o SQLite entre máquinas.
/// Destinado somente aos dispositivos do próprio usuário; ver termos das fontes.
/// </summary>
public sealed class SharedCatalogService(LauncherRepository repository, CatalogCredentialsService credentials)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly SemaphoreSlim FileGate = new(1, 1);
    public string? LastError { get; private set; }

    public async Task<int> ImportAsync(CancellationToken ct)
    {
        var path = credentials.Options.SharedCatalogPath;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return 0;
        try
        {
            if (!File.Exists(path)) return 0;
            var info = new FileInfo(path);
            if (info.Length > 3_000_000) throw new InvalidDataException("Catálogo de rede muito grande.");
            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var payload = await JsonSerializer.DeserializeAsync<SharedCatalogSnapshot>(stream, JsonOptions, ct);
            if (payload is null || payload.SchemaVersion != 1) return 0;
            // Dados Watchmode gratuitos têm validade limitada; nunca prolongar o carimbo de validação.
            var cutoff = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeSeconds();
            var valid = (payload.Titles ?? []).Take(100)
                .Where(x => x.LastValidatedUnix >= cutoff && x.LastSeenTrendingUnix >= cutoff)
                .Where(ValidateTitle)
                .ToArray();
            if (valid.Length == 0) return 0;
            // IDs SQLite são locais a cada dispositivo: nunca importar o ID do publicador.
            var localEntries = valid.Select(x => new AutoFeaturedTitle
            {
                ExternalKey = x.ExternalKey,
                Title = x.Title,
                Synopsis = x.Synopsis,
                Category = x.Category,
                Genres = x.Genres ?? "",
                Provider = x.Provider,
                DirectUrl = x.DirectUrl,
                TrailerVideoId = x.TrailerVideoId,
                BackdropUrl = x.BackdropUrl,
                LastSeenTrendingUnix = x.LastSeenTrendingUnix,
                LastValidatedUnix = x.LastValidatedUnix,
                TrendPosition = x.TrendPosition
            }).ToArray();
            await repository.UpsertAutoFeaturedAsync(localEntries, ct);
            LastError = null;
            return valid.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            LastError = "Catálogo compartilhado indisponível: " + ex.Message;
            return 0;
        }
    }

    public async Task ExportAsync(CancellationToken ct)
    {
        if (credentials.Options.CatalogMode != "publisher") return;
        var path = credentials.Options.SharedCatalogPath;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return;
        await FileGate.WaitAsync(ct);
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                throw new DirectoryNotFoundException("Configure uma pasta de rede já existente e com permissão de escrita.");
            var titles = await repository.GetAutoFeaturedForSharingAsync(ct);
            var payload = new SharedCatalogSnapshot
            {
                GeneratedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Titles = titles.Where(ValidateTitle).ToArray()
            };
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(payload, JsonOptions), ct);
                File.Move(temporary, path, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            LastError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastError = "Não foi possível publicar o catálogo compartilhado: " + ex.Message;
        }
        finally { FileGate.Release(); }
    }

    private static bool ValidateTitle(AutoFeaturedTitle x)
    {
        if (string.IsNullOrWhiteSpace(x.ExternalKey) || x.ExternalKey.Length > 50 ||
            string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 120 ||
            string.IsNullOrWhiteSpace(x.Provider) || x.Provider.Length > 70 ||
            x.Synopsis is null || x.Synopsis.Length > 450 ||
            x.Category is null || x.Category.Length > 32 ||
            x.Genres is null || x.Genres.Length > 500) return false;
        if (!Uri.TryCreate(x.DirectUrl, UriKind.Absolute, out var dest) || dest.Scheme != "https") return false;
        if (!Uri.TryCreate(x.BackdropUrl, UriKind.Absolute, out var image) || image.Scheme != "https") return false;
        if (!string.IsNullOrWhiteSpace(x.TrailerVideoId) &&
            !Regex.IsMatch(x.TrailerVideoId, "^[A-Za-z0-9_-]{11}$")) return false;
        var allowed = x.Provider switch
        {
            "Netflix" => new[] { "netflix.com" },
            "Prime Video" => new[] { "primevideo.com", "amazon.com", "amazon.com.br" },
            "Disney+" => new[] { "disneyplus.com" },
            "Max" => new[] { "max.com", "hbomax.com" },
            "Globoplay" => new[] { "globoplay.globo.com" },
            "Apple TV+" => new[] { "tv.apple.com" },
            "Paramount+" => new[] { "paramountplus.com" },
            "Crunchyroll" => new[] { "crunchyroll.com" },
            _ => Array.Empty<string>()
        };
        return allowed.Any(host => dest.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
            dest.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class SharedCatalogSnapshot
    {
        public int SchemaVersion { get; set; } = 1;
        public long GeneratedAtUnix { get; set; }
        public AutoFeaturedTitle[] Titles { get; set; } = [];
    }
}
