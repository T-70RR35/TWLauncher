using System.IO;

namespace TVLauncher.App.Services;

public static class AppPaths
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TVLauncher");
    public static string DatabasePath => Path.Combine(Root, "tvlauncher.db");
    public static string KeysFile => Path.Combine(Root, "api-keys.json");
    public static string BrowserPath => Path.Combine(Root, "WebView2Profile");

    public static void Ensure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(BrowserPath);
    }
}
