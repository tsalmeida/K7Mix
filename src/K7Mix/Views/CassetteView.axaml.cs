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
        var text = mix.Appearance.TextColor;
        TitleText.Foreground = new SolidColorBrush(Color.FromRgb(text.R, text.G, text.B));
        TitleText.Text = string.IsNullOrWhiteSpace(mix.Title) ? "Untitled mix" : mix.Title;
        TitleText.FontFamily = FontFor(mix.Appearance.Font);
        var label = CassetteRenderer.LabelBounds();
        TitleText.Margin = new Thickness(
            label.X * 480,
            label.Y * 270 + 4,
            480 - (label.X + label.Width) * 480,
            270 - (label.Y + label.Height) * 270);
    }

    internal static FontFamily FontFor(CassetteFont font) => font.Id switch
    {
        "cousine" => new FontFamily("avares://K7Mix/Assets/TitleFonts#Cousine"),
        "rock-salt" => new FontFamily("avares://K7Mix/Assets/TitleFonts#Rock Salt"),
        "fixedsys" => new FontFamily("avares://K7Mix/Assets/TitleFonts#Fixedsys Excelsior 3.01"),
        _ => new FontFamily("avares://K7Mix/Assets/Fonts#Roboto"),
    };
}
