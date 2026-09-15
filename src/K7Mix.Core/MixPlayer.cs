using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace K7Mix.Core;

/// <summary>
/// Plays a mix through NAudio. The transport (skip, sides, transitions) lives in
/// <see cref="MixTransport"/> so games can use the same rules without Avalonia.
/// Continuous playback adds no decoder silence; silence already in a recording is kept.
/// </summary>
public sealed class MixPlayer : IDisposable
{
    private readonly MixDocument _mix;
    private readonly MixTransport _transport;
    private readonly object _gate = new();
    private WaveOutEvent? _output;
    private MixingSampleProvider? _mixer;
    private ISampleProvider? _outgoing;
    private ISampleProvider? _incoming;
    private IDisposable? _outgoingReader;
    private IDisposable? _incomingReader;
    private readonly System.Timers.Timer _clock;
    private DateTime _lastTick;
    private bool _disposed;

    public MixPlayer(MixDocument mix)
    {
        _mix = mix;
        _transport = new MixTransport(mix);
        _clock = new System.Timers.Timer(50) { AutoReset = true };
        _clock.Elapsed += (_, _) => Tick();
    }

    public MixTransport Transport => _transport;
    public event EventHandler? Changed;

    public void PlayPause()
    {
        lock (_gate)
        {
            _transport.PlayPause();
            if (_transport.IsPlaying)
            {
                EnsureOutput();
                if (_outgoing is null) OpenCurrent();
                _output?.Play();
                _lastTick = DateTime.UtcNow;
                _clock.Start();
            }
            else
            {
                _output?.Pause();
                _clock.Stop();
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        lock (_gate)
        {
            _clock.Stop();
            _transport.Stop();
            _output?.Stop();
            CloseReaders();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Next()
    {
        bool started;
        lock (_gate)
        {
            started = _transport.Next();
            if (started) SyncReadersToTransport();
        }
        if (started) Changed?.Invoke(this, EventArgs.Empty);
        return started;
    }

    public bool RequestCodename(string codename)
    {
        bool started;
        lock (_gate)
        {
            started = _transport.RequestCodename(codename);
            if (started) SyncReadersToTransport();
        }
        if (started) Changed?.Invoke(this, EventArgs.Empty);
        return started;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _clock.Dispose();
        lock (_gate)
        {
            _output?.Dispose();
            _output = null;
        }
    }

    private void Tick()
    {
        lock (_gate)
        {
            if (!_transport.IsPlaying) return;
            var now = DateTime.UtcNow;
            var delta = now - _lastTick;
            _lastTick = now;
            var wasTransitioning = _transport.IsTransitioning;
            var previous = _transport.DisplayedTrackIndex;
            _transport.Advance(delta);
            if (_transport.State == TransportState.Stopped)
            {
                _clock.Stop();
                _output?.Stop();
                CloseReaders();
            }
            else if (wasTransitioning && !_transport.IsTransitioning)
            {
                PromoteIncoming();
            }
            ApplyGains();
            if (previous != _transport.DisplayedTrackIndex)
                Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void EnsureOutput()
    {
        if (_output is not null) return;
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(
            PlaybackProfile.SampleRate, PlaybackProfile.Channels))
        {
            ReadFully = true,
        };
        _output = new WaveOutEvent { DesiredLatency = 80 };
        _output.Init(_mixer.ToWaveProvider16());
    }

    private void OpenCurrent()
    {
        CloseReaders();
        var track = _transport.CurrentTrack;
        if (track is null) return;
        var reader = Open(track);
        if (reader is null) return;
        _outgoingReader = reader.Disposable;
        _outgoing = reader.Provider;
        _mixer?.AddMixerInput(_outgoing);
    }

    private void SyncReadersToTransport()
    {
        if (!_transport.IsTransitioning)
        {
            OpenCurrent();
            return;
        }
        var to = _transport.IncomingTrackIndex;
        if (to is null) return;
        var incomingTrack = _mix.Tracks[to.Value];
        var reader = Open(incomingTrack);
        if (reader is null) return;
        _incomingReader?.Dispose();
        _incomingReader = reader.Disposable;
        _incoming = reader.Provider;
        _mixer?.AddMixerInput(_incoming);
        ApplyGains();
    }

    private void PromoteIncoming()
    {
        if (_outgoing is not null) _mixer?.RemoveMixerInput(_outgoing);
        _outgoingReader?.Dispose();
        _outgoingReader = _incomingReader;
        _outgoing = _incoming;
        _incomingReader = null;
        _incoming = null;
        ApplyGains();
    }

    private void ApplyGains()
    {
        if (_outgoing is VolumeSampleProvider outVol)
            outVol.Volume = (float)_transport.OutgoingGain();
        if (_incoming is VolumeSampleProvider inVol)
            inVol.Volume = (float)_transport.IncomingGain();
    }

    private void CloseReaders()
    {
        if (_outgoing is not null) _mixer?.RemoveMixerInput(_outgoing);
        if (_incoming is not null) _mixer?.RemoveMixerInput(_incoming);
        _outgoingReader?.Dispose();
        _incomingReader?.Dispose();
        _outgoingReader = null;
        _incomingReader = null;
        _outgoing = null;
        _incoming = null;
    }

    private static Opened? Open(MixTrack track)
    {
        var path = track.OriginalSourcePath ?? track.PackagedAudioPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        var extension = Path.GetExtension(path).ToLowerInvariant();
        ISampleProvider provider;
        IDisposable disposable;
        if (extension is ".ogg" or ".oga")
        {
            var vorbis = new VorbisWaveReader(path);
            provider = vorbis.ToSampleProvider();
            disposable = vorbis;
        }
        else
        {
            var audio = new AudioFileReader(path);
            provider = audio;
            disposable = audio;
        }

        if (provider.WaveFormat.SampleRate != PlaybackProfile.SampleRate ||
            provider.WaveFormat.Channels != PlaybackProfile.Channels)
        {
            provider = new WdlResamplingSampleProvider(provider, PlaybackProfile.SampleRate);
            if (provider.WaveFormat.Channels == 1)
                provider = new MonoToStereoSampleProvider(provider);
        }

        var volume = new VolumeSampleProvider(provider) { Volume = 1 };
        return new Opened(volume, disposable);
    }

    private sealed record Opened(VolumeSampleProvider Provider, IDisposable Disposable);
}
