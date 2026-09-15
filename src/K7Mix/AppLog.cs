using System.Text;

namespace K7Mix;

internal static class AppLog
{
    private static readonly object Gate = new();
    private static string _path = System.IO.Path.Combine(AppContext.BaseDirectory, "K7Mix.log");

    public static string LogPath => _path;

    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            File.AppendAllText(_path, string.Empty);
        }
        catch
        {
            var folder = AppPaths.UserDataDirectory;
            Directory.CreateDirectory(folder);
            _path = System.IO.Path.Combine(folder, "K7Mix.log");
        }

        var version = typeof(AppLog).Assembly.GetName().Version?.ToString() ?? "unknown";
        Info($"K7 Mix starting; version {version}; OS {Environment.OSVersion}; " +
             $"64-bit process: {Environment.Is64BitProcess}");
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string context, Exception exception) =>
        Write("ERROR", $"{context}{Environment.NewLine}{exception}");

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{Environment.NewLine}";
            lock (Gate) File.AppendAllText(_path, line, Encoding.UTF8);
        }
        catch
        {
            // Logging must not stop the mixer.
        }
    }
}

internal static class AppPaths
{
    public static string UserDataDirectory => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "K7Mix");
}
