using K7Mix.Core;

namespace K7Mix.Tests;

public class MarkdownImportTests
{
    [Fact]
    public void SummerBangers_preserves_hex_and_multiline_and_has_no_tracks()
    {
        var path = TestAudio.Fixture("summer-bangers", "Summer-bangers.md");
        var markdown = File.ReadAllText(path);
        var result = MarkdownImporter.Import(markdown, Path.GetDirectoryName(path)!);

        Assert.Equal("Summer bangers", result.Document.Title);
        Assert.Contains("greats summer bangers", result.Document.Description);
        Assert.Contains("second paragraph", result.Document.Description);
        Assert.Equal("https://www.summer-bangers.com/", result.Document.SourceUrl);
        Assert.Equal("Summer Bangers Class A", result.Document.SideALabel);
        Assert.Equal("Summer Bangers Class B", result.Document.SideBLabel);
        Assert.Equal("cousine", result.Document.Appearance.Font.Id);
        Assert.Equal("#000000", result.Document.Appearance.TextColor.ToHex());
        Assert.Equal("#FFFF00", result.Document.Appearance.TapeColor.ToHex());
        Assert.Equal("#FFFFFF", result.Document.Appearance.StickerColor.ToHex());
        Assert.Equal("Non-Commercial Use Only", result.Document.License);
        Assert.Empty(result.Document.Tracks);
        Assert.Contains(result.Issues.Items, i => i.Message.Contains("Tracks in csv format"));
        Assert.Contains(result.Issues.Items, i => i.Message.Contains("trailing value"));
        Assert.False(result.Issues.HasErrors);
    }

    [Fact]
    public void TracksCsv_quoted_filenames_and_side_b()
    {
        var root = CreateTrackFolder();
        var markdown = File.ReadAllText(TestAudio.Fixture("tracks-csv", "Soundtrack.md"));
        var result = MarkdownImporter.Import(markdown, root);

        Assert.Equal(4, result.Document.Tracks.Count);
        Assert.Equal("Deep forest", result.Document.Tracks[0].Title);
        Assert.Equal("deep-forest", result.Document.Tracks[0].Codename);
        Assert.Equal("Public Domain", result.Document.Tracks[0].License);
        Assert.Equal("Taiga", result.Document.Tracks[2].Title);
        Assert.Equal(3, result.Document.SideBStartTrack);
        Assert.Equal(0, result.Document.SideOf(0));
        Assert.Equal(1, result.Document.SideOf(2));
        Assert.Equal(TransitionKind.Crossfade, result.Document.Tracks[0].TransitionOut.Kind);
        Assert.Equal(0.2, result.Document.Tracks[0].TransitionOut.Duration.TotalSeconds, 3);
        Assert.Equal(TransitionKind.None, result.Document.Tracks[1].TransitionOut.Kind);
        Assert.Equal(TransitionKind.None, result.Document.Tracks[2].TransitionOut.Kind);
        Assert.Contains(result.Issues.Items, i => i.Message.Contains("defaulted to none"));
        Assert.False(result.Issues.HasErrors);
        Assert.True(File.Exists(result.Document.Appearance.FrontArtworkPath));
    }

    [Fact]
    public void Excess_transitions_are_ignored()
    {
        var root = Path.Combine(Path.GetTempPath(), "k7mix-excess-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        TestAudio.SineWav(root, "a.wav", 1);
        TestAudio.SineWav(root, "b.wav", 1);
        var markdown = File.ReadAllText(TestAudio.Fixture("tracks-csv", "excess-transitions.md"));
        var result = MarkdownImporter.Import(markdown, root);
        Assert.Equal(2, result.Document.Tracks.Count);
        Assert.Equal(TransitionKind.Silence, result.Document.Tracks[0].TransitionOut.Kind);
        Assert.Contains(result.Issues.Items, i => i.Message.Contains("ignored"));
    }

    [Fact]
    public void Empty_cells_are_none_and_keep_position()
    {
        var issues = new MixIssueList();
        var parsed = TransitionParser.ParseList("crossfade 1s,,silence 2s", 4);
        Assert.Equal(3, parsed.Transitions.Count);
        Assert.Equal(TransitionKind.Crossfade, parsed.Transitions[0].Kind);
        Assert.Equal(TransitionKind.None, parsed.Transitions[1].Kind);
        Assert.Equal(TransitionKind.Silence, parsed.Transitions[2].Kind);
        Assert.False(parsed.Issues.HasErrors);
    }

    [Fact]
    public void Unrecognised_transition_is_an_error()
    {
        var parsed = TransitionParser.ParseList("blend 4s", 2);
        Assert.True(parsed.Issues.HasErrors);
    }

    [Fact]
    public void Duplicate_recognised_field_is_ambiguous()
    {
        var markdown = """
            # Duplicate

            ## License
            CC0

            ## License
            All rights reserved
            """;
        var result = MarkdownImporter.Import(markdown, Path.GetTempPath());
        Assert.Contains(result.Issues.Items, i => i.Message.Contains("more than once"));
    }

    [Fact]
    public void Path_escape_is_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "k7mix-escape-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var markdown = """
            # Escape

            ## Tracks in csv format
            filename,title,codename,license,source
            ..\secret.wav,Secret,secret,,
            """;
        var result = MarkdownImporter.Import(markdown, root);
        Assert.Contains(result.Issues.Items, i => i.Message.Contains("leaves the source folder"));
    }

    [Fact]
    public void Absolute_path_in_csv_is_accepted()
    {
        var root = Path.Combine(Path.GetTempPath(), "k7mix-abspath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        // Audio file in a completely separate directory.
        var audioDir = Path.Combine(Path.GetTempPath(), "k7mix-audio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(audioDir);
        TestAudio.SineWav(audioDir, "track.wav", 1);
        var absPath = Path.Combine(audioDir, "track.wav").Replace('\\', '/');
        var markdown = $"""
            # Absolute path

            ## Tracks in csv format
            filename,title,codename,license,source
            "{absPath}",My Track,my-track,,
            """;
        var result = MarkdownImporter.Import(markdown, root);
        Assert.Single(result.Document.Tracks);
        Assert.Equal("My Track", result.Document.Tracks[0].Title);
        Assert.False(result.Issues.HasErrors);
    }

    [Fact]
    public void Unknown_fields_are_preserved()
    {
        var markdown = """
            # Extra

            ## Description
            Hello

            ## Catalogue number
            K7-001
            """;
        var result = MarkdownImporter.Import(markdown, Path.GetTempPath());
        Assert.Equal("K7-001", result.Document.UnknownFields["Catalogue number"]);
        Assert.Contains(result.Issues.Items, i => i.Message.Contains("Catalogue number"));
    }

    private static string CreateTrackFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "k7mix-tracks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "art"));
        TestAudio.SineWav(root, "01, deep-forest.wav", 1.0, 220);
        TestAudio.SineWav(root, "02-desert.wav", 1.2, 330);
        TestAudio.SineWav(root, "03 taiga (live).wav", 0.8, 440);
        TestAudio.SineWav(root, "04-pyramid.wav", 1.5, 550);
        File.WriteAllBytes(Path.Combine(root, "art", "front.png"), TinyPng());
        File.WriteAllBytes(Path.Combine(root, "art", "back.png"), TinyPng());
        return root;
    }

    private static byte[] TinyPng() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
        0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x03, 0x00, 0x01, 0x00, 0x05, 0xFE, 0xD4, 0xEF, 0x00, 0x00,
        0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];
}
