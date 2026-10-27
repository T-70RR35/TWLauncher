using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using TVLauncher.App.Data;
using TVLauncher.App.Services;

namespace TVLauncher.App;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x5411;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkHome = 0x24;

    private readonly ServiceProvider _provider;
    private readonly WindowsHostService _host;
    private readonly LauncherUpdateService _updater;
    private HwndSource? _hookSource;
    private bool _hotkeyRegistered;

    public MainWindow()
    {
        var collection = new ServiceCollection();
        collection.AddWpfBlazorWebView();
        collection.AddDbContextFactory<LauncherDbContext>(options =>
            options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));
        collection.AddSingleton<LauncherRepository>();
        collection.AddSingleton<WindowsHostService>();
        collection.AddSingleton<LauncherUpdateService>();
        collection.AddSingleton<WeatherService>();
        collection.AddSingleton<NewsService>();
        collection.AddSingleton<CatalogCredentialsService>();
        collection.AddSingleton<TrendingCatalogService>();
        collection.AddSingleton<SharedCatalogService>();
        collection.AddSingleton<FeaturedRotationService>();
        collection.AddSingleton<StreamingProviderDirectory>();
        collection.AddSingleton<AppIconService>();
        collection.AddHttpClient();

        _provider = collection.BuildServiceProvider();
        _host = _provider.GetRequiredService<WindowsHostService>();
        _updater = _provider.GetRequiredService<LauncherUpdateService>();
        _host.Attach(this);
        Resources.Add("services", _provider);

        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _provider.GetRequiredService<LauncherRepository>().InitializeAsync();
        }
        catch (Exception error)
        {
            MessageBox.Show($"Não foi possível inicializar o banco local.\n{error.Message}",
                "TW Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        // A verificação ocorre mesmo se o catálogo ainda estiver indisponível.
        _updater.Start();
    }

    public void RestartForUpdate()
    {
        if (!_updater.Ready) return;
        _updater.ApplyAndRestart();
        Close();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hookSource = HwndSource.FromHwnd(hwnd);
        _hookSource?.AddHook(HotkeyHook);
        _hotkeyRegistered = RegisterHotKey(hwnd, HotkeyId, ModAlt | ModControl, VkHome);
        if (!_hotkeyRegistered)
        {
            Title = "TW Launcher (atalho Ctrl+Alt+Home indisponível)";
        }
    }

    private IntPtr HotkeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            _host.ReturnHome();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_hotkeyRegistered)
            UnregisterHotKey(new WindowInteropHelper(this).Handle, HotkeyId);
        _hookSource?.RemoveHook(HotkeyHook);
        _host.Dispose();
        // Agenda a instalação só depois de fechar o navegador e salvar seus cookies.
        _updater.ScheduleApplyOnExit();
        _provider.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
