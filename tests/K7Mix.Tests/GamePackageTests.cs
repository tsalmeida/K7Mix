using K7Mix.Core;

namespace K7Mix.Tests;

public class GamePackageTests
{
    private static string? DefaultK7m()
    {
        var path = @"L:\Library\Coding\C89\RunningWild\open-assets\music\collections\default.k7m";
        return File.Exists(path) ? path : null;
    }

    [Fact]
    public void Default_k7m_opens_with_tracks_and_playable_audio()
    {
        var path = DefaultK7m();
        if (path is null) return;

        var extract = Path.Combine(Path.GetTempPath(), "k7mix-game-" + Guid.NewGuid().ToString("N"));
        var mix = K7mPackage.Load(path, extract);
        Assert.Equal("Default", mix.Title);
        Assert.Equal("roboto", mix.Appearance.Font.Id);
        Assert.True(mix.Tracks.Count >= 10);
        Assert.Contains(mix.Tracks, t => t.Codename == "dense-forest");
        foreach (var track in mix.Tracks)
        {
            Assert.False(string.IsNullOrWhiteSpace(track.OriginalSourcePath));
            Assert.True(File.Exists(track.OriginalSourcePath!), track.Title);
            Assert.True(track.Duration > TimeSpan.Zero, track.Title);
        }
    }
}
