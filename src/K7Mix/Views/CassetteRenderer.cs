using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using K7Mix.Core;

namespace K7Mix.Views;

/// <summary>
/// Tints Running Wild's cassette plate the same way the game does: split on
/// luminance, normalise the dark shell, colour-mod the cream label.
/// </summary>
internal static class CassetteRenderer
{
    private const int SplitLuma = 130;
    private const int SplitFloor = 42;
    private static WriteableBitmap? _source;

    public static Bitmap Render(MixDocument mix)
    {
        var src = Source();
        var w = src.PixelSize.Width;
        var h = src.PixelSize.Height;
        var pixels = ReadPixels(src);

        var tape = mix.Appearance.TapeColor;
        var sticker = mix.Appearance.StickerColor;
        var shellRef = ReferenceTone(pixels, w, h, bright: false);
        var dest = new WriteableBitmap(src.PixelSize, src.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var fb = dest.Lock())
        {
            var row = new byte[w * 4];
            for (var y = 0; y < h; y++)
            {
                Buffer.BlockCopy(pixels, y * w * 4, row, 0, row.Length);
                for (var x = 0; x < w; x++)
                {
                    var i = x * 4;
                    var b = row[i];
                    var g = row[i + 1];
                    var r = row[i + 2];
                    var a = row[i + 3];
                    if (a < 128)
                    {
                        row[i] = 0; row[i + 1] = 0; row[i + 2] = 0; row[i + 3] = 0;
                        continue;
                    }
                    var luma = (r * 77 + g * 151 + b * 28) >> 8;
                    if (luma < SplitLuma)
                    {
                        var grey = Math.Clamp(luma * 255 / Math.Max(1, shellRef), SplitFloor, 255);
                        row[i] = (byte)(grey * tape.B / 255);
                        row[i + 1] = (byte)(grey * tape.G / 255);
                        row[i + 2] = (byte)(grey * tape.R / 255);
                    }
                    else
                    {
                        row[i] = (byte)(b * sticker.B / 255);
                        row[i + 1] = (byte)(g * sticker.G / 255);
                        row[i + 2] = (byte)(r * sticker.R / 255);
                    }
                    row[i + 3] = a;
                }
                Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, row.Length);
            }
        }
        return dest;
    }

    /// <summary>
    /// Finds the printed sticker, not just any bright pixel: the corner mounting
    /// screws are bright too, but they are small and sparse, while the sticker is
    /// a wide, solidly filled band. Only rows that are mostly filled across a wide
    /// span count as the label.
    /// </summary>
    public static Rect LabelBounds()
    {
        var src = Source();
        var w = src.PixelSize.Width;
        var h = src.PixelSize.Height;
        var pixels = ReadPixels(src);
        var minX = w; var minY = h; var maxX = 0; var maxY = 0;
        var top = h * 2 / 5;
        for (var y = 0; y < top; y++)
        {
            var rowMinX = w; var rowMaxX = -1; var count = 0;
            for (var x = 0; x < w; x++)
            {
                var i = (y * w + x) * 4;
                var a = pixels[i + 3];
                if (a < 128) continue;
                var luma = (pixels[i + 2] * 77 + pixels[i + 1] * 151 + pixels[i] * 28) >> 8;
                if (luma < SplitLuma) continue;
                count++;
                if (x < rowMinX) rowMinX = x;
                if (x > rowMaxX) rowMaxX = x;
            }
            if (rowMaxX < rowMinX) continue;
            var span = rowMaxX - rowMinX + 1;
            if (span < w * 0.3 || count < span * 0.85) continue;
            if (rowMinX < minX) minX = rowMinX;
            if (rowMaxX > maxX) maxX = rowMaxX;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        if (maxX <= minX || maxY <= minY)
            return new Rect(0.18, 0.10, 0.64, 0.18);
        return new Rect(minX / (double)w, minY / (double)h,
            (maxX - minX) / (double)w, (maxY - minY) / (double)h);
    }

    private static int ReferenceTone(byte[] pixels, int w, int h, bool bright)
    {
        var hist = new int[256];
        var total = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var a = pixels[i + 3];
            if (a < 128) continue;
            var luma = (pixels[i + 2] * 77 + pixels[i + 1] * 151 + pixels[i] * 28) >> 8;
            if (bright ? luma < SplitLuma : luma >= SplitLuma) continue;
            hist[luma]++;
            total++;
        }
        if (total == 0) return 180;
        var run = 0;
        var target = (int)(total * 0.97);
        for (var i = 0; i < 256; i++)
        {
            run += hist[i];
            if (run >= target) return Math.Max(i, 1);
        }
        return 180;
    }

    private static byte[] ReadPixels(WriteableBitmap src)
    {
        var w = src.PixelSize.Width;
        var h = src.PixelSize.Height;
        var pixels = new byte[w * h * 4];
        using var fb = src.Lock();
        for (var y = 0; y < h; y++)
            Marshal.Copy(fb.Address + y * fb.RowBytes, pixels, y * w * 4, w * 4);
        return pixels;
    }

    private static WriteableBitmap Source()
    {
        if (_source is not null) return _source;
        using var stream = AssetLoader.Open(new Uri("avares://K7Mix/Assets/Cassette/TT-Cassette-Tape.png"));
        var bmp = new Bitmap(stream);
        _source = new WriteableBitmap(bmp.PixelSize, bmp.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var fb = _source.Lock())
        {
            bmp.CopyPixels(new PixelRect(0, 0, bmp.PixelSize.Width, bmp.PixelSize.Height),
                fb.Address, fb.RowBytes * bmp.PixelSize.Height, fb.RowBytes);
        }
        return _source;
    }
}
