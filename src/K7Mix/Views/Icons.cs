namespace K7Mix.Views;

/// <summary>
/// Font Awesome 6 Pro Solid glyphs, as the codepoints the webfont stylesheet
/// gives for each icon name. The face ships in Assets/Icons and is referenced in
/// XAML through the <c>K7Icons</c> resource; the constants here are for the few
/// glyphs that have to change at runtime.
///
/// Only the names are meaningful: a bare codepoint in the markup says nothing
/// about which icon it is, so anything the code touches gets a name here.
/// </summary>
internal static class Icons
{
    /// <summary>fa-play</summary>
    public const string Play = "";

    /// <summary>fa-pause</summary>
    public const string Pause = "";
}
