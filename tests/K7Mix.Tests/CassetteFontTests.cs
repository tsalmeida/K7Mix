using K7Mix.Core;

namespace K7Mix.Tests;

public class CassetteFontTests
{
    private const string Catalog = """
        {
          "families": [
            { "family": "Libron", "role": "serif" },
            { "family": "Roboto", "role": "sans" },
            { "family": "Roboto", "role": "sans", "windowsFamily": "Roboto Condensed" },
            { "family": "Barlow", "role": "sans", "windowsFamily": "Barlow Semi Condensed" },
            { "family": "Rock Salt", "role": "script" },
            { "family": "Fixedsys Excelsior", "role": "pixel" },
            { "family": "Font Awesome 6 Pro Solid", "role": "symbol" }
          ]
        }
        """;

    [Fact]
    public void The_catalog_adds_its_faces_after_the_bundled_four_without_repeating_them()
    {
        var faces = CassetteFont.LogarktosFaces(Catalog);

        Assert.Equal(["Libron", "Barlow"], faces.Select(f => f.FamilyName));
        Assert.All(faces, f => Assert.False(f.Bundled));
        Assert.Equal("libron", faces[0].Id);
        Assert.Equal("Barlow Semi Condensed", faces[1].ScreenFamily);
    }

    [Fact]
    public void A_catalog_face_is_found_by_id_or_by_name()
    {
        var faces = new List<CassetteFont>(CassetteFont.BundledFaces);
        faces.AddRange(CassetteFont.LogarktosFaces(Catalog));

        Assert.Equal("Libron", CassetteFont.Parse("libron", faces).FamilyName);
        Assert.Equal("Libron", CassetteFont.Parse("Libron", faces).FamilyName);
        Assert.Same(CassetteFont.RockSalt, CassetteFont.Parse("Rock Salt", faces));
        Assert.Throws<MixValidationException>(() => CassetteFont.Parse("Comic Sans", faces));
    }

    [Fact]
    public void A_missing_or_broken_catalog_leaves_the_bundled_four()
    {
        Assert.Empty(CassetteFont.LogarktosFaces(null));
        Assert.Empty(CassetteFont.LogarktosFaces("{ not json"));
    }

    [Fact]
    public void A_package_set_in_a_Logarktos_face_reopens_by_family_name()
    {
        var work = Path.Combine(Path.GetTempPath(), "k7mix-font-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var wav = TestAudio.SineWav(work, "track.wav", 0.3);
        var mix = new MixDocument { Title = "Set in a family the catalog may not list" };
        mix.Appearance.Font = CassetteFont.FromFamily("Some Logarktos Face", "Some Logarktos Face Condensed");
        mix.Tracks.Add(new MixTrack { Title = "T1", Duration = TimeSpan.FromSeconds(0.3), OriginalSourcePath = wav });

        var path = Path.Combine(work, "face.k7m");
        K7mPackage.Save(mix, path, lockedCopy: false, new CopyAudioEncoder());
        var loaded = K7mPackage.Load(path, Path.Combine(work, "extract"));

        Assert.Equal("some-logarktos-face", loaded.Appearance.Font.Id);
        Assert.Equal("Some Logarktos Face", loaded.Appearance.Font.FamilyName);
        Assert.Equal("Some Logarktos Face Condensed", loaded.Appearance.Font.ScreenFamily);
        Assert.False(loaded.Appearance.Font.Bundled);
    }
}
