using System.Reflection;

namespace K7Mix;

internal static class TemplateSeed
{
    public static string Ensure()
    {
        var folder = Path.Combine(AppPaths.UserDataDirectory, "Templates");
        Directory.CreateDirectory(folder);
        var fonts = Path.Combine(folder, "fonts");
        Directory.CreateDirectory(fonts);
        var assembly = typeof(TemplateSeed).Assembly;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            string dest;
            if (name.StartsWith("K7Mix.Templates.", StringComparison.Ordinal))
                dest = Path.Combine(folder, name["K7Mix.Templates.".Length..]);
            else if (name.StartsWith("K7Mix.TemplateFonts.", StringComparison.Ordinal))
                dest = Path.Combine(fonts, name["K7Mix.TemplateFonts.".Length..]);
            else
                continue;
            if (File.Exists(dest) && !dest.EndsWith(".typ", StringComparison.OrdinalIgnoreCase)) continue;
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;
            using var file = File.Create(dest);
            stream.CopyTo(file);
        }
        return folder;
    }
}
