using System.Text.Json;

namespace K7Mix.Core;

/// <summary>
/// The cassette-title faces. The four bundled ones (Cousine, Roboto, Rock Salt,
/// Fixedsys) ship with the app; their identifiers are shared with RunningWild and
/// with the Typst insert so a title looks the same in the app, the game and print.
/// After them come the standard Logarktos faces from <c>L:\Sync\logarktos-fonts.json</c>,
/// installed for the user by <c>Install-LogarktosFonts.ps1</c>: the app and the
/// insert set them by family name, and a game that does not know one shows Roboto.
/// </summary>
public sealed record CassetteFont(
    string Id,
    string DisplayName,
    string FileName,
    string FamilyName,
    string LicenseNotice,
    string? ScreenFamily = null,
    bool Bundled = true)
{
    public const string CatalogPath = @"L:\Sync\logarktos-fonts.json";

    public static readonly CassetteFont Cousine = new(
        "cousine",
        "Cousine",
        "Cousine-Regular.ttf",
        "Cousine",
        "Cousine is licensed under the SIL Open Font License, Version 1.1.");

    public static readonly CassetteFont Roboto = new(
        "roboto",
        "Roboto",
        "Roboto-Regular.ttf",
        "Roboto",
        "Roboto is licensed under the SIL Open Font License, Version 1.1.");

    public static readonly CassetteFont RockSalt = new(
        "rock-salt",
        "Rock Salt",
        "RockSalt-Regular.ttf",
        "Rock Salt",
        "Rock Salt is licensed under the Apache License, Version 2.0.");

    public static readonly CassetteFont Fixedsys = new(
        "fixedsys",
        "Fixedsys",
        "FSEX302.ttf",
        "Fixedsys Excelsior 3.01",
        "Fixedsys Excelsior is dedicated to the public domain (CC0).");

    /// <summary>The faces bundled with the app and known to the games.</summary>
    public static readonly IReadOnlyList<CassetteFont> BundledFaces = [Cousine, Roboto, RockSalt, Fixedsys];

    private static IReadOnlyList<CassetteFont>? _all;

    /// <summary>The bundled faces, then every standard Logarktos face not already among them.</summary>
    public static IReadOnlyList<CassetteFont> All => _all ??= [.. BundledFaces, .. LogarktosFaces(ReadCatalog(CatalogPath))];

    public static CassetteFont Default => Roboto;

    /// <summary>A face set by family name: a standard Logarktos face, or one a package names.</summary>
    public static CassetteFont FromFamily(string family, string? screenFamily = null)
    {
        var name = family.Trim();
        return new CassetteFont(
            Slug(name),
            name,
            "",
            name,
            $"{name} is a standard Logarktos font, installed on this computer from L:\\Library\\Environment\\Fonts; its licence is in that folder.",
            string.IsNullOrWhiteSpace(screenFamily) ? null : screenFamily.Trim(),
            Bundled: false);
    }

    public static CassetteFont Parse(string? text) => Parse(text, All);

    internal static CassetteFont Parse(string? text, IReadOnlyList<CassetteFont> faces)
    {
        if (string.IsNullOrWhiteSpace(text)) return Default;
        var key = Normalize(text);
        foreach (var font in faces)
        {
            if (Normalize(font.Id) == key) return font;
            if (Normalize(font.DisplayName) == key) return font;
            if (Normalize(font.FamilyName) == key) return font;
        }

        throw new MixValidationException(
            $"Font '{text}' is not one of Cousine, Roboto, Rock Salt, Fixedsys or a standard Logarktos font.");
    }

    public static bool TryParse(string? text, out CassetteFont font)
    {
        try
        {
            font = Parse(text);
            return true;
        }
        catch (MixValidationException)
        {
            font = Default;
            return false;
        }
    }

    /// <summary>
    /// The text faces of the Logarktos catalog, each family once, minus those the
    /// bundled four already cover. Empty when the catalog is missing or unreadable.
    /// </summary>
    internal static IReadOnlyList<CassetteFont> LogarktosFaces(string? catalogJson)
    {
        var faces = new List<CassetteFont>();
        if (string.IsNullOrWhiteSpace(catalogJson)) return faces;
        try
        {
            using var document = JsonDocument.Parse(catalogJson);
            if (!document.RootElement.TryGetProperty("families", out var list)) return faces;
            var seen = new HashSet<string>(BundledFaces.Select(f => Normalize(f.DisplayName)));
            foreach (var f in BundledFaces) seen.Add(Normalize(f.FamilyName));
            seen.Add(Normalize("Fixedsys Excelsior"));
            foreach (var item in list.EnumerateArray())
            {
                var family = Text(item, "family");
                if (family.Length == 0 || Text(item, "role") == "symbol") continue;
                if (!seen.Add(Normalize(family))) continue;
                var screen = Text(item, "windowsFamily");
                faces.Add(FromFamily(family, screen.Length > 0 ? screen : null));
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return faces;
    }

    private static string? ReadCatalog(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Text(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? ""
            : "";

    private static string Slug(string family) =>
        string.Join("-", family.ToLowerInvariant().Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries));

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "-");
}
