namespace K7Mix.Core;

/// <summary>
/// The four cassette-title faces. Identifiers are shared with RunningWild and
/// with the Typst insert so a title looks the same in the app, the game and print.
/// </summary>
public sealed record CassetteFont(
    string Id,
    string DisplayName,
    string FileName,
    string FamilyName,
    string LicenseNotice)
{
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

    public static readonly IReadOnlyList<CassetteFont> All = [Cousine, Roboto, RockSalt, Fixedsys];

    public static CassetteFont Default => Roboto;

    public static CassetteFont Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Default;
        var key = Normalize(text);
        foreach (var font in All)
        {
            if (Normalize(font.Id) == key) return font;
            if (Normalize(font.DisplayName) == key) return font;
            if (Normalize(font.FamilyName) == key) return font;
        }

        throw new MixValidationException(
            $"Font '{text}' is not one of Cousine, Roboto, Rock Salt or Fixedsys.");
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

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "-");
}
