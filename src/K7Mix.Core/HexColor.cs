using System.Globalization;
using System.Text.RegularExpressions;

namespace K7Mix.Core;

public readonly record struct HexColor(byte R, byte G, byte B)
{
    /// <summary>
    /// Black or white, whichever reads on this colour. Used where text sits on a
    /// panel the user chose the colour of, such as the J-card spine, so their ink
    /// colour cannot make the title vanish into the tape colour.
    /// </summary>
    public HexColor ContrastingInk() =>
        (R * 77 + G * 151 + B * 28) >> 8 < 130 ? new HexColor(255, 255, 255) : new HexColor(0, 0, 0);

    private static readonly Regex Pattern = new(
        @"^#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public override string ToString() => ToHex();

    public static bool IsValid(string? text) =>
        !string.IsNullOrWhiteSpace(text) && Pattern.IsMatch(text.Trim());

    public static bool TryParse(string? text, out HexColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var value = text.Trim();
        if (!Pattern.IsMatch(value)) return false;
        var hex = value[1..];
        if (hex.Length == 3)
        {
            color = new HexColor(
                (byte)(Uri.FromHex(hex[0]) * 17),
                (byte)(Uri.FromHex(hex[1]) * 17),
                (byte)(Uri.FromHex(hex[2]) * 17));
            return true;
        }

        color = new HexColor(
            byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        return true;
    }

    public static HexColor Parse(string text, string fieldName)
    {
        if (TryParse(text, out var color)) return color;
        throw new MixValidationException(
            $"{fieldName} must be a hexadecimal colour such as #000000 or #fff, not '{text}'.");
    }
}
