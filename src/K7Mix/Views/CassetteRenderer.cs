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

    /// <summary>
    /// The artwork ships as an opaque rectangle with a black frame printed around a
    /// shell that has rounded corners. Anything at or below this luminance that can
    /// be reached from the edge of the image is that frame.
    /// </summary>
    private const int FrameLuma = 24;

    private static Plate? _plate;

    /// <summary>
    /// The artwork with its printed frame cleared and cropped away, held as raw
    /// BGRA. Both the tint and the label search work from this, so the normalised
    /// label bounds stay in step with the bitmap that is actually drawn.
    /// </summary>
    private sealed record Plate(byte[] Bgra, int Width, int Height, Vector Dpi);

    public static Bitmap Render(MixDocument mix)
    {
        var plate = Source();
        var w = plate.Width;
        var h = plate.Height;

        var tape = mix.Appearance.TapeColor;
        var sticker = mix.Appearance.StickerColor;
        var shellRef = ReferenceTone(plate.Bgra);
        var dest = new WriteableBitmap(
            new PixelSize(w, h), plate.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var fb = dest.Lock())
        {
            var row = new byte[w * 4];
            for (var y = 0; y < h; y++)
            {
                Buffer.BlockCopy(plate.Bgra, y * w * 4, row, 0, row.Length);
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

    /// <summary>The plate's width divided by its height, once cropped.</summary>
    public static double AspectRatio()
    {
        var plate = Source();
        return plate.Height == 0 ? 1 : plate.Width / (double)plate.Height;
    }

    /// <summary>
    /// Finds the printed sticker, not just any bright pixel: the corner mounting
    /// screws are bright too, but they are small and sparse, while the sticker is
    /// a wide, solidly filled band. Only rows that are mostly filled across a wide
    /// span count as the label.
    /// </summary>
    public static Rect LabelBounds()
    {
        var plate = Source();
        var w = plate.Width;
        var h = plate.Height;
        var pixels = plate.Bgra;
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

    private static int ReferenceTone(byte[] pixels)
    {
        var hist = new int[256];
        var total = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var a = pixels[i + 3];
            if (a < 128) continue;
            var luma = (pixels[i + 2] * 77 + pixels[i + 1] * 151 + pixels[i] * 28) >> 8;
            if (luma >= SplitLuma) continue;
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

    private static Plate Source()
    {
        if (_plate is not null) return _plate;
        using var stream = AssetLoader.Open(new Uri("avares://K7Mix/Assets/Cassette/TT-Cassette-Tape.png"));
        var bmp = new Bitmap(stream);
        var w = bmp.PixelSize.Width;
        var h = bmp.PixelSize.Height;
        var pixels = new byte[w * h * 4];
        using (var buffer = new WriteableBitmap(
                   bmp.PixelSize, bmp.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul))
        {
            using var fb = buffer.Lock();
            bmp.CopyPixels(new PixelRect(0, 0, w, h), fb.Address, fb.RowBytes * h, fb.RowBytes);
            for (var y = 0; y < h; y++)
                Marshal.Copy(fb.Address + y * fb.RowBytes, pixels, y * w * 4, w * 4);
        }
        ClearFrame(pixels, w, h);
        _plate = Crop(pixels, w, h, bmp.Dpi);
        return _plate;
    }

    /// <summary>
    /// Clears the printed frame so the shell's own rounded corners become the edge
    /// of the image, by walking inwards from each of the four sides and stopping at
    /// the first pixel that is not frame-black.
    ///
    /// A flood fill is the obvious tool and the wrong one here: the bevel lines
    /// under the shell are as black as the frame and touch it, so a fill runs down
    /// them and eats two diagonal slits out of the cassette. A row scan stops at the
    /// shell instead, and rows that are frame all the way across clear whole, which
    /// is what takes off the top and bottom margins. Scanning the columns as well
    /// would be redundant and would crawl up those same bevel lines where they reach
    /// the bottom edge, so the rows do it alone. Reading the artwork this way also
    /// beats guessing a corner radius.
    /// </summary>
    private static void ClearFrame(byte[] pixels, int w, int h)
    {
        for (var y = 0; y < h; y++)
        {
            var row = y * w;
            for (var x = 0; x < w && IsFrame(pixels, row + x); x++) Clear(pixels, row + x);
            for (var x = w - 1; x >= 0 && IsFrame(pixels, row + x); x--) Clear(pixels, row + x);
        }
    }

    /// <summary>
    /// A cleared pixel reads as frame-black again, which is harmless: a scan only
    /// ever re-crosses pixels it has already cleared on its way in.
    /// </summary>
    private static bool IsFrame(byte[] pixels, int p)
    {
        var i = p * 4;
        return ((pixels[i + 2] * 77 + pixels[i + 1] * 151 + pixels[i] * 28) >> 8) <= FrameLuma;
    }

    private static void Clear(byte[] pixels, int p)
    {
        var i = p * 4;
        pixels[i] = 0; pixels[i + 1] = 0; pixels[i + 2] = 0; pixels[i + 3] = 0;
    }

    /// <summary>
    /// Trims the fully transparent margin left by <see cref="ClearFrame"/> so the
    /// shell fills the bitmap and the control does not letterbox around empty rows.
    /// </summary>
    private static Plate Crop(byte[] pixels, int w, int h, Vector dpi)
    {
        var minX = w; var minY = h; var maxX = -1; var maxY = -1;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (pixels[(y * w + x) * 4 + 3] == 0) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        if (maxX < minX || maxY < minY) return new Plate(pixels, w, h, dpi);

        var cw = maxX - minX + 1;
        var ch = maxY - minY + 1;
        var cropped = new byte[cw * ch * 4];
        for (var y = 0; y < ch; y++)
            Buffer.BlockCopy(pixels, ((y + minY) * w + minX) * 4, cropped, y * cw * 4, cw * 4);
        return new Plate(cropped, cw, ch, dpi);
    }
}
