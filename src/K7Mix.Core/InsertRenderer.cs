using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace K7Mix.Core;

public static class InsertRenderer
{
    public static string BuildTypstData(MixDocument mix, string? frontImage, string? backImage)
    {
        var payload = new Dictionary<string, object?>
        {
            ["title"] = mix.Title,
            ["description"] = mix.Description,
            ["sourceUrl"] = mix.SourceUrl,
            ["license"] = mix.License,
            ["sideA"] = mix.EffectiveSideALabel,
            ["sideB"] = mix.EffectiveSideBLabel,
            ["font"] = mix.Appearance.Font.Id,
            ["fontFamily"] = mix.Appearance.Font.FamilyName,
            ["textColor"] = mix.Appearance.TextColor.ToHex(),
            ["tapeColor"] = mix.Appearance.TapeColor.ToHex(),
            ["stickerColor"] = mix.Appearance.StickerColor.ToHex(),
            ["spineInk"] = mix.Appearance.TapeColor.ContrastingInk().ToHex(),
            ["front"] = frontImage,
            ["back"] = backImage,
            ["notice"] = LegalNotice.PackagingDoesNotGrantPermission,
            ["tracks"] = mix.Tracks.Select((t, i) => new Dictionary<string, object?>
            {
                ["n"] = i + 1,
                ["title"] = t.Title,
                ["codename"] = t.Codename,
                ["side"] = mix.SideOf(i) == 0 ? "A" : "B",
                ["duration"] = MixValidator.FormatMinutes(t.Duration),
            }).ToList(),
        };
        return JsonSerializer.Serialize(payload);
    }

    public static void Compile(string templatePath, string dataJson, string outputPdf, string fontsDirectory)
    {
        var typst = FindTypst()
            ?? throw new MixValidationException("typst was not found on PATH.");
        var work = Path.Combine(Path.GetTempPath(), "k7mix-insert-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson)
                          ?? new Dictionary<string, JsonElement>();
            foreach (var key in new[] { "front", "back" })
            {
                if (!payload.TryGetValue(key, out var node) || node.ValueKind != JsonValueKind.String)
                    continue;
                var imagePath = node.GetString();
                if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                {
                    payload.Remove(key);
                    continue;
                }
                var destName = key + Path.GetExtension(imagePath);
                File.Copy(imagePath, Path.Combine(work, destName), overwrite: true);
                payload[key] = JsonSerializer.SerializeToElement(destName);
            }
            var dataPath = Path.Combine(work, "mix.json");
            File.WriteAllText(dataPath, JsonSerializer.Serialize(payload), new UTF8Encoding(false));
            File.Copy(templatePath, Path.Combine(work, "insert.typ"), overwrite: true);
            if (Directory.Exists(fontsDirectory))
            {
                foreach (var font in Directory.EnumerateFiles(fontsDirectory, "*.ttf"))
                    File.Copy(font, Path.Combine(work, Path.GetFileName(font)), overwrite: true);
            }

            var args = $"compile --root \"{work}\" --font-path \"{work}\" insert.typ \"{outputPdf}\"";
            var psi = new ProcessStartInfo
            {
                FileName = typst,
                Arguments = args,
                WorkingDirectory = work,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi)
                ?? throw new MixValidationException("typst could not be started.");
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(60_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                throw new MixValidationException("typst timed out.");
            }
            if (process.ExitCode != 0 || !File.Exists(outputPdf))
                throw new MixValidationException("typst could not compile the insert. " + error.Trim());
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch { /* temp */ }
        }
    }

    public static string? FindTypst()
    {
        foreach (var name in new[] { "typst.exe", "typst" })
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                var candidate = Path.Combine(dir.Trim(), name);
                if (File.Exists(candidate)) return candidate;
            }
        }
        var winget = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\WinGet\Packages");
        if (!Directory.Exists(winget)) return null;
        return Directory.EnumerateFiles(winget, "typst.exe", SearchOption.AllDirectories).FirstOrDefault();
    }
}
