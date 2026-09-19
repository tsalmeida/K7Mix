using K7Mix.Core;

namespace K7Mix.Tests;

public class ArtworkCropTests
{
    [Fact]
    public void A_picture_that_is_too_wide_loses_its_sides_evenly()
    {
        // 2:1 into 1:1 keeps the full height and half the width, centred.
        var crop = ArtworkCrop.CenterCrop(400, 200, 1.0);
        Assert.Equal(200, crop.Width);
        Assert.Equal(200, crop.Height);
        Assert.Equal(100, crop.X);
        Assert.Equal(0, crop.Y);
    }

    [Fact]
    public void A_picture_that_is_too_tall_loses_its_top_and_bottom_evenly()
    {
        var crop = ArtworkCrop.CenterCrop(200, 400, 1.0);
        Assert.Equal(200, crop.Width);
        Assert.Equal(200, crop.Height);
        Assert.Equal(0, crop.X);
        Assert.Equal(100, crop.Y);
    }

    [Fact]
    public void A_square_becomes_the_front_panel_by_losing_the_top_and_bottom()
    {
        // The front is wider than it is tall, so a square is trimmed vertically.
        var crop = ArtworkCrop.CenterCrop(640, 640, JewelCaseLayout.FrontArtAspect);
        Assert.Equal(640, crop.Width);
        Assert.Equal(410, crop.Height);
        Assert.Equal(115, crop.Y);
        Assert.Equal(0, crop.X);
        Assert.Equal(JewelCaseLayout.FrontArtAspect, crop.Width / (double)crop.Height, 2);
    }

    [Fact]
    public void The_same_square_is_cropped_differently_for_each_panel()
    {
        // Both panels are wider than they are tall, so a square loses top and bottom
        // on each — but the back is much nearer to square, so it keeps far more of
        // the picture. One crop could not serve both, which is why each panel has
        // its own.
        var front = ArtworkCrop.CenterCrop(640, 640, JewelCaseLayout.FrontArtAspect);
        var back = ArtworkCrop.CenterCrop(640, 640, JewelCaseLayout.BackArtAspect);

        Assert.Equal(640, front.Width);
        Assert.Equal(640, back.Width);
        Assert.True(back.Height > front.Height);
        Assert.Equal(JewelCaseLayout.BackArtAspect, back.Width / (double)back.Height, 2);
        Assert.Equal(0, back.X);
    }

    [Fact]
    public void A_picture_already_at_the_target_keeps_all_of_itself()
    {
        // This is what makes re-saving a package leave the artwork alone, and how
        // someone who wants to choose the framing does it: supply the right shape.
        var crop = ArtworkCrop.CenterCrop(1000, 640, JewelCaseLayout.FrontArtAspect);
        Assert.Equal(0, crop.X);
        Assert.Equal(0, crop.Y);
        Assert.Equal(1000, crop.Width);
        Assert.Equal(640, crop.Height);
    }

    [Fact]
    public void Cropping_is_idempotent()
    {
        var first = ArtworkCrop.CenterCrop(1234, 567, JewelCaseLayout.FrontArtAspect);
        var second = ArtworkCrop.CenterCrop(first.Width, first.Height, JewelCaseLayout.FrontArtAspect);
        Assert.Equal(0, second.X);
        Assert.Equal(0, second.Y);
        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);
    }

    [Fact]
    public void A_degenerate_size_is_returned_untouched_rather_than_throwing()
    {
        var crop = ArtworkCrop.CenterCrop(0, 0, JewelCaseLayout.FrontArtAspect);
        Assert.Equal(0, crop.Width);
        Assert.Equal(0, crop.Height);
    }
}
