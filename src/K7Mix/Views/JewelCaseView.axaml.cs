using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using K7Mix.Core;

namespace K7Mix.Views;

public partial class JewelCaseView : UserControl
{
    public JewelCaseView()
    {
        InitializeComponent();
        Apply(new MixDocument());
    }

    public void Apply(MixDocument mix)
    {
        var tape = Color.FromRgb(mix.Appearance.TapeColor.R, mix.Appearance.TapeColor.G, mix.Appearance.TapeColor.B);
        var sticker = Color.FromRgb(mix.Appearance.StickerColor.R, mix.Appearance.StickerColor.G, mix.Appearance.StickerColor.B);
        var ink = Color.FromRgb(mix.Appearance.TextColor.R, mix.Appearance.TextColor.G, mix.Appearance.TextColor.B);
        var tapeBrush = new SolidColorBrush(tape);
        var stickerBrush = new SolidColorBrush(sticker);
        var inkBrush = new SolidColorBrush(ink);

        Spine.Background = tapeBrush;
        SpineText.Text = mix.Title;
        SpineText.Foreground = inkBrush;
        SpineText.FontFamily = CassetteView.FontFor(mix.Appearance.Font);

        FrontPanel.Background = stickerBrush;
        FrontTitle.Text = string.IsNullOrWhiteSpace(mix.Title) ? "Untitled mix" : mix.Title;
        FrontTitle.Foreground = inkBrush;
        FrontTitle.FontFamily = CassetteView.FontFor(mix.Appearance.Font);
        FrontHint.Foreground = inkBrush;

        BackPanel.Background = tapeBrush;
        BackText.Foreground = inkBrush;
        var back = mix.Description;
        if (string.IsNullOrWhiteSpace(back))
            back = mix.License;
        if (string.IsNullOrWhiteSpace(back))
            back = mix.Tracks.Count + " track(s)";
        BackText.Text = back;

        SetArt(FrontArt, FrontTitle, mix.Appearance.FrontArtworkPath);
        SetArt(BackArt, BackText, mix.Appearance.BackArtworkPath);
        FrontHint.IsVisible = string.IsNullOrWhiteSpace(mix.Appearance.FrontArtworkPath);
    }

    private static void SetArt(Image image, Control fallback, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            image.Source = new Bitmap(path);
            fallback.IsVisible = false;
        }
        else
        {
            image.Source = null;
            fallback.IsVisible = true;
        }
    }
}
