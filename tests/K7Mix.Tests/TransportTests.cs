using K7Mix.Core;

namespace K7Mix.Tests;

public class TransportTests
{
    [Fact]
    public void Timeline_subtracts_crossfade_and_adds_silence()
    {
        var mix = Mix(3, TimeSpan.FromSeconds(10));
        mix.Tracks[1].TransitionIn = new TrackTransition(TransitionKind.Crossfade, TimeSpan.FromSeconds(2));
        mix.Tracks[2].TransitionIn = new TrackTransition(TransitionKind.Silence, TimeSpan.FromSeconds(1));
        Assert.Equal(29, DurationMath.Timeline(mix).TotalSeconds, 3);
    }

    [Fact]
    public void Overlapping_transitions_are_errors()
    {
        var mix = Mix(3, TimeSpan.FromSeconds(5));
        mix.Tracks[1].TransitionIn = new TrackTransition(TransitionKind.Crossfade, TimeSpan.FromSeconds(3));
        mix.Tracks[2].TransitionIn = new TrackTransition(TransitionKind.Crossfade, TimeSpan.FromSeconds(3));
        var issues = DurationMath.ValidateTransitions(mix);
        Assert.True(issues.HasErrors);
    }

    [Fact]
    public void Over_120_minutes_blocks_export()
    {
        var mix = Mix(1, TimeSpan.FromMinutes(121));
        Assert.False(MixValidator.CanExport(mix, out var issues));
        Assert.Contains(issues.Items, i => i.Message.Contains("120-minute"));
    }

    [Fact]
    public void Over_90_minutes_is_a_warning()
    {
        var mix = Mix(1, TimeSpan.FromMinutes(95));
        Assert.True(MixValidator.CanExport(mix, out var issues));
        Assert.Contains(issues.Items, i => i.Severity == IssueSeverity.Warning && i.Message.Contains("90-minute"));
    }

    [Fact]
    public void Skip_starts_the_configured_transition()
    {
        var mix = Mix(2, TimeSpan.FromSeconds(10));
        mix.Tracks[1].TransitionIn = new TrackTransition(TransitionKind.Crossfade, TimeSpan.FromSeconds(2));
        var t = new MixTransport(mix);
        t.PlayPause();
        t.Advance(TimeSpan.FromSeconds(1));
        Assert.True(t.Next());
        Assert.True(t.IsTransitioning);
        Assert.Equal(0, t.DisplayedTrackIndex);
        Assert.False(t.Next());
        t.Advance(TimeSpan.FromSeconds(2));
        Assert.False(t.IsTransitioning);
        Assert.Equal(1, t.CurrentTrackIndex);
        Assert.Equal(1, t.DisplayedTrackIndex);
    }

    [Fact]
    public void None_transition_skip_is_zero_duration_not_a_gap()
    {
        var mix = Mix(2, TimeSpan.FromSeconds(10));
        mix.Tracks[1].TransitionIn = TrackTransition.None;
        var t = new MixTransport(mix);
        t.PlayPause();
        Assert.True(t.Next());
        Assert.False(t.IsTransitioning);
        Assert.Equal(1, t.CurrentTrackIndex);
        Assert.Equal(TimeSpan.Zero, t.PositionOnTrack);
    }

