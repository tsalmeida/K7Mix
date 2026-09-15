using System.Diagnostics;
using System.Runtime.InteropServices;

namespace K7Mix;

internal static class SingleInstance
{
    private const string MutexName = @"Local\K7Mix.SingleInstance";

    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out var createdNew);
        if (createdNew) return true;

        try { _mutex.Dispose(); } catch { /* ignore */ }
        _mutex = null;

        AppLog.Info("Another K7 Mix instance is already running; activating it and exiting");
        ActivateExistingWindow();
        return false;
    }

    public static void Release()
    {
        if (_mutex is null) return;
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try { _mutex.Dispose(); } catch { /* ignore */ }
        _mutex = null;
    }

    private static void ActivateExistingWindow()
    {
        try
        {
            var current = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                try
                {
                    if (process.Id == current.Id) continue;
                    var handle = process.MainWindowHandle;
                    if (handle == IntPtr.Zero) continue;

                    ShowWindow(handle, IsIconic(handle) ? SwRestore : SwShow);
                    SetForegroundWindow(handle);
                    return;
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not activate the existing K7 Mix window", ex);
        }
    }

    private const int SwRestore = 9;
    private const int SwShow = 5;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
}
