namespace K7Mix.Core;

public enum TransportState
{
    Stopped,
    Playing,
    Paused,
}

/// <summary>
/// Playback clock for a mix. Independent of any UI. Skip always honours the
/// configured transition and cannot be disabled. Further skip requests while a
/// transition is running are ignored, not queued. Play/Pause and Stop remain
/// available. After the last track the tape loops back to the first, crossing the
/// first track's own transition. There is no Previous, seeking or direct track
/// selection.
/// </summary>
public sealed class MixTransport
{
    private readonly MixDocument _mix;
    private TimeSpan _positionOnTrack;
    private int _index;

    /// <summary>
    /// Whether the current track has already taken its repeat. Only meaningful on
    /// a track whose <see cref="MixTrack.Loop"/> is set; cleared whenever playback
    /// arrives on a track.
    /// </summary>
    private bool _loopPlayed;

    private bool _inTransition;
    private int _transitionFrom;
    private int _transitionTo;
    private TrackTransition _activeTransition = TrackTransition.None;
    private TimeSpan _transitionElapsed;
    private bool _automaticTransition;
    private TransportState _state = TransportState.Stopped;

    public MixTransport(MixDocument mix)
    {
        _mix = mix;
    }

    public TransportState State => _state;
    public bool IsPlaying => _state == TransportState.Playing;
    public bool IsTransitioning => _inTransition;
    public int CurrentTrackIndex => _index;
    public MixTrack? CurrentTrack => HasTracks && _index >= 0 && _index < _mix.Tracks.Count
        ? _mix.Tracks[_index]
        : null;
    public int DisplayedTrackIndex =>
        _inTransition && _activeTransition.Kind == TransitionKind.Crossfade
            ? _transitionFrom
            : _index;
    public int DisplayedSide => _mix.SideOf(DisplayedTrackIndex);
    public TimeSpan PositionOnTrack => _positionOnTrack;
    public TimeSpan MixPosition => MixPositionAt(_index, PositionInSpan);
    public bool HasTracks => _mix.Tracks.Count > 0;

    /// <summary>
    /// How long the current track occupies the tape: twice its duration when it
    /// loops, so a progress bar covers the repeat instead of snapping back.
    /// </summary>
    public TimeSpan CurrentTrackSpan => CurrentTrack?.TapeSpan ?? TimeSpan.Zero;

    /// <summary>
    /// The playhead measured against <see cref="CurrentTrackSpan"/>, so the second
    /// pass of a looping track reads as the second half of the span.
    /// </summary>
    public TimeSpan PositionInSpan
    {
        get
        {
            var track = CurrentTrack;
            if (track is null) return TimeSpan.Zero;
            return track.Loop && _loopPlayed ? track.Duration + _positionOnTrack : _positionOnTrack;
        }
    }

    /// <summary>
    /// Where along the progress bar the repeat begins, or null when the track does
    /// not loop. Always the midpoint: the repeat plays the whole track again.
    /// </summary>
    public double? LoopMarker => CurrentTrack?.Loop == true ? 0.5 : null;

    /// <summary>Whether the current track is playing its repeat.</summary>
    public bool OnLoopRepeat => CurrentTrack?.Loop == true && _loopPlayed;

    public void PlayPause()
    {
        if (!HasTracks) return;
        if (_state == TransportState.Playing)
        {
            _state = TransportState.Paused;
            return;
        }

        if (_state == TransportState.Stopped)
        {
            _index = 0;
            _positionOnTrack = TimeSpan.Zero;
            _inTransition = false;
            _loopPlayed = false;
        }
        _state = TransportState.Playing;
    }

