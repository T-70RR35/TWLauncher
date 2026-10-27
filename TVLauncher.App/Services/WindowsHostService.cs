using System.Diagnostics;
using System.IO;
using System.Windows;

namespace TVLauncher.App.Services;

public sealed class WindowsHostService : IDisposable
{
    private MainWindow? _main;
    private StreamingWindow? _browser;
    private Process? _desktop;
    private bool _returning;
    private readonly WeatherService _weatherService;

    public WindowsHostService(WeatherService weatherService) => _weatherService = weatherService;

    public event Action? HomeRestored;

    public void Attach(MainWindow mainWindow) => _main = mainWindow;

    public void RequestUpdateRestart()
    {
        if (_main is null || _main.Dispatcher.HasShutdownStarted) return;
        if (_main.Dispatcher.CheckAccess()) _main.RestartForUpdate();
        else _main.Dispatcher.BeginInvoke(_main.RestartForUpdate);
    }

    public async Task OpenWebAsync(string url, string title)
    {
        if (!IsHttpUrl(url))
            throw new ArgumentException("Somente URLs HTTPS/HTTP são aceitas.", nameof(url));

        if (_main is null) throw new InvalidOperationException("Janela principal não inicializada.");

        // O dispatcher evita manipular janelas WPF a partir de uma thread do Blazor.
        await _main.Dispatcher.InvokeAsync(async () =>
        {
            ReturnHomeCore(notify: false);
            var browser = new StreamingWindow(title, ReturnHome, _weatherService);
            _browser = browser;
            browser.Closed += (_, _) =>
            {
                if (ReferenceEquals(_browser, browser)) ReturnHome();
            };
            browser.Show();
            _main.Hide();
            try
            {
                await browser.NavigateAsync(url);
            }
            catch
            {
                ReturnHomeCore();
                throw;
            }
        }).Task.Unwrap();
    }

    public async Task OpenDesktopAsync(string executable, string title)
    {
        if (string.IsNullOrWhiteSpace(executable) ||
            !Path.IsPathFullyQualified(executable) ||
            !File.Exists(executable) ||
            !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Selecione um executável .exe existente e com caminho absoluto.");
        }

        if (_main is null) throw new InvalidOperationException("Janela principal não inicializada.");
        await _main.Dispatcher.InvokeAsync(() =>
        {
            ReturnHomeCore(notify: false);
            var process = Process.Start(new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(executable)
            }) ?? throw new InvalidOperationException("O Windows não iniciou o aplicativo.");
            _desktop = process;
            _main.Hide();
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => ReturnHome();
        });
    }

    public void ShowDesktop()
    {
        if (_main is null) return;
        _main.Dispatcher.Invoke(() =>
        {
            ReturnHomeCore();
            _main.Hide();
        });
    }

    public void ReturnHome()
    {
        if (_main is null || _main.Dispatcher.HasShutdownStarted) return;
        if (_main.Dispatcher.CheckAccess()) ReturnHomeCore();
        else _main.Dispatcher.BeginInvoke(ReturnHomeCore);
    }

    private void ReturnHomeCore(bool notify = true)
    {
        if (_main is null || _returning) return;
        _returning = true;
        try
        {
            // Primeiro apresenta a Home; depois encerra apenas a janela que controlamos.
            _main.Show();
            _main.WindowState = WindowState.Maximized;
            _main.Activate();
            if (notify) HomeRestored?.Invoke();
            if (_browser is { } browser)
            {
                _browser = null;
                browser.Close();
            }
            if (_desktop is { } process)
            {
                _desktop = null;
                try
                {
                    if (!process.HasExited) process.CloseMainWindow();
                }
                catch (InvalidOperationException) { }
                finally { process.Dispose(); }
            }
        }
        finally { _returning = false; }
    }

    private static bool IsHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public void Dispose()
    {
        var browser = _browser;
        _browser = null;
        browser?.Close();
        _desktop?.Dispose();
        _desktop = null;
    }
}
