using System.IO.Compression;
using System.Text.Json.Nodes;
using K7Mix.Core;

namespace K7Mix.Tests;

public class PackageTests
{
    [Fact]
    public void Save_and_reopen_preserves_metadata_without_source_files()
    {
        var work = Path.Combine(Path.GetTempPath(), "k7mix-pkg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var wav = TestAudio.SineWav(work, "track.wav", 0.4);
        var mix = new MixDocument
        {
            Title = "Keep me",
            Description = "A mix",
            License = "CC0",
            SourceUrl = "https://example.invalid",
            SideALabel = "Alpha",
            SideBStartTrack = 1,
        };
        mix.Appearance.Font = CassetteFont.Cousine;
        mix.Appearance.TapeColor = HexColor.Parse("#FFFF00", "tape");
        mix.Tracks.Add(new MixTrack
        {
            Title = "Deep forest",
            Codename = "deep-forest",
            License = "CC0",
            Source = "game",
            Duration = TimeSpan.FromSeconds(0.4),
            OriginalSourcePath = wav,
        });

        var path = Path.Combine(work, "keep.k7m");
        K7mPackage.Save(mix, path, lockedCopy: false, new CopyAudioEncoder());
        File.Delete(wav);

        var extract = Path.Combine(work, "extract");
        var loaded = K7mPackage.Load(path, extract);
        Assert.Equal("Keep me", loaded.Title);
        Assert.Equal("CC0", loaded.License);
        Assert.Equal("cousine", loaded.Appearance.Font.Id);
        Assert.Equal("#FFFF00", loaded.Appearance.TapeColor.ToHex());
        Assert.Equal("deep-forest", loaded.Tracks[0].Codename);
        Assert.True(File.Exists(loaded.Tracks[0].OriginalSourcePath));
        Assert.False(loaded.Locked);
    }

    [Fact]
    public void A_version_1_package_moves_each_transition_onto_the_track_it_leads_into()
    {
        var work = Path.Combine(Path.GetTempPath(), "k7mix-v1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var wav = TestAudio.SineWav(work, "t.wav", 0.3);
        var mix = new MixDocument { Title = "Legacy" };
        for (var i = 0; i < 3; i++)
        {
            mix.Tracks.Add(new MixTrack
            {
                Title = $"T{i + 1}",
                Duration = TimeSpan.FromSeconds(0.3),
                OriginalSourcePath = wav,
            });
        }
        var path = Path.Combine(work, "legacy.k7m");
        K7mPackage.Save(mix, path, lockedCopy: false, new CopyAudioEncoder());

        // Rewrite the manifest the way version 1 wrote it: the boundary stored on
        // the outgoing track, with the last track's value unused.
        var outgoing = new[] { "crossfade 0.1s", "silence 0.2s", "none" };
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry(K7mPackage.ManifestName)!;
            string json;
            using (var reader = new StreamReader(entry.Open())) json = reader.ReadToEnd();
            var node = JsonNode.Parse(json)!;
            node["formatVersion"] = 1;
            var tracks = node["tracks"]!.AsArray();
            for (var i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i]!.AsObject();
                track.Remove("transitionIn");
                track.Remove("loop");
                track["transitionOut"] = outgoing[i];
            }
            entry.Delete();
            var rewritten = zip.CreateEntry(K7mPackage.ManifestName);
            using var writer = new StreamWriter(rewritten.Open());
            writer.Write(node.ToJsonString());
        }

        var loaded = K7mPackage.Load(path, Path.Combine(work, "extract"));

        // A version 1 tape did not loop, so the first track's slot stays empty.
        Assert.Equal(TransitionKind.None, loaded.Tracks[0].TransitionIn.Kind);
        Assert.Equal(TransitionKind.Crossfade, loaded.Tracks[1].TransitionIn.Kind);
        Assert.Equal(0.1, loaded.Tracks[1].TransitionIn.Duration.TotalSeconds, 3);
        Assert.Equal(TransitionKind.Silence, loaded.Tracks[2].TransitionIn.Kind);
        Assert.Equal(0.2, loaded.Tracks[2].TransitionIn.Duration.TotalSeconds, 3);
        Assert.All(loaded.Tracks, t => Assert.False(t.Loop));
    }

