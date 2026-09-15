using Avalonia;
using Avalonia.Media;

namespace K7Mix;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLog.Initialize();

        if (!SingleInstance.TryAcquire())
            return;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            AppLog.Info("K7 Mix closed normally");
        }
        catch (Exception ex)
        {
            AppLog.Error("K7 Mix could not start", ex);
            throw;
        }
        finally
        {
            SingleInstance.Release();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new FontManagerOptions { DefaultFamilyName = "avares://K7Mix/Assets/Fonts#Roboto" })
            .LogToTrace();
}
