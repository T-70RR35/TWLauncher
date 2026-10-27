using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace TVLauncher.App.Services;

/// <summary>Extrai o ícone associado a um .exe local, sem iniciar o programa.</summary>
public static class WindowsAppIconService
{
    private const uint SHGFI_ICON = 0x00000100;
    private const uint SHGFI_LARGEICON = 0x00000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes,
        ref SHFILEINFO info, uint infoSize, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static string? ReadIcon(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path) ||
            !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;

        var info = new SHFILEINFO();
        try
        {
            var handle = SHGetFileInfo(path, 0, ref info,
                (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
            if (handle == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
            var image = Imaging.CreateBitmapSourceFromHIcon(info.hIcon,
                Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(64, 64));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var output = new MemoryStream();
            encoder.Save(output);
            return $"data:image/png;base64,{Convert.ToBase64String(output.ToArray())}";
        }
        catch (Exception e) when (e is IOException or ExternalException or ArgumentException)
        {
            return null;
        }
        finally
        {
            if (info.hIcon != IntPtr.Zero) DestroyIcon(info.hIcon);
        }
    }
}