    [Fact]
    public void Locked_copy_leaves_the_editable_original()
    {
        var work = Path.Combine(Path.GetTempPath(), "k7mix-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var wav = TestAudio.SineWav(work, "t.wav", 0.3);
        var mix = new MixDocument { Title = "Editable" };
        mix.Tracks.Add(new MixTrack { Title = "A", Duration = TimeSpan.FromSeconds(0.3), OriginalSourcePath = wav });
        var editable = Path.Combine(work, "mix.k7m");
        var locked = Path.Combine(work, "mix.locked.k7m");
        K7mPackage.Save(mix, editable, lockedCopy: false, new CopyAudioEncoder());
        K7mPackage.Save(mix, locked, lockedCopy: true, new CopyAudioEncoder());
        var loadedLock = K7mPackage.Load(locked, Path.Combine(work, "l"));
        var loadedEdit = K7mPackage.Load(editable, Path.Combine(work, "e"));
        Assert.True(loadedLock.Locked);
        Assert.False(loadedEdit.Locked);
        Assert.Equal("Editable", loadedEdit.Title);
    }

    [Fact]
    public void Missing_asset_is_rejected_on_open()
    {
        var work = Path.Combine(Path.GetTempPath(), "k7mix-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var wav = TestAudio.SineWav(work, "t.wav", 0.3);
        var mix = new MixDocument { Title = "Broken" };
        mix.Tracks.Add(new MixTrack { Title = "A", Duration = TimeSpan.FromSeconds(0.3), OriginalSourcePath = wav });
        var path = Path.Combine(work, "broken.k7m");
        K7mPackage.Save(mix, path, lockedCopy: false, new CopyAudioEncoder());
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var audio = zip.Entries.First(e => e.FullName.StartsWith("audio/", StringComparison.Ordinal));
            audio.Delete();
        }
        var ex = Assert.Throws<MixValidationException>(() =>
            K7mPackage.Load(path, Path.Combine(work, "x")));
        Assert.Contains("missing", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unsupported_version_is_clear()
    {
        var work = Path.Combine(Path.GetTempPath(), "k7mix-ver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var path = Path.Combine(work, "old.k7m");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("manifest.json");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("""{"format":"k7m","formatVersion":99,"title":"x","tracks":[]}""");
        }
        var ex = Assert.Throws<MixValidationException>(() =>
            K7mPackage.Load(path, Path.Combine(work, "x")));
        Assert.Contains("version 99", ex.Message);
    }

    [Fact]
    public void Ffmpeg_package_decodes_with_stb_vorbis()
    {
        if (FfmpegAudioEncoder.FindFfmpeg() is null)
            return;

        var work = Path.Combine(Path.GetTempPath(), "k7mix-ogg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var wav = TestAudio.SineWav(work, "tone.wav", 0.35, 440);
        var mix = new MixDocument { Title = "Vorbis" };
        mix.Tracks.Add(new MixTrack
        {
            Title = "Tone",
            Codename = "tone",
            Duration = TimeSpan.FromSeconds(0.35),
            OriginalSourcePath = wav,
        });
        var path = Path.Combine(work, "tone.k7m");
        K7mPackage.Save(mix, path, lockedCopy: false, new FfmpegAudioEncoder());
        File.Delete(wav);
        var extract = Path.Combine(work, "ex");
        var loaded = K7mPackage.Load(path, extract);
        var ogg = loaded.Tracks[0].OriginalSourcePath!;
        Assert.Equal(".ogg", Path.GetExtension(ogg), ignoreCase: true);
        Assert.True(File.Exists(ogg));

        var probe = ProbeWithStbVorbis(ogg);
        if (probe is null) return;
        Assert.Contains("OK", probe);
        Assert.Contains("rate=44100", probe);
        Assert.Contains("channels=2", probe);
    }

    [Fact]
    public void Midi_render_reports_and_writes_audio()
    {
        var work = Path.Combine(Path.GetTempPath(), "k7mix-mid-" + Guid.NewGuid().ToString("N"));
        var midi = TestAudio.TinyMidi(work, "beep.mid");
        var wav = Path.Combine(work, "beep.wav");
        var report = MidiRenderer.RenderToWav(midi, wav);
        Assert.True(File.Exists(wav));
        Assert.True(new FileInfo(wav).Length > 100);
        Assert.True(MidiRenderer.Duration(midi) > TimeSpan.Zero);
        _ = report;
    }

    private static string? ProbeWithStbVorbis(string ogg)
    {
        var runningWild = @"L:\Library\Coding\C89\RunningWild\frontend\sdl\third_party";
        var header = Path.Combine(runningWild, "stb_vorbis.c");
        if (!File.Exists(header)) return null;
        var native = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "native", "k7m_stb_vorbis_probe.c"));
        if (!File.Exists(native)) return null;
        var exe = Path.Combine(Path.GetTempPath(), "k7m_stb_vorbis_probe.exe");
        var compiler = FindCl() ?? FindClang();
        if (compiler is null) return null;
        var compile = compiler.EndsWith("clang.exe", StringComparison.OrdinalIgnoreCase)
            ? $"\"{compiler}\" -O2 -o \"{exe}\" \"{native}\" -I \"{runningWild}\""
            : $"\"{compiler}\" /nologo /O2 /Fe\"{exe}\" \"{native}\" /I \"{runningWild}\"";
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c " + compile,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using (var process = System.Diagnostics.Process.Start(psi))
        {
            process!.WaitForExit(30_000);
            if (process.ExitCode != 0 || !File.Exists(exe)) return null;
        }
        var run = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"\"{ogg}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var probe = System.Diagnostics.Process.Start(run);
        var output = probe!.StandardOutput.ReadToEnd();
        probe.WaitForExit(15_000);
        return output;
    }

    private static string? FindCl()
    {
        var vs = @"C:\Program Files\Microsoft Visual Studio";
        if (!Directory.Exists(vs)) return null;
        return Directory.EnumerateFiles(vs, "cl.exe", SearchOption.AllDirectories)
            .FirstOrDefault(p => p.Contains(@"Hostx64\x64", StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindClang()
    {
        var clang = @"C:\msys64\clang64\bin\clang.exe";
        return File.Exists(clang) ? clang : null;
    }
}
