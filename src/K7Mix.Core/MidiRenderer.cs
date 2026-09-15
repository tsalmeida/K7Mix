using System.Text;

namespace K7Mix.Core;

/// <summary>
/// Renders Standard MIDI Files with a bundled GM-style synthesizer. Channel 10
/// is percussion. Instrument programs 0–127 are honoured as timbre families;
/// SysEx, SMF type 2 and unsupported controllers are reported, not guessed.
/// </summary>
public static class MidiRenderer
{
    public const int SampleRate = PlaybackProfile.SampleRate;
    public const int Channels = PlaybackProfile.Channels;

    public static MidiRenderReport RenderToWav(string midiPath, string wavPath)
    {
        var file = File.ReadAllBytes(midiPath);
        var parsed = Parse(file);
        var pcm = Render(parsed);
        WriteWav(wavPath, pcm);
        return parsed.Report;
    }

    public static TimeSpan Duration(string midiPath)
    {
        var parsed = Parse(File.ReadAllBytes(midiPath));
        return TimeSpan.FromSeconds(parsed.TotalSamples / (double)SampleRate);
    }

    internal static ParsedMidi Parse(byte[] data)
    {
        var report = new MidiRenderReport();
        if (data.Length < 14 || Encoding.ASCII.GetString(data, 0, 4) != "MThd")
            throw new MixValidationException("This is not a Standard MIDI File.");

        var headerLength = ReadBe32(data, 4);
        var format = ReadBe16(data, 8);
        var ntrks = ReadBe16(data, 10);
        var division = ReadBe16(data, 12);
        if (format > 1)
            report.Unsupported.Add($"SMF type {format} is not supported (type 0 and 1 are).");
        if ((division & 0x8000) != 0)
            report.Unsupported.Add("SMPTE time division is not supported.");

        var ticksPerQuarter = Math.Max(1, division & 0x7FFF);
        var offset = 8 + headerLength;
        var events = new List<MidiEvent>();
        for (var track = 0; track < ntrks && offset + 8 <= data.Length; track++)
        {
            if (Encoding.ASCII.GetString(data, offset, 4) != "MTrk")
            {
                report.Unsupported.Add($"Track {track} is missing an MTrk header.");
                break;
            }
            var length = (int)ReadBe32(data, offset + 4);
            offset += 8;
            ParseTrack(data, offset, length, events, report);
            offset += length;
        }

        events.Sort((a, b) => a.Tick.CompareTo(b.Tick));
        var tempo = 500_000; // default 120 bpm
        var samples = 0L;
        var lastTick = 0;
        foreach (var ev in events)
        {
            var deltaTicks = ev.Tick - lastTick;
            samples += TicksToSamples(deltaTicks, ticksPerQuarter, tempo);
            lastTick = ev.Tick;
            if (ev.Kind == MidiKind.Tempo) tempo = ev.Value;
            ev.Sample = samples;
        }

        var total = samples + SampleRate / 2;
        return new ParsedMidi(events, total, report);
    }

