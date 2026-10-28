using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TVLauncher.App.Services;

public enum SystemPowerAction
{
    Sleep,
    Restart,
    Shutdown
}

/// <summary>
/// Solicita as operações de energia ao Windows. Nunca usa a opção /f para
/// forçar o fechamento de aplicativos com trabalho não salvo.
/// </summary>
public sealed class SystemPowerService
{
    public async Task ExecuteAsync(SystemPowerAction action)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("As opções de energia requerem Windows.");

        if (action == SystemPowerAction.Sleep)
        {
            // O P/Invoke pode bloquear até o computador retornar da suspensão.
            await Task.Run(() =>
            {
                if (!SetSuspendState(false, false, false))
                    throw new Win32Exception(Marshal.GetLastWin32Error(),
                        "O Windows não conseguiu suspender o computador.");
            });
            return;
        }

        var command = action == SystemPowerAction.Restart ? "/r" : "/s";
        var info = new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(command);
        info.ArgumentList.Add("/t");
        info.ArgumentList.Add("0");

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Não foi possível iniciar o comando do Windows.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new Win32Exception($"O Windows recusou a operação (código {process.ExitCode}).");
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool SetSuspendState(
        [MarshalAs(UnmanagedType.U1)] bool hibernate,
        [MarshalAs(UnmanagedType.U1)] bool forceCritical,
        [MarshalAs(UnmanagedType.U1)] bool disableWakeEvent);
}
