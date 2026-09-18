namespace K7Mix.Core;

public static class DurationMath
{
    /// <summary>
    /// The length of one pass through the tape. The first track's transition is the
    /// loop back to the start, and one pass never crosses it, so it is left out.
    /// </summary>
    public static TimeSpan Timeline(MixDocument mix)
    {
        if (mix.Tracks.Count == 0) return TimeSpan.Zero;
        var total = TimeSpan.Zero;
        for (var i = 0; i < mix.Tracks.Count; i++)
        {
            total += mix.Tracks[i].TapeSpan;
            if (i > 0)
            {
                var t = mix.Tracks[i].TransitionIn;
                total += t.AddedSilence;
                total -= t.SubtractedOverlap;
            }
        }
        if (total < TimeSpan.Zero) total = TimeSpan.Zero;
        return total;
    }

    public static MixIssueList ValidateTransitions(MixDocument mix)
    {
        var issues = new MixIssueList();
        var tracks = mix.Tracks;
        if (tracks.Count == 0) return issues;

        // Every track owns the boundary that brings playback into it. The first
        // track's outgoing side is the last track, because that boundary is the
        // loop back to the start.
        for (var i = 0; i < tracks.Count; i++)
        {
            var incoming = tracks[i];
            var outgoing = tracks[i == 0 ? tracks.Count - 1 : i - 1];
            var t = incoming.TransitionIn;
            var name = i == 0
                ? "The loop back to the start"
                : $"The transition into '{incoming.Title}'";

            if (t.Kind == TransitionKind.Crossfade)
            {
                if (t.Duration > outgoing.Duration)
                {
                    issues.Error(
                        $"{name} ({t.Duration.TotalSeconds:0.###}s) is longer than the outgoing " +
                        $"track '{outgoing.Title}' ({outgoing.Duration.TotalSeconds:0.###}s).");
                }
                if (t.Duration > incoming.Duration)
                {
                    issues.Error(
                        $"{name} ({t.Duration.TotalSeconds:0.###}s) is longer than " +
                        $"'{incoming.Title}' ({incoming.Duration.TotalSeconds:0.###}s).");
                }
            }
            else if (t.Kind == TransitionKind.FadeOut && t.Duration > outgoing.Duration)
            {
                issues.Error(
                    $"{name} ({t.Duration.TotalSeconds:0.###}s) is longer than the outgoing " +
                    $"track '{outgoing.Title}' ({outgoing.Duration.TotalSeconds:0.###}s).");
            }
        }

        // What the incoming boundary holds at a track's start plus what the outgoing
        // one holds at its end has to fit inside the track. Now that the tape loops,
        // the first and last tracks have both halves too.
        for (var i = 0; i < tracks.Count; i++)
        {
            var incoming = tracks[i].TransitionIn;
            var outgoing = tracks[mix.NextIndex(i)].TransitionIn;
            var incomingHold = IncomingHold(incoming);
            var outgoingHold = OutgoingHold(outgoing);
            if (incomingHold + outgoingHold > tracks[i].TapeSpan)
            {
                issues.Error(
                    $"Transitions overlap on '{tracks[i].Title}': incoming {incoming} and outgoing {outgoing} " +
                    $"need {(incomingHold + outgoingHold).TotalSeconds:0.###}s of a " +
                    $"{tracks[i].TapeSpan.TotalSeconds:0.###}s track. Shorten one of them.");
            }
        }

        return issues;
    }

    public static TimeSpan IncomingHold(TrackTransition incoming) =>
        incoming.Kind == TransitionKind.Crossfade ? incoming.Duration : TimeSpan.Zero;

    public static TimeSpan OutgoingHold(TrackTransition outgoing) => outgoing.Kind switch
    {
        TransitionKind.Crossfade => outgoing.Duration,
        TransitionKind.FadeOut => outgoing.Duration,
        _ => TimeSpan.Zero,
    };
}
