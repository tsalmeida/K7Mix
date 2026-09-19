using SkiaSharp;

namespace K7Mix.Core;

/// <summary>
/// Centre-cropping for jewel-case artwork. The user is never asked to frame a
/// picture: whatever they supply is trimmed on its two longer sides until it has
/// the proportion of the panel it prints on, keeping the middle. Supplying an
/// image already at that proportion is how someone takes control of the framing,
/// because then there is nothing to trim.
/// </summary>
public static class ArtworkCrop
{
    /// <summary>
    /// The part of a <paramref name="width"/> × <paramref name="height"/> picture
    /// that survives a crop to <paramref name="targetAspect"/> (width over height).
    /// Too wide loses the left and right edges, too tall the top and bottom, and an
    /// image already at the target keeps all of itself.
    /// </summary>
    public static Rectangle CenterCrop(int width, int height, double targetAspect)
    {
        if (width <= 0 || height <= 0 || targetAspect <= 0 || double.IsNaN(targetAspect))
            return new Rectangle(0, 0, Math.Max(width, 0), Math.Max(height, 0));

        var current = width / (double)height;
        if (current > targetAspect)
        {
            // Too wide: keep the full height and trim the sides.
            var cropped = (int)Math.Round(height * targetAspect);
            if (cropped < 1) cropped = 1;
            if (cropped > width) cropped = width;
            return new Rectangle((width - cropped) / 2, 0, cropped, height);
        }

        // Too tall (or exact): keep the full width and trim top and bottom.
        var croppedHeight = (int)Math.Round(width / targetAspect);
        if (croppedHeight < 1) croppedHeight = 1;
        if (croppedHeight > height) croppedHeight = height;
        return new Rectangle(0, (height - croppedHeight) / 2, width, croppedHeight);
    }

    /// <summary>
    /// Reads an image, centre-crops it to <paramref name="targetAspect"/> and
    /// returns it as PNG. Throws a validation error naming the file if it cannot be
    /// decoded, because a silently skipped cover is worse than a refused save.
    /// </summary>
    public static byte[] CropToPng(string path, double targetAspect)
    {
        using var source = SKBitmap.Decode(path)
            ?? throw new MixValidationException(
                $"Artwork could not be read as an image: {Path.GetFileName(path)}");

        var crop = CenterCrop(source.Width, source.Height, targetAspect);
        using var cropped = new SKBitmap(crop.Width, crop.Height, source.ColorType, source.AlphaType);
        if (!source.ExtractSubset(cropped, new SKRectI(crop.X, crop.Y, crop.Right, crop.Bottom)))
            throw new MixValidationException(
                $"Artwork could not be cropped: {Path.GetFileName(path)}");

        using var image = SKImage.FromBitmap(cropped);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A pixel rectangle, so Core does not need a UI framework for one.</summary>
    public readonly record struct Rectangle(int X, int Y, int Width, int Height)
    {
        public int Right => X + Width;
        public int Bottom => Y + Height;
    }
}