    private static void ParseTrack(byte[] data, int start, int length, List<MidiEvent> events, MidiRenderReport report)
    {
        var i = start;
        var end = start + length;
        var tick = 0;
        var running = 0;
        while (i < end)
        {
            tick += ReadVar(data, ref i, end);
            if (i >= end) break;
            var b = data[i];
            int status;
            if (b < 0x80)
            {
                status = running;
            }
            else
            {
                status = b;
                i++;
                if (b < 0xF0) running = b;
            }

            if (status == 0xFF)
            {
                if (i >= end) break;
                var type = data[i++];
                var len = ReadVar(data, ref i, end);
                if (type == 0x2F) break;
                if (type == 0x51 && len == 3 && i + 3 <= end)
                {
                    var tempo = (data[i] << 16) | (data[i + 1] << 8) | data[i + 2];
                    events.Add(new MidiEvent { Tick = tick, Kind = MidiKind.Tempo, Value = tempo });
                }
                i += len;
                continue;
            }

            if (status == 0xF0 || status == 0xF7)
            {
                var len = ReadVar(data, ref i, end);
                i += len;
                report.Unsupported.Add("SysEx event ignored.");
                continue;
            }

            var command = status & 0xF0;
            var channel = status & 0x0F;
            switch (command)
            {
                case 0x80:
                    if (i + 1 >= end) return;
                    events.Add(new MidiEvent { Tick = tick, Kind = MidiKind.NoteOff, Channel = channel, Note = data[i], Velocity = data[i + 1] });
                    i += 2;
                    break;
                case 0x90:
                    if (i + 1 >= end) return;
                    var vel = data[i + 1];
                    events.Add(new MidiEvent
                    {
                        Tick = tick,
                        Kind = vel == 0 ? MidiKind.NoteOff : MidiKind.NoteOn,
                        Channel = channel,
                        Note = data[i],
                        Velocity = vel,
                    });
                    i += 2;
                    break;
                case 0xC0:
                    if (i >= end) return;
                    events.Add(new MidiEvent { Tick = tick, Kind = MidiKind.Program, Channel = channel, Value = data[i] });
                    i += 1;
                    break;
                case 0xB0:
                    if (i + 1 >= end) return;
                    var cc = data[i];
                    if (cc is not 7 and not 10 and not 11 and not 64 and not 120 and not 121 and not 123)
                        report.Unsupported.Add($"Controller {cc} on channel {channel + 1} is not applied.");
                    i += 2;
                    break;
                case 0xA0:
                case 0xE0:
                    report.Unsupported.Add(command == 0xE0
                        ? "Pitch bend is not applied."
                        : "Polyphonic aftertouch is not applied.");
                    i += 2;
                    break;
                case 0xD0:
                    report.Unsupported.Add("Channel aftertouch is not applied.");
                    i += 1;
                    break;
                default:
                    i += 1;
                    break;
            }
        }
    }

    internal static short[] Render(ParsedMidi parsed)
    {
        var samples = (int)Math.Min(int.MaxValue / 4, parsed.TotalSamples * Channels);
        var pcm = new short[samples];
        var voices = new Voice[24];
        var programs = new int[16];
        var ei = 0;
        var events = parsed.Events;
        for (var s = 0; s < parsed.TotalSamples; s++)
        {
            while (ei < events.Count && events[ei].Sample <= s)
            {
                Apply(events[ei], voices, programs);
                ei++;
            }

            double left = 0, right = 0;
            for (var v = 0; v < voices.Length; v++)
            {
                if (!voices[v].Active) continue;
                var sample = voices[v].Next();
                left += sample;
                right += sample;
            }
            left = Math.Clamp(left, -1, 1);
            right = Math.Clamp(right, -1, 1);
            var i = s * Channels;
            if (i + 1 >= pcm.Length) break;
            pcm[i] = (short)(left * 20000);
            pcm[i + 1] = (short)(right * 20000);
        }
        return pcm;
    }

    private static void Apply(MidiEvent ev, Voice[] voices, int[] programs)
    {
        switch (ev.Kind)
        {
            case MidiKind.Program:
                programs[ev.Channel & 15] = ev.Value & 127;
                break;
            case MidiKind.NoteOff:
                for (var i = 0; i < voices.Length; i++)
                {
                    if (voices[i].Active && voices[i].Channel == ev.Channel && voices[i].Note == ev.Note)
                        voices[i].Release();
                }
                break;
            case MidiKind.NoteOn:
                var slot = -1;
                for (var i = 0; i < voices.Length; i++)
                    if (!voices[i].Active) { slot = i; break; }
                if (slot < 0)
                {
                    slot = 0;
                    var oldest = voices[0].Age;
                    for (var i = 1; i < voices.Length; i++)
                        if (voices[i].Age > oldest) { oldest = voices[i].Age; slot = i; }
                }
                var percussion = ev.Channel == 9;
                voices[slot].Start(ev.Channel, ev.Note, ev.Velocity, programs[ev.Channel & 15], percussion);
                break;
        }
    }

