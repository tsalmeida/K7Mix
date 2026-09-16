using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace K7Mix.Core;

/// <summary>
/// Plays a mix through NAudio. The transport (skip, sides, transitions) lives in
/// <see cref="MixTransport"/> so games can use the same rules without Avalonia.
/// Audio follows the transport: a track file is opened when that track becomes
/// current, including automatic advances.
/// </summary>
public sealed class MixPlayer : IDisposable
{
    private readonly MixDocument _mix;
    private readonly MixTransport _transport;
    private readonly object _gate = new();
    private WaveOutEvent? _output;
    private MixingSampleProvider? _mixer;
    private VolumeSampleProvider? _outgoing;
    private VolumeSampleProvider? _incoming;
    private IDisposable? _outgoingReader;
    private IDisposable? _incomingReader;
    private int _armedTrack = -1;
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
    public string? LastError { get; private set; }
    public event EventHandler? Changed;

    public void PlayPause()
    {
        lock (_gate)
        {
            _transport.PlayPause();
            if (_transport.IsPlaying)
            {
                try
                {
                    EnsureOutput();
                    ArmCurrent(force: _outgoing is null);
                    _output?.Play();
                    _lastTick = DateTime.UtcNow;
                    _clock.Start();
                    LastError = null;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    _transport.Stop();
                    CloseReaders();
                }
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
            try { _output?.Stop(); } catch { /* ignore */ }
            CloseReaders();
            _armedTrack = -1;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Next()
    {
        bool started;
        lock (_gate)
        {
            started = _transport.Next();
            if (started)
            {
                try
                {
                    if (_transport.IsTransitioning) ArmIncoming();
                    else ArmCurrent(force: true);
                    ApplyGains();
                    LastError = null;
                }
                catch (Exception ex) { LastError = ex.Message; }
            }
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
            if (started)
            {
                try
                {
                    if (_transport.IsTransitioning) ArmIncoming();
                    else ArmCurrent(force: true);
                    ApplyGains();
                    LastError = null;
                }
                catch (Exception ex) { LastError = ex.Message; }
            }
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
            _mixer = null;
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
            var previous = _transport.CurrentTrackIndex;
            var wasTransitioning = _transport.IsTransitioning;
            _transport.Advance(delta);
            if (_transport.State == TransportState.Stopped)
            {
                _clock.Stop();
                try { _output?.Stop(); } catch { /* ignore */ }
                CloseReaders();
                _armedTrack = -1;
                Changed?.Invoke(this, EventArgs.Empty);
                return;
            }

            try
            {
                if (_transport.IsTransitioning)
                    ArmIncoming();
                else if (wasTransitioning)
                    PromoteIncoming();
                else
                    ArmCurrent(force: false);
                ApplyGains();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }

            if (previous != _transport.CurrentTrackIndex ||
                wasTransitioning != _transport.IsTransitioning)
                Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void EnsureOutput()
    {
        if (_output is not null && _mixer is not null) return;
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(
            PlaybackProfile.SampleRate, PlaybackProfile.Channels))
        {
            ReadFully = true,
        };
        _output?.Dispose();
        _output = new WaveOutEvent { DesiredLatency = 100 };
        _output.Init(_mixer.ToWaveProvider16());
    }

    private void ArmCurrent(bool force)
    {
        var index = _transport.CurrentTrackIndex;
        if (!force && _armedTrack == index && _outgoing is not null) return;
        CloseReaders();
        var track = _transport.CurrentTrack;
        if (track is null) return;
        var opened = Open(track);
        _outgoingReader = opened.Disposable;
        _outgoing = opened.Provider;
        _mixer?.AddMixerInput(_outgoing);
        _armedTrack = index;
        ApplyGains();
    }

    private void ArmIncoming()
    {
        var to = _transport.IncomingTrackIndex;
        if (to is null) return;
        if (_incoming is not null) return;
        var opened = Open(_mix.Tracks[to.Value]);
        _incomingReader = opened.Disposable;
        _incoming = opened.Provider;
        _mixer?.AddMixerInput(_incoming);
        ApplyGains();
    }

    private void PromoteIncoming()
    {
        if (_incoming is null)
        {
            ArmCurrent(force: true);
            return;
        }
        if (_outgoing is not null) _mixer?.RemoveMixerInput(_outgoing);
        _outgoingReader?.Dispose();
        _outgoingReader = _incomingReader;
        _outgoing = _incoming;
        _incomingReader = null;
        _incoming = null;
        _armedTrack = _transport.CurrentTrackIndex;
        if (_outgoing is not null) _outgoing.Volume = 1;
    }

    private void ApplyGains()
    {
        if (_outgoing is not null)
            _outgoing.Volume = _transport.IsTransitioning
                ? (float)_transport.OutgoingGain()
                : 1f;
        if (_incoming is not null)
            _incoming.Volume = (float)_transport.IncomingGain();
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

    private Opened Open(MixTrack track)
    {
        var path = track.OriginalSourcePath ?? track.PackagedAudioPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new MixValidationException(
                $"Audio for '{track.Title}' is missing ({path ?? "no path"}).");

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

        if (provider.WaveFormat.SampleRate != PlaybackProfile.SampleRate)
            provider = new WdlResamplingSampleProvider(provider, PlaybackProfile.SampleRate);
        if (provider.WaveFormat.Channels == 1)
            provider = new MonoToStereoSampleProvider(provider);

        var volume = new VolumeSampleProvider(new MixerFormatSampleProvider(provider, _mixer!.WaveFormat))
        {
            Volume = 1,
        };
        return new Opened(volume, disposable);
    }

    private sealed record Opened(VolumeSampleProvider Provider, IDisposable Disposable);

    /// <summary>
    /// MixingSampleProvider requires WaveFormat.Equals; NVorbis/NAudio often
    /// differ in extra-size even at the same rate and channels.
    /// </summary>
    private sealed class MixerFormatSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        public MixerFormatSampleProvider(ISampleProvider source, WaveFormat format)
        {
            _source = source;
            WaveFormat = format;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count) =>
            _source.Read(buffer, offset, count);
    }
}
