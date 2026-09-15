using System.Diagnostics;
using System.Text;
using NAudio.Vorbis;
using NAudio.Wave;

namespace K7Mix.Core;

public interface IAudioEncoder
{
    void EncodeToProfile(string sourcePath, Stream destination, MixTrack track);
}

public sealed class FfmpegAudioEncoder : IAudioEncoder
{
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".flac", ".ogg", ".oga", ".mid", ".midi",
    };

    public static string SupportedCodecsDocument =>
        """
        Import is by decoder, not by file extension. A file whose extension is listed
        still fails if its contents cannot be decoded.

        | Extension | Decoder |
        | --- | --- |
        | .wav | PCM / IEEE float WAV via NAudio, then encoded with ffmpeg libvorbis |
        | .mp3 | MPEG-1/2 Layer III via Windows Media Foundation (same family RunningWild uses) |
        | .flac | FLAC via Media Foundation when the platform decoder is present |
        | .ogg / .oga | Ogg Vorbis via NVorbis; copied without re-encoding when already 44100 Hz stereo Vorbis |
        | .mid / .midi | Standard MIDI File rendered by the bundled GM synthesizer, then encoded |

        Not accepted: Opus-in-Ogg, Speex, MP4/AAC, WMA, Tracker modules, encrypted files.
        """;

    public void EncodeToProfile(string sourcePath, Stream destination, MixTrack track)
    {
        var extension = Path.GetExtension(sourcePath);
        if (extension.Equals(".mid", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".midi", StringComparison.OrdinalIgnoreCase))
        {
            var wav = Path.Combine(Path.GetTempPath(), "k7mix-" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                var report = MidiRenderer.RenderToWav(sourcePath, wav);
                track.SourceExtension = extension;
                EncodeWav(wav, destination);
                if (report.Unsupported.Count > 0)
                    track.Source = Append(track.Source, "MIDI: " + string.Join("; ", report.Unsupported));
            }
            finally
            {
                try { File.Delete(wav); } catch { /* temp */ }
            }
            return;
        }

        if (IsAlreadyProfile(sourcePath))
        {
            using var input = File.OpenRead(sourcePath);
            input.CopyTo(destination);
            track.EncodedProfile = PlaybackProfile.Id;
            return;
        }

        EncodeWithFfmpeg(sourcePath, destination);
        track.EncodedProfile = PlaybackProfile.Id;
    }

    public static TimeSpan ProbeDuration(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".mid" or ".midi")
            return MidiRenderer.Duration(path);
        if (extension is ".ogg" or ".oga")
        {
            using var vorbis = new VorbisWaveReader(path);
            return vorbis.TotalTime;
        }

        using var reader = new AudioFileReader(path);
        return reader.TotalTime;
    }

    public static bool IsSupportedExtension(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    private static bool IsAlreadyProfile(string path)
    {
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".oga", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            using var reader = new VorbisWaveReader(path);
            return reader.WaveFormat.SampleRate == PlaybackProfile.SampleRate
                   && reader.WaveFormat.Channels == PlaybackProfile.Channels;
        }
        catch
        {
            return false;
        }
    }

    private static void EncodeWav(string wavPath, Stream destination) =>
        EncodeWithFfmpeg(wavPath, destination);

    private static void EncodeWithFfmpeg(string sourcePath, Stream destination)
    {
        var ffmpeg = FindFfmpeg()
            ?? throw new MixValidationException(
                "ffmpeg was not found on PATH. Encoding to Ogg Vorbis needs ffmpeg with libvorbis.");

        var output = Path.Combine(Path.GetTempPath(), "k7mix-" + Guid.NewGuid().ToString("N") + ".ogg");
        try
        {
            var args =
                $"-y -hide_banner -loglevel error -i \"{sourcePath}\" " +
                $"-ac {PlaybackProfile.Channels} -ar {PlaybackProfile.SampleRate} " +
                $"-c:a libvorbis -q:a {PlaybackProfile.VorbisQuality} \"{output}\"";
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi)
                ?? throw new MixValidationException("ffmpeg could not be started.");
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(120_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                throw new MixValidationException("ffmpeg timed out while encoding.");
            }
            if (process.ExitCode != 0 || !File.Exists(output))
                throw new MixValidationException("ffmpeg could not encode this file as Ogg Vorbis. " + error.Trim());
            using var file = File.OpenRead(output);
            file.CopyTo(destination);
        }
        finally
        {
            try { File.Delete(output); } catch { /* temp */ }
        }
    }

    public static string? FindFfmpeg()
    {
        var fromPath = FindOnPath("ffmpeg.exe") ?? FindOnPath("ffmpeg");
        if (fromPath is not null) return fromPath;
        var winget = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\WinGet\Packages");
        if (!Directory.Exists(winget)) return null;
        return Directory.EnumerateFiles(winget, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
    }

    private static string? FindOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var candidate = Path.Combine(dir.Trim(), name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string Append(string existing, string extra) =>
        string.IsNullOrWhiteSpace(existing) ? extra : existing + " | " + extra;
}

/// <summary>
/// Test encoder: copies WAV bytes tagged as the profile without calling ffmpeg.
/// Production packaging uses <see cref="FfmpegAudioEncoder"/>.
/// </summary>
public sealed class CopyAudioEncoder : IAudioEncoder
{
    public void EncodeToProfile(string sourcePath, Stream destination, MixTrack track)
    {
        using var input = File.OpenRead(sourcePath);
        input.CopyTo(destination);
        track.EncodedProfile = PlaybackProfile.Id;
        track.PackagedAudioPath ??= sourcePath;
    }
}
