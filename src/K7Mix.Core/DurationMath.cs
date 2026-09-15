namespace K7Mix.Core;

public static class DurationMath
{
    public static TimeSpan Timeline(MixDocument mix)
    {
        if (mix.Tracks.Count == 0) return TimeSpan.Zero;
        var total = TimeSpan.Zero;
        for (var i = 0; i < mix.Tracks.Count; i++)
        {
            total += mix.Tracks[i].Duration;
            if (i < mix.Tracks.Count - 1)
            {
                var t = mix.Tracks[i].TransitionOut;
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

        for (var i = 0; i < tracks.Count - 1; i++)
        {
            var outgoing = tracks[i];
            var incoming = tracks[i + 1];
            var t = outgoing.TransitionOut;

            if (t.Kind == TransitionKind.Crossfade)
            {
                if (t.Duration > outgoing.Duration)
                {
                    issues.Error(
                        $"Crossfade after '{outgoing.Title}' ({t.Duration.TotalSeconds:0.###}s) " +
                        $"is longer than the outgoing track ({outgoing.Duration.TotalSeconds:0.###}s).");
                }
                if (t.Duration > incoming.Duration)
                {
                    issues.Error(
                        $"Crossfade after '{outgoing.Title}' ({t.Duration.TotalSeconds:0.###}s) " +
                        $"is longer than the incoming track '{incoming.Title}' " +
                        $"({incoming.Duration.TotalSeconds:0.###}s).");
                }
            }
            else if (t.Kind == TransitionKind.FadeOut && t.Duration > outgoing.Duration)
            {
                issues.Error(
                    $"Fade-out after '{outgoing.Title}' ({t.Duration.TotalSeconds:0.###}s) " +
                    $"is longer than that track ({outgoing.Duration.TotalSeconds:0.###}s).");
            }
        }

        for (var i = 1; i < tracks.Count - 1; i++)
        {
            var incoming = tracks[i - 1].TransitionOut;
            var outgoing = tracks[i].TransitionOut;
            var incomingHold = IncomingHold(incoming);
            var outgoingHold = OutgoingHold(outgoing);
            if (incomingHold + outgoingHold > tracks[i].Duration)
            {
                issues.Error(
                    $"Transitions overlap on '{tracks[i].Title}': incoming {incoming} and outgoing {outgoing} " +
                    $"need {(incomingHold + outgoingHold).TotalSeconds:0.###}s of a " +
                    $"{tracks[i].Duration.TotalSeconds:0.###}s track. Shorten one of them.");
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
