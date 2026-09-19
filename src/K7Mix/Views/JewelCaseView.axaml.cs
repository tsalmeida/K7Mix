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
        var stickerBrush = new SolidColorBrush(sticker);
        var inkBrush = new SolidColorBrush(ink);

        // The card is printed paper, so the faces take the sticker colour and only
        // the spine wears the tape colour. Putting the tape colour on a face used to
        // leave dark ink on a dark panel.
        FrontPanel.Background = stickerBrush;
        BackPanel.Background = stickerBrush;
        Spine.Background = new SolidColorBrush(tape);
        var flap = new SolidColorBrush(Lighten(tape, 0.7));
        TopFlap.Background = flap;
        BottomFlap.Background = flap;

        var title = string.IsNullOrWhiteSpace(mix.Title) ? "Untitled mix" : mix.Title;
        FrontTitle.Text = title;
        FrontTitle.Foreground = inkBrush;
        SpineText.Text = title;
        var spineInk = mix.Appearance.TapeColor.ContrastingInk();
        SpineText.Foreground = new SolidColorBrush(Color.FromRgb(spineInk.R, spineInk.G, spineInk.B));
        try
        {
            FrontTitle.FontFamily = CassetteView.FontFor(mix.Appearance.Font);
            SpineText.FontFamily = CassetteView.FontFor(mix.Appearance.Font);
        }
        catch (Exception)
        {
            // Unresolvable embedded font: keep whatever the controls already have.
        }

        var back = mix.Description;
        if (string.IsNullOrWhiteSpace(back)) back = mix.License;
        if (string.IsNullOrWhiteSpace(back)) back = mix.Tracks.Count + " track(s)";
        BackText.Text = back;
        BackText.Foreground = inkBrush;

        SetArt(FrontArt, FrontTitle, mix.Appearance.FrontArtworkPath);
        SetArt(BackArt, BackText, mix.Appearance.BackArtworkPath);
    }

    /// <summary>
    /// The artwork fills its panel and the panel clips, which centre-crops it to
    /// that panel's proportion — the same crop the package and the PDF apply.
    /// </summary>
    private static void SetArt(Image image, Control fallback, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                image.Source = new Bitmap(path);
                fallback.IsVisible = false;
            }
            catch (Exception)
            {
                // An unreadable file is reported when the mix is saved; the preview
                // just falls back to the printed text.
                image.Source = null;
                fallback.IsVisible = true;
            }
        }
        else
        {
            image.Source = null;
            fallback.IsVisible = true;
        }
    }

    private static Color Lighten(Color c, double amount) => Color.FromRgb(
        (byte)(c.R + (255 - c.R) * amount),
        (byte)(c.G + (255 - c.G) * amount),
        (byte)(c.B + (255 - c.B) * amount));
}
