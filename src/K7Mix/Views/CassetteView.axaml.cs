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
        var tape = mix.Appearance.TapeColor;
        var sticker = mix.Appearance.StickerColor;
        var text = mix.Appearance.TextColor;
        Shell.Background = new SolidColorBrush(Color.FromRgb(tape.R, tape.G, tape.B));
        Sticker.Background = new SolidColorBrush(Color.FromRgb(sticker.R, sticker.G, sticker.B));
        TitleText.Foreground = new SolidColorBrush(Color.FromRgb(text.R, text.G, text.B));
        TitleText.Text = string.IsNullOrWhiteSpace(mix.Title) ? "Untitled mix" : mix.Title;
        TitleText.FontFamily = FontFor(mix.Appearance.Font);
    }

    private static FontFamily FontFor(CassetteFont font) => font.Id switch
    {
        "cousine" => new FontFamily("avares://K7Mix/Assets/TitleFonts#Cousine"),
        "rock-salt" => new FontFamily("avares://K7Mix/Assets/TitleFonts#Rock Salt"),
        "fixedsys" => new FontFamily("avares://K7Mix/Assets/TitleFonts#Fixedsys Excelsior 3.01"),
        _ => new FontFamily("avares://K7Mix/Assets/Fonts#Roboto"),
    };
}