    [Fact]
    public void Automatic_transition_is_not_restarted_by_skip()
    {
        var mix = Mix(2, TimeSpan.FromSeconds(10));
        mix.Tracks[1].TransitionIn = new TrackTransition(TransitionKind.Crossfade, TimeSpan.FromSeconds(2));
        var t = new MixTransport(mix);
        t.PlayPause();
        t.Advance(TimeSpan.FromSeconds(8.5));
        Assert.True(t.IsTransitioning);
        Assert.False(t.Next());
        t.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, t.CurrentTrackIndex);
    }

    [Fact]
    public void The_end_of_the_tape_loops_back_instead_of_stopping()
    {
        // Playback used to end after the last track. The tape now wraps, so the
        // transport stays in Playing and the playhead returns to the first track.
        var mix = Mix(1, TimeSpan.FromSeconds(2));
        var t = new MixTransport(mix);
        t.PlayPause();
        t.Advance(TimeSpan.FromSeconds(2.5));
        Assert.Equal(TransportState.Playing, t.State);
        Assert.Equal(0, t.CurrentTrackIndex);
    }

    [Fact]
    public void The_loop_back_uses_the_first_tracks_own_transition()
    {
        var mix = Mix(2, TimeSpan.FromSeconds(10));
        mix.Tracks[0].TransitionIn = new TrackTransition(TransitionKind.Crossfade, TimeSpan.FromSeconds(2));
        var t = new MixTransport(mix);
        t.PlayPause();

        // Through the first track and up to where the loop back starts: the
        // crossfade is held back from the end of the last track, as at any boundary.
        t.Advance(TimeSpan.FromSeconds(18));
        Assert.Equal(1, t.CurrentTrackIndex);
        Assert.True(t.IsTransitioning);

        t.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, t.CurrentTrackIndex);
        Assert.False(t.IsTransitioning);
        Assert.Equal(TransportState.Playing, t.State);
    }

    [Fact]
    public void Next_on_the_last_track_wraps_to_the_first()
    {
        var mix = Mix(2, TimeSpan.FromSeconds(10));
        var t = new MixTransport(mix);
        t.PlayPause();
        Assert.True(t.Next());
        Assert.Equal(1, t.CurrentTrackIndex);
        Assert.True(t.Next());
        Assert.Equal(0, t.CurrentTrackIndex);
    }

    [Fact]
    public void A_looping_track_plays_through_twice_before_the_tape_moves_on()
    {
        var mix = Mix(2, TimeSpan.FromSeconds(10));
        mix.Tracks[0].Loop = true;
        var t = new MixTransport(mix);
        t.PlayPause();

        t.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(0, t.CurrentTrackIndex);
        Assert.False(t.OnLoopRepeat);

        // Crossing the end of the first pass restarts the same track. The repeat is
        // seamless: no transition runs, because no boundary is crossed.
        t.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, t.CurrentTrackIndex);
        Assert.True(t.OnLoopRepeat);
        Assert.False(t.IsTransitioning);
        Assert.Equal(1, t.PositionOnTrack.TotalSeconds, 3);

        // The second pass ends at the boundary like any other track.
        t.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(1, t.CurrentTrackIndex);
        Assert.False(t.OnLoopRepeat);
    }

    [Fact]
    public void The_progress_span_covers_both_passes_of_a_looping_track()
    {
        var mix = Mix(2, TimeSpan.FromSeconds(10));
        mix.Tracks[0].Loop = true;
        var t = new MixTransport(mix);
        t.PlayPause();
        Assert.Equal(20, t.CurrentTrackSpan.TotalSeconds, 3);
        Assert.Equal(0.5, t.LoopMarker);

        // Eleven seconds in, the playhead is one second into the repeat, which reads
        // as just past the halfway mark rather than snapping back to the left.
        t.Advance(TimeSpan.FromSeconds(11));
        Assert.Equal(11, t.PositionInSpan.TotalSeconds, 3);
        Assert.Equal(1, t.PositionOnTrack.TotalSeconds, 3);

        t.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, t.CurrentTrackIndex);
        Assert.Equal(10, t.CurrentTrackSpan.TotalSeconds, 3);
        Assert.Null(t.LoopMarker);
    }

    [Fact]
    public void A_looping_track_counts_twice_towards_the_tape_length()
    {
        var mix = Mix(2, TimeSpan.FromSeconds(10));
        mix.Tracks[0].Loop = true;
        Assert.Equal(30, DurationMath.Timeline(mix).TotalSeconds, 3);
    }

    [Fact]
    public void The_loop_back_is_not_part_of_the_tape_length()
    {
        // One pass through the tape never crosses the first track's transition, so
        // its silence must not be added to the printed length.
        var mix = Mix(2, TimeSpan.FromSeconds(10));
        mix.Tracks[0].TransitionIn = new TrackTransition(TransitionKind.Silence, TimeSpan.FromSeconds(5));
        Assert.Equal(20, DurationMath.Timeline(mix).TotalSeconds, 3);
    }

    [Fact]
    public void Side_changes_with_displayed_track()
    {
        var mix = Mix(3, TimeSpan.FromSeconds(10));
        mix.SideBStartTrack = 2;
        var t = new MixTransport(mix);
        t.PlayPause();
        Assert.Equal(0, t.DisplayedSide);
        t.Next();
        Assert.Equal(1, t.DisplayedSide);
    }

    [Fact]
    public void Codename_request_is_not_instant_and_is_ignored_during_transition()
    {
        var mix = Mix(3, TimeSpan.FromSeconds(10));
        mix.Tracks[0].Codename = "forest";
        mix.Tracks[1].Codename = "desert";
        mix.Tracks[2].Codename = "pyramid";
        mix.Tracks[2].TransitionIn = new TrackTransition(TransitionKind.Crossfade, TimeSpan.FromSeconds(2));
        var t = new MixTransport(mix);
        t.PlayPause();
        Assert.True(t.RequestCodename("pyramid"));
        Assert.True(t.IsTransitioning);
        Assert.False(t.RequestCodename("desert"));
        t.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(2, t.CurrentTrackIndex);
    }

    [Fact]
    public void Codename_request_behind_the_playhead_lands_on_the_requested_track()
    {
        // RequestCodename may target a track behind the playhead, so the track index
        // is not monotonic. Advance walks boundaries iteratively to stay correct for
        // either direction.
        var mix = Mix(4, TimeSpan.FromSeconds(10));
        mix.Tracks[0].Codename = "forest";
        mix.Tracks[3].Codename = "pyramid";
        mix.Tracks[0].TransitionIn = new TrackTransition(TransitionKind.Crossfade, TimeSpan.FromSeconds(2));
        var t = new MixTransport(mix);
        t.PlayPause();
        t.Advance(TimeSpan.FromSeconds(35)); // now on the last track
        Assert.Equal(3, t.CurrentTrackIndex);

        Assert.True(t.RequestCodename("forest"));
        t.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, t.CurrentTrackIndex);
        Assert.False(t.IsTransitioning);
    }

    [Fact]
    public void Crossfade_into_a_shorter_track_does_not_invent_playback_time()
    {
        // A finished crossfade parks the playhead at the transition duration, which can
        // sit past a shorter incoming track. That made the remaining time negative and
        // the leftover larger than the delta that came in, so the playhead ran ahead of
        // the clock: 30s of playback used to land at 23s into track 3 instead of 20s.
        var mix = Mix(3, TimeSpan.FromSeconds(1));
        mix.Tracks[0].Duration = TimeSpan.FromSeconds(10);
        mix.Tracks[1].TransitionIn = new TrackTransition(TransitionKind.Crossfade, TimeSpan.FromSeconds(4));
        mix.Tracks[2].Duration = TimeSpan.FromSeconds(100);
        var t = new MixTransport(mix);
        t.PlayPause();
        t.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(2, t.CurrentTrackIndex);
        Assert.Equal(20, t.PositionOnTrack.TotalSeconds, 3);
    }

    [Fact]
    public void Duplicate_codenames_fail_validation()
    {
        var mix = Mix(2, TimeSpan.FromSeconds(1));
        mix.Tracks[0].Codename = "forest";
        mix.Tracks[1].Codename = "forest";
        Assert.False(MixValidator.CanExport(mix, out var issues));
        Assert.Contains(issues.Items, i => i.Message.Contains("unique"));
    }

    [Fact]
    public void Reorder_keeps_metadata_and_moves_the_transition_with_the_track()
    {
        var mix = Mix(3, TimeSpan.FromSeconds(5));
        mix.Tracks[0].Title = "A";
        mix.Tracks[0].Codename = "a";
        mix.Tracks[0].TransitionIn = new TrackTransition(TransitionKind.Silence, TimeSpan.FromSeconds(1));
        mix.Tracks[1].Title = "B";
        mix.Tracks[2].Title = "C";
        mix.MoveTrack(0, 2);
        Assert.Equal("B", mix.Tracks[0].Title);
        Assert.Equal("C", mix.Tracks[1].Title);
        Assert.Equal("A", mix.Tracks[2].Title);
        Assert.Equal("a", mix.Tracks[2].Codename);
        Assert.Equal(TransitionKind.Silence, mix.Tracks[2].TransitionIn.Kind);
    }

    [Fact]
    public void Play_pause_and_stop_work_during_a_transition()
    {
        var mix = Mix(2, TimeSpan.FromSeconds(10));
        mix.Tracks[1].TransitionIn = new TrackTransition(TransitionKind.Crossfade, TimeSpan.FromSeconds(4));
        var t = new MixTransport(mix);
        t.PlayPause();
        t.Next();
        Assert.True(t.IsTransitioning);
        t.PlayPause();
        Assert.Equal(TransportState.Paused, t.State);
        Assert.True(t.IsTransitioning);
        t.PlayPause();
        Assert.Equal(TransportState.Playing, t.State);
        t.Stop();
        Assert.Equal(TransportState.Stopped, t.State);
        Assert.False(t.IsTransitioning);
    }

    private static MixDocument Mix(int tracks, TimeSpan duration)
    {
        var mix = new MixDocument { Title = "Test" };
        for (var i = 0; i < tracks; i++)
        {
            mix.Tracks.Add(new MixTrack
            {
                Title = $"T{i + 1}",
                Duration = duration,
            });
        }
        return mix;
    }
}
