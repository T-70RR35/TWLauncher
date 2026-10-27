using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace TVLauncher.App.Services;

/// <summary>
/// Atualizador do instalador Velopack. Baixa em segundo plano; aplica ao fechar
/// ou após o usuário solicitar o reinício. Nunca toca os dados em AppPaths.Root.
/// </summary>
public sealed class LauncherUpdateService : IDisposable
{
    private readonly SemaphoreSlim _operation = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _timer;
    private readonly UpdateManager? _manager;
    private bool _closing;
    private bool _applying;

    public event Action? Changed;
    public string Version => _manager?.CurrentVersion?.ToString() ??
        (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.6.13");
    public bool IsInstalled => _manager?.IsInstalled == true;
    public bool Busy { get; private set; }
    public bool Ready => _manager?.UpdatePendingRestart != null;
    public int Percent { get; private set; }
    public string Status { get; private set; } = "Inicializando atualizador.";
    public string? AvailableVersion { get; private set; }

    public LauncherUpdateService()
    {
        // O GitHub Actions gera este arquivo no publish com a URL REAL do repositório.
        // Uma cópia opcional em LocalAppData permite testar um feed sem recompilar.
        var userConfig = Path.Combine(AppPaths.Root, "update-source.json");
        var shippedConfig = Path.Combine(AppContext.BaseDirectory, "update-source.json");
        var path = File.Exists(userConfig) ? userConfig : shippedConfig;
        try
        {
            if (File.Exists(path))
            {
                var config = JsonSerializer.Deserialize<UpdateSourceConfig>(
                    File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var url = config?.RepositoryUrl?.Trim().TrimEnd('/');
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    uri.Scheme == Uri.UriSchemeHttps &&
                    uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
                    uri.AbsolutePath.Trim('/').Split('/').Length == 2)
                {
                    _manager = new UpdateManager(new GithubSource(url, null, false));
                }
                else if (!string.IsNullOrWhiteSpace(url))
                {
                    Status = "Fonte de atualização inválida. Configure um repositório público HTTPS do GitHub.";
                }
            }
            if (_manager is null && Status == "Inicializando atualizador.")
                Status = "Fonte de atualizações não configurada neste build.";
            else if (_manager is not null)
                Status = _manager.IsInstalled
                    ? "Atualizações automáticas habilitadas."
                    : "Execute o instalador Setup.exe para habilitar as atualizações.";
            if (Ready) Status = "Atualização já baixada. Será aplicada ao sair.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            Status = "Não foi possível ler a configuração de atualizações: " + ex.Message;
        }

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromHours(6)
        };
        _timer.Tick += (_, _) => _ = CheckAsync(false);
    }

    public void Start()
    {
        if (!IsInstalled || _closing) return;
        _timer.Start();
        // Não bloqueia a Home. Também verifica instalações sem acesso à internet.
        _ = CheckAsync(false);
    }

    public async Task CheckAsync(bool manual)
    {
        if (_manager is null || !_manager.IsInstalled || _closing)
        {
            if (manual) Notify();
            return;
        }
        // Uma tentativa por vez (incluindo verificação periódica).
        if (!await _operation.WaitAsync(0)) return;
        try
        {
            if (Ready)
            {
                Status = "Atualização baixada. Reinicie ou feche o TW Launcher para instalar.";
                Notify();
                return;
            }
            Busy = true;
            Percent = 0;
            Status = "Verificando atualizações...";
            Notify();

            var update = await _manager.CheckForUpdatesAsync();
            if (_closing) return;
            if (update is null)
            {
                Status = $"Você já está na versão mais recente ({Version}).";
                return;
            }

            AvailableVersion = update.TargetFullRelease.Version.ToString();
            Status = $"Baixando versão {AvailableVersion}...";
            Notify();
            await _manager.DownloadUpdatesAsync(update, progress =>
            {
                Percent = progress;
                Notify();
            }, _lifetime.Token);
            if (_closing) return;
            Percent = 100;
            Status = $"Versão {AvailableVersion} pronta. Será instalada ao sair do aplicativo.";
        }
        catch (OperationCanceledException) when (_closing) { }
        catch (Exception ex)
        {
            // Sem rede ou feed indisponível não impede a navegação.
            Status = "Não foi possível verificar ou baixar: " + ex.Message;
            Debug.WriteLine("[TW Updates] " + ex);
        }
        finally
        {
            Busy = false;
            _operation.Release();
            Notify();
        }
    }

    public void ApplyAndRestart()
    {
        if (!Ready || _closing || _applying) return;
        _applying = true;
        // O updater espera o processo encerrar com limpeza natural do WebView2.
        // A chamada de fechamento é feita na janela principal, após este retorno.
    }

    public bool ShouldRestart => _applying;

    /// <summary>
    /// Chamado durante Closing, ANTES de o processo fechar. O updater espera a saída
    /// normal do aplicativo e então aplica o pacote validado pelo Velopack.
    /// </summary>
    public void ScheduleApplyOnExit()
    {
        if (_closing || !Ready || _manager is null) return;
        _closing = true;
        _timer.Stop();
        try
        {
            _manager.WaitExitThenApplyUpdates(null, silent: !_applying, restart: _applying);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[TW Updates] Falha ao agendar instalação: " + ex);
        }
    }

    private void Notify() => Changed?.Invoke();

    public void Dispose()
    {
        _closing = true;
        _timer.Stop();
        _lifetime.Cancel();
        // Não liberar _operation aqui: uma verificação ainda pode estar terminando.
    }

    private sealed class UpdateSourceConfig
    {
        public string? RepositoryUrl { get; set; }
    }
}
