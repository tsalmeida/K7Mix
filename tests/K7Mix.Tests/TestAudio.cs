using System.Text;
using K7Mix.Core;

namespace K7Mix.Tests;

internal static class TestAudio
{
    public static string SineWav(string directory, string fileName, double seconds, double hz = 440)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        var frames = (int)(PlaybackProfile.SampleRate * seconds);
        var pcm = new short[frames * PlaybackProfile.Channels];
        for (var i = 0; i < frames; i++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * hz * i / PlaybackProfile.SampleRate) * 8000);
            pcm[i * 2] = sample;
            pcm[i * 2 + 1] = sample;
        }
        MidiRenderer.WriteWav(path, pcm);
        return path;
    }

    public static string TinyMidi(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        // SMF type 0, one track, note 60 for a quarter at 120 bpm, then end.
        var track = new List<byte>();
        void Delta(int value)
        {
            var bytes = new Stack<byte>();
            bytes.Push((byte)(value & 0x7F));
            value >>= 7;
            while (value > 0)
            {
                bytes.Push((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            track.AddRange(bytes);
        }
        Delta(0); track.AddRange([0x90, 60, 80]);
        Delta(96); track.AddRange([0x80, 60, 0]);
        Delta(0); track.AddRange([0xFF, 0x2F, 0x00]);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("MThd"));
        writer.Write(ToBe32(6));
        writer.Write(ToBe16(0));
        writer.Write(ToBe16(1));
        writer.Write(ToBe16(96));
        writer.Write(Encoding.ASCII.GetBytes("MTrk"));
        writer.Write(ToBe32(track.Count));
        writer.Write(track.ToArray());
        return path;
    }

    public static string Fixture(params string[] parts) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "fixtures", Path.Combine(parts)));

    private static byte[] ToBe16(int value) => [(byte)(value >> 8), (byte)value];
    private static byte[] ToBe32(int value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
