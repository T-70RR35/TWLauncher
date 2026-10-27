using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using TVLauncher.App.Services;

namespace TVLauncher.App;

public partial class StreamingWindow : Window
{
    private readonly Action _goHome;
    private readonly WeatherService _weatherService;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _weatherTimer;
    private readonly DispatcherTimer _headerDetectionTimer;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _isFullscreen;
    private bool _isClosed;
    private bool _headerIsVisible;

    // Região de ativação virtual: não desenha uma faixa nem desloca o WebView2.
    private const double RevealDistance = 8;
    private const double HideTolerance = 6;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    public StreamingWindow(string title, Action goHome, WeatherService weatherService)
    {
        InitializeComponent();
        ServiceTitle.Text = title;
        _goHome = goHome;
        _weatherService = weatherService;
        ClockText.Text = DateTime.Now.ToString("HH:mm");

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm");

        _weatherTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(30)
        };
        _weatherTimer.Tick += async (_, _) => await UpdateWeatherAsync();

        // WebView2 recebe os próprios eventos de mouse. Verificamos a posição
        // do cursor diretamente no Windows para detectar o topo invisível.
        _headerDetectionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(90)
        };
        _headerDetectionTimer.Tick += (_, _) => UpdateHeaderVisibility();

        Loaded += StreamingWindow_Loaded;
        Closed += StreamingWindow_Closed;
    }

    private async void StreamingWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _clockTimer.Start();
        _weatherTimer.Start();
        _headerDetectionTimer.Start();
        await UpdateWeatherAsync();
    }

    private void StreamingWindow_Closed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _lifetime.Cancel();
        _clockTimer.Stop();
        _weatherTimer.Stop();
        _headerDetectionTimer.Stop();
        Browser.Dispose();
        _lifetime.Dispose();
    }

    private async Task UpdateWeatherAsync()
    {
        try
        {
            var snapshot = await _weatherService.GetAsync(_lifetime.Token);
            if (_isClosed || snapshot is null) return;

            WeatherText.Text = $"{snapshot.Temperature}°";
            WeatherSymbol.Text = snapshot.WeatherCode switch
            {
                0 => "☀",
                1 or 2 => "⛅",
                3 => "☁",
                45 or 48 => "≋",
                51 or 53 or 55 or 61 or 63 or 65 or 80 or 81 or 82 => "☂",
                95 or 96 or 99 => "ϟ",
                _ => "☁"
            };
            WeatherSymbol.ToolTip = $"{snapshot.City} · {WeatherService.Describe(snapshot.WeatherCode)}";
            WeatherText.ToolTip = WeatherSymbol.ToolTip;
        }
        catch (OperationCanceledException) when (_isClosed) { }
        catch (Exception)
        {
            // A falha meteorológica não pode interromper o streaming.
            if (!_isClosed)
            {
                WeatherText.Text = "--°";
                WeatherSymbol.ToolTip = "Clima indisponível";
            }
        }
    }

    public async Task NavigateAsync(string url)
    {
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: AppPaths.BrowserPath);
        if (_isClosed) return;
        await Browser.EnsureCoreWebView2Async(environment);
        if (_isClosed) return;

        Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
        Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
        Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        Browser.CoreWebView2.NavigationCompleted += (_, _) => UpdateNavigationButtons();
        Browser.CoreWebView2.HistoryChanged += (_, _) => UpdateNavigationButtons();
        Browser.CoreWebView2.ContainsFullScreenElementChanged += (_, _) =>
        {
            _isFullscreen = Browser.CoreWebView2.ContainsFullScreenElement;
            HideHeader();
        };
        Browser.CoreWebView2.Navigate(url);
    }

    private void UpdateNavigationButtons()
    {
        if (_isClosed || Browser.CoreWebView2 is null) return;
        BackButton.IsEnabled = Browser.CoreWebView2.CanGoBack;
        ForwardButton.IsEnabled = Browser.CoreWebView2.CanGoForward;
    }

    private void UpdateHeaderVisibility()
    {
        if (_isClosed || _isFullscreen || !IsActive || WindowState == WindowState.Minimized)
        {
            HideHeader();
            return;
        }

        if (!GetCursorPos(out var cursor)) return;

        // PointFromScreen leva em conta o DPI / escalonamento do Windows.
        var position = PointFromScreen(new Point(cursor.X, cursor.Y));
        var isInsideWindow = position.X >= 0 && position.X < ActualWidth
            && position.Y >= 0 && position.Y < ActualHeight;

        if (!_headerIsVisible)
        {
            if (isInsideWindow && position.Y <= RevealDistance)
                ShowHeader();
            return;
        }

        // Enquanto o ponteiro estiver dentro do cabeçalho ele permanece aberto.
        // Sair da área recolhe o painel, devolvendo toda a altura ao site.
        var headerHeight = Math.Max(HeaderPanel.ActualHeight, 58);
        if (!isInsideWindow || position.Y > headerHeight + HideTolerance)
            HideHeader();
    }

    private void ShowHeader()
    {
        if (_isClosed || _isFullscreen || _headerIsVisible) return;
        _headerIsVisible = true;
        HeaderPanel.Visibility = Visibility.Visible;
    }

    private void HideHeader()
    {
        if (!_headerIsVisible) return;
        _headerIsVisible = false;
        HeaderPanel.Visibility = Visibility.Collapsed;
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e) => _goHome();
    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2?.CanGoBack == true) Browser.CoreWebView2.GoBack();
    }
    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2?.CanGoForward == true) Browser.CoreWebView2.GoForward();
    }
    private void ReloadButton_Click(object sender, RoutedEventArgs e) => Browser.CoreWebView2?.Reload();
}
