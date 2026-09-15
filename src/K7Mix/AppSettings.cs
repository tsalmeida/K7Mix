using System.Text;
using System.Text.Json;

namespace K7Mix;

internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string? LastFolder { get; set; }
    public List<string> RecentFiles { get; set; } = [];

    public static string SettingsPath => Path.Combine(AppPaths.UserDataDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            AppLog.Error("Loading settings", ex);
            return new AppSettings();
        }
    }

    public void Remember(string path)
    {
        LastFolder = Path.GetDirectoryName(path);
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > 8) RecentFiles.RemoveRange(8, RecentFiles.Count - 8);
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.UserDataDirectory);
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            AppLog.Error("Saving settings", ex);
        }
    }
}