    public void Stop()
    {
        _state = TransportState.Stopped;
        _inTransition = false;
        _automaticTransition = false;
        _loopPlayed = false;
        if (HasTracks)
        {
            _index = 0;
            _positionOnTrack = TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Next / skip. Honours the transition belonging to the track being moved into,
    /// including a zero-duration 'none'. Ignored while a transition is already
    /// running. On the last track it wraps to the first, which is the same boundary
    /// the tape takes on its own at the end.
    /// </summary>
    public bool Next()
    {
        if (_state == TransportState.Stopped || !HasTracks) return false;
        if (_inTransition) return false;
        var to = _mix.NextIndex(_index);
        BeginTransition(_index, to, _mix.Tracks[to].TransitionIn, automatic: false);
        return true;
    }

    /// <summary>
    /// Game-driven request for a nonempty unique codename. Same rule as Skip:
    /// never an instantaneous jump, never queued while a transition is running.
    /// </summary>
    public bool RequestCodename(string codename)
    {
        if (_state == TransportState.Stopped || !HasTracks) return false;
        if (_inTransition) return false;
        var target = _mix.Tracks.FindIndex(t =>
            string.Equals(t.Codename, codename, StringComparison.OrdinalIgnoreCase));
        if (target < 0 || target == _index) return false;
        // The requested track's own transition brings playback in, wherever the
        // request came from; the skip is not instantaneous.
        BeginTransition(_index, target, _mix.Tracks[target].TransitionIn, automatic: false);
        return true;
    }

    public void Advance(TimeSpan delta)
    {
        if (_state != TransportState.Playing || !HasTracks || delta <= TimeSpan.Zero) return;

        // Iterative, not recursive. A codename request may target a track behind the
        // playhead (see skip-behavior.md), so _index is not monotonic and the old
        // Advance/AdvanceTransition mutual recursion had no bound on its depth. The
        // tape also loops rather than ending, so nothing stops the walk on its own:
        // the step guard counts boundaries crossed, which holds in either direction
        // and caps a delta long enough to wrap the whole tape.
        var steps = 0;
        var limit = (_mix.Tracks.Count + 2) * 4;
        while (delta > TimeSpan.Zero && _state == TransportState.Playing && steps++ < limit)
            delta = AdvanceStep(delta);
    }

    /// <summary>
    /// Consumes as much of <paramref name="delta"/> as fits before the next boundary
    /// and returns the unconsumed remainder.
    /// </summary>
    private TimeSpan AdvanceStep(TimeSpan delta)
    {
        if (_inTransition)
        {
            var remainingInTransition = _activeTransition.Duration - _transitionElapsed;
            if (delta >= remainingInTransition)
            {
                var leftover = delta - Max(remainingInTransition, TimeSpan.Zero);
                CompleteTransition();
                return leftover;
            }
            _transitionElapsed += delta;
            if (_activeTransition.Kind == TransitionKind.Crossfade)
            {
                var outgoing = _mix.Tracks[_transitionFrom];
                _positionOnTrack = outgoing.Duration - _activeTransition.Duration + _transitionElapsed;
            }
            return TimeSpan.Zero;
        }

        var track = _mix.Tracks[_index];
        // A finished crossfade leaves the playhead at the transition duration, which
        // can sit past a short incoming track. Clamping keeps the leftover from
        // growing larger than the delta that came in.
        var remaining = Max(track.Duration - _positionOnTrack, TimeSpan.Zero);

        // A looping track takes its repeat before any boundary is considered. The
        // repeat is seamless, so there is no transition to start early and nothing
        // to hand over to: the playhead simply returns to zero.
        if (track.Loop && !_loopPlayed)
        {
            if (delta < remaining)
            {
                _positionOnTrack += delta;
                return TimeSpan.Zero;
            }
            _loopPlayed = true;
            _positionOnTrack = TimeSpan.Zero;
            return delta - remaining;
        }

        // The boundary leaving this track is owned by whichever track comes next,
        // and on the last track that is the first one: the tape loops.
        var to = _mix.NextIndex(_index);
        var transition = _mix.Tracks[to].TransitionIn;
        var autoAt = AutomaticStartOffset(track, transition);
        var untilAuto = autoAt - _positionOnTrack;

        if (untilAuto <= delta && untilAuto >= TimeSpan.Zero)
        {
            _positionOnTrack = autoAt;
            var leftover = delta - untilAuto;
            BeginTransition(_index, to, transition, automatic: true);
            return leftover;
        }

        if (delta >= remaining)
        {
            var leftover = delta - remaining;
            BeginTransition(_index, to, transition, automatic: true);
            return leftover;
        }

        _positionOnTrack += delta;
        return TimeSpan.Zero;
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    public double OutgoingGain()
    {
        if (!_inTransition) return 1;
        var t = Progress();
        return _activeTransition.Kind switch
        {
            TransitionKind.Crossfade => 1 - t,
            TransitionKind.FadeOut => 1 - t,
            TransitionKind.Silence => 0,
            _ => 0,
        };
    }

    public double IncomingGain()
    {
        if (!_inTransition) return 0;
        var t = Progress();
        return _activeTransition.Kind switch
        {
            TransitionKind.Crossfade => t,
            TransitionKind.FadeOut => 0,
            TransitionKind.Silence => 0,
            _ => 1,
        };
    }

    public int? IncomingTrackIndex => _inTransition ? _transitionTo : null;

    private void BeginTransition(int from, int to, TrackTransition transition, bool automatic)
    {
        _transitionFrom = from;
        _transitionTo = to;
        _activeTransition = transition;
        _transitionElapsed = TimeSpan.Zero;
        _automaticTransition = automatic;
        if (transition.IsInstant)
        {
            CompleteTransition();
            return;
        }
        _inTransition = true;
        if (transition.Kind == TransitionKind.Silence)
            _positionOnTrack = _mix.Tracks[from].Duration;
    }

    private void CompleteTransition()
    {
        _inTransition = false;
        _index = _transitionTo;
        // Playback has arrived on a track, so its repeat, if it has one, is still
        // ahead. This is also what lets a looping track be entered twice in a row
        // without the second visit being treated as the repeat of the first.
        _loopPlayed = false;
        _positionOnTrack = _activeTransition.Kind == TransitionKind.Crossfade
            ? _activeTransition.Duration
            : TimeSpan.Zero;
        if (_index >= _mix.Tracks.Count)
            Stop();
    }

    private double Progress()
    {
        if (_activeTransition.Duration <= TimeSpan.Zero) return 1;
        var t = _transitionElapsed.TotalSeconds / _activeTransition.Duration.TotalSeconds;
        if (t < 0) return 0;
        if (t > 1) return 1;
        return t;
    }

    private static TimeSpan AutomaticStartOffset(MixTrack track, TrackTransition outgoing)
    {
        var hold = DurationMath.OutgoingHold(outgoing);
        var start = track.Duration - hold;
        return start < TimeSpan.Zero ? TimeSpan.Zero : start;
    }

    private TimeSpan MixPositionAt(int index, TimeSpan onTrack)
    {
        var total = TimeSpan.Zero;
        for (var i = 0; i < index && i < _mix.Tracks.Count; i++)
        {
            total += _mix.Tracks[i].TapeSpan;
            // The boundary crossed after track i belongs to track i + 1, which is
            // in range because i is below index and index is inside the list.
            var t = _mix.Tracks[i + 1].TransitionIn;
            total += t.AddedSilence;
            total -= t.SubtractedOverlap;
        }
        total += onTrack;
        return total < TimeSpan.Zero ? TimeSpan.Zero : total;
    }
}
