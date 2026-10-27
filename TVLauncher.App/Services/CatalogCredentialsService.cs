using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace TVLauncher.App.Services;

/// <summary>
/// Permite editar %LOCALAPPDATA%\TVLauncher\api-keys.json sem entrar no aplicativo.
/// As chaves no JSON são texto simples: arquivo de uso pessoal, jamais versionar ou compartilhar.
/// Credenciais protegidas por DPAPI de versões antigas continuam sendo aceitas como fallback.
/// </summary>
public sealed class CatalogCredentialsService
{
    private readonly object _gate = new();
    private Credentials _saved;

    public CatalogCredentialsService()
    {
        AppPaths.Ensure();
        if (!File.Exists(AppPaths.KeysFile))
        {
            // Se o usuário preencher o JSON no projeto antes de compilar, o arquivo
            // copiado junto do .exe passa a ser a configuração inicial da instalação.
            var shippedFile = Path.Combine(AppContext.BaseDirectory, "api-keys.json");
            if (File.Exists(shippedFile)) File.Copy(shippedFile, AppPaths.KeysFile);
            else File.WriteAllText(AppPaths.KeysFile,
                JsonSerializer.Serialize(new CatalogOptions(), JsonOptions));
        }
        _saved = ReadProtected();
    }

    public CatalogOptions Options
    {
        get
        {
            try
            {
                var options = JsonSerializer.Deserialize<CatalogOptions>(File.ReadAllText(AppPaths.KeysFile), JsonOptions)
                    ?? new CatalogOptions();
                return options.Normalize();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // JSON inválido NÃO impede o Launcher de abrir a Home.
                return new CatalogOptions();
            }
        }
    }

    public string WatchmodeKey => !string.IsNullOrWhiteSpace(Options.WatchmodeApiKey)
        ? Options.WatchmodeApiKey.Trim() : ProtectedKeys.WatchmodeKey;

    public string TmdbKey => !string.IsNullOrWhiteSpace(Options.TmdbReadAccessToken)
        ? Options.TmdbReadAccessToken.Trim() : ProtectedKeys.TmdbKey;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(WatchmodeKey);
    public bool CanFetch => Options.CatalogMode != "reader" && IsConfigured;
    private Credentials ProtectedKeys { get { lock (_gate) return _saved; } }

    // Preserva a configuração pela UI com proteção DPAPI, sem substituir o JSON do usuário.
    public void Save(string watchmodeKey, string? tmdbKey)
    {
        var keys = new Credentials((watchmodeKey ?? "").Trim(), (tmdbKey ?? "").Trim());
        if (keys.WatchmodeKey.Length < 8)
            throw new ArgumentException("Informe uma chave Watchmode válida.");
        var encrypted = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(keys), null,
            DataProtectionScope.CurrentUser);
        var file = Path.Combine(AppPaths.Root, "catalog-credentials.dat");
        File.WriteAllBytes(file + ".tmp", encrypted);
        File.Move(file + ".tmp", file, true);
        lock (_gate) _saved = keys;
    }

    private static Credentials ReadProtected()
    {
        try
        {
            var file = Path.Combine(AppPaths.Root, "catalog-credentials.dat");
            return File.Exists(file)
                ? JsonSerializer.Deserialize<Credentials>(ProtectedData.Unprotect(File.ReadAllBytes(file),
                    null, DataProtectionScope.CurrentUser)) ?? new Credentials("", "")
                : new Credentials("", "");
        }
        catch (Exception ex) when (ex is IOException or CryptographicException or JsonException)
        {
            return new Credentials("", "");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private sealed record Credentials(string WatchmodeKey, string TmdbKey);
}

public sealed class CatalogOptions
{
    public string WatchmodeApiKey { get; set; } = "";
    public string TmdbReadAccessToken { get; set; } = "";
    // standalone: consultas apenas nesta TV | publisher: consulta e publica na rede | reader: lê da rede, zero consultas de catálogo.
    public string CatalogMode { get; set; } = "standalone";
    // Exemplo: \\PC-COZINHA\TVLauncher\catalog.json (não é o arquivo de chaves).
    public string SharedCatalogPath { get; set; } = "";
    public int MaxTitlesPerSync { get; set; } = 12;
    public int MonthlyWatchmodeRequestBudget { get; set; } = 500;

    public CatalogOptions Normalize()
    {
        var mode = CatalogMode?.Trim().ToLowerInvariant();
        CatalogMode = mode is "publisher" or "reader" ? mode : "standalone";
        SharedCatalogPath = SharedCatalogPath?.Trim() ?? "";
        MaxTitlesPerSync = Math.Clamp(MaxTitlesPerSync, 1, 25);
        MonthlyWatchmodeRequestBudget = Math.Clamp(MonthlyWatchmodeRequestBudget, 1, 2500);
        return this;
    }
}
