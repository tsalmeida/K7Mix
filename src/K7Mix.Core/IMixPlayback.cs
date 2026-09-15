namespace K7Mix.Core;

/// <summary>
/// Minimal playback surface for a game. RunningWild should expose the same
/// operations from C. Nonempty codenames are unique. A codename request is
/// never an instantaneous skip: it starts the configured transition from the
/// current track, and is ignored while a transition is already running.
/// </summary>
public interface IMixPlayback
{
    void Load(string k7mPath);
    MixDocument Mix { get; }
    void PlayPause();
    void Stop();
    bool Next();
    bool RequestCodename(string codename);
    int CurrentTrackIndex { get; }
    int DisplayedSide { get; }
    bool IsPlaying { get; }
    bool IsTransitioning { get; }
    MixTrack? TrackByCodename(string codename);
}