    private static long TicksToSamples(int ticks, int ticksPerQuarter, int tempoUs)
    {
        var seconds = ticks * (tempoUs / 1_000_000.0) / ticksPerQuarter;
        return (long)Math.Round(seconds * SampleRate);
    }

    private static int ReadBe16(byte[] d, int i) => (d[i] << 8) | d[i + 1];
    private static int ReadBe32(byte[] d, int i) => (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];

    private static int ReadVar(byte[] d, ref int i, int end)
    {
        var value = 0;
        for (var n = 0; n < 4 && i < end; n++)
        {
            var b = d[i++];
            value = (value << 7) | (b & 0x7F);
            if ((b & 0x80) == 0) break;
        }
        return value;
    }

    internal static void WriteWav(string path, short[] pcm)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        var dataBytes = pcm.Length * 2;
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)Channels);
        writer.Write(SampleRate);
        writer.Write(SampleRate * Channels * 2);
        writer.Write((short)(Channels * 2));
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        foreach (var s in pcm) writer.Write(s);
    }

    private struct Voice
    {
        public bool Active;
        public int Channel;
        public int Note;
        public int Age;
        public double Phase;
        public double Step;
        public double Level;
        public double Brightness;
        public bool Percussion;
        private bool _releasing;

        public void Start(int channel, int note, int velocity, int program, bool percussion)
        {
            Active = true;
            Channel = channel;
            Note = note;
            Age = 0;
            _releasing = false;
            Percussion = percussion;
            var freq = 440.0 * Math.Pow(2, (note - 69) / 12.0);
            Step = freq / SampleRate;
            Level = Math.Clamp(velocity / 127.0, 0, 1) * 0.35;
            Brightness = Timbre(program, percussion);
            Phase = 0;
        }

        public void Release() => _releasing = true;

        public double Next()
        {
            Age++;
            if (_releasing)
            {
                Level *= 0.9985;
                if (Level < 0.0005) { Active = false; return 0; }
            }
            Phase += Step;
            if (Phase >= 1) Phase -= 1;
            double wave;
            if (Percussion)
            {
                wave = (Random.Shared.NextDouble() * 2 - 1) * Math.Exp(-Age / (double)(SampleRate / 8));
            }
            else if (Brightness < 0.35)
            {
                wave = Math.Sin(Phase * Math.PI * 2);
            }
            else if (Brightness < 0.7)
            {
                wave = Phase < 0.5 ? 1 : -1;
            }
            else
            {
                wave = (Phase * 2) - 1;
            }
            return wave * Level;
        }

        private static double Timbre(int program, bool percussion)
        {
            if (percussion) return 1;
            return program switch
            {
                < 8 => 0.2,
                < 16 => 0.6,
                < 24 => 0.25,
                < 32 => 0.75,
                < 40 => 0.15,
                < 56 => 0.3,
                < 80 => 0.65,
                _ => 0.5,
            };
        }
    }

    internal sealed class ParsedMidi
    {
        public ParsedMidi(List<MidiEvent> events, long totalSamples, MidiRenderReport report)
        {
            Events = events;
            TotalSamples = totalSamples;
            Report = report;
        }

        public List<MidiEvent> Events { get; }
        public long TotalSamples { get; }
        public MidiRenderReport Report { get; }
    }

    internal sealed class MidiEvent
    {
        public int Tick;
        public long Sample;
        public MidiKind Kind;
        public int Channel;
        public int Note;
        public int Velocity;
        public int Value;
    }

    internal enum MidiKind { NoteOn, NoteOff, Program, Tempo }

    public sealed class MidiRenderReport
    {
        public List<string> Unsupported { get; } = [];
    }
}
