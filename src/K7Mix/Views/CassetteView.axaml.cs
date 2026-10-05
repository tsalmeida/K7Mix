using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using K7Mix.Core;

namespace K7Mix.Views;

public partial class CassetteView : UserControl
{
    public CassetteView()
    {
        InitializeComponent();
        Apply(new MixDocument());
    }

    public void Apply(MixDocument mix)
    {
        Plate.Source = CassetteRenderer.Render(mix);

        // Match the panel to the cropped plate so Stretch="Uniform" fills it exactly.
        // Otherwise the image is letterboxed inside the panel and the title, which is
        // placed from the panel's own size, drifts off the printed label.
        PlateArea.Height = 270;
        PlateArea.Width = 270 * CassetteRenderer.AspectRatio();

        var text = mix.Appearance.TextColor;
        TitleText.Foreground = new SolidColorBrush(Color.FromRgb(text.R, text.G, text.B));
        TitleText.Text = string.IsNullOrWhiteSpace(mix.Title) ? "Untitled mix" : mix.Title;
        try
        {
            TitleText.FontFamily = FontFor(mix.Appearance.Font);
        }
        catch (Exception)
        {
            // Unresolvable embedded font: fall back to the UI default so the app
            // never crashes during layout over a bad font name.
            TitleText.FontFamily = new FontFamily("avares://K7Mix/Assets/Fonts#Roboto");
        }
        // Pin the text box to the printed label and let it centre itself inside.
        // The old margin nudged the top down by 4px and left the block stretched,
        // so the text hung from the top of the sticker instead of sitting on it.
        var label = CassetteRenderer.LabelBounds();
        var w = PlateArea.Width;
        var h = PlateArea.Height;
        TitleText.Margin = new Thickness(
            label.X * w,
            label.Y * h,
            w - (label.X + label.Width) * w,
            h - (label.Y + label.Height) * h);
    }

    /// <summary>
    /// A bundled face comes from the app's assets; a standard Logarktos face is
    /// the one installed on this computer, with Roboto behind it when it is not.
    /// </summary>
    internal static FontFamily FontFor(CassetteFont font) => font.Id switch
    {
        "cousine" => new FontFamily("avares://K7Mix/Assets/TitleFonts#Cousine"),
        "rock-salt" => new FontFamily("avares://K7Mix/Assets/TitleFonts#Rock Salt"),
        "fixedsys" => new FontFamily("avares://K7Mix/Assets/TitleFonts#Fixedsys Excelsior"),
        _ when !font.Bundled => new FontFamily($"{font.ScreenFamily ?? font.FamilyName}, avares://K7Mix/Assets/Fonts#Roboto"),
        _ => new FontFamily("avares://K7Mix/Assets/Fonts#Roboto"),
    };
}
