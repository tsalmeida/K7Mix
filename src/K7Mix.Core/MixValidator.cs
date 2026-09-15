using System.Text.RegularExpressions;

namespace K7Mix.Core;

public static class MixValidator
{
    private static readonly Regex CodenamePattern = new(
        @"^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static MixIssueList Validate(MixDocument mix)
    {
        var issues = new MixIssueList();
        if (string.IsNullOrWhiteSpace(mix.Title))
            issues.Error("The mix needs a title.", "Title");

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < mix.Tracks.Count; i++)
        {
            var track = mix.Tracks[i];
            if (string.IsNullOrWhiteSpace(track.Title))
                issues.Warn($"Track {i + 1} has no title.");

            if (!string.IsNullOrWhiteSpace(track.Codename))
            {
                if (!CodenamePattern.IsMatch(track.Codename))
                {
                    issues.Error(
                        $"Track {i + 1} codename '{track.Codename}' must be lowercase letters, digits and hyphens.",
                        "codename");
                }
                if (seen.TryGetValue(track.Codename, out var first))
                {
                    issues.Error(
                        $"Codename '{track.Codename}' is used by track {first + 1} and track {i + 1}. " +
                        "Nonempty codenames must be unique within a mix.",
                        "codename");
                }
                else seen[track.Codename] = i;
            }

            if (track.Duration <= TimeSpan.Zero)
                issues.Error($"Track {i + 1} ('{track.Title}') has no duration.");
        }

        if (mix.SideBStartTrack is { } start)
        {
            if (start < 1 || start > mix.Tracks.Count)
            {
                issues.Error(
                    $"Side B starting track is {start}, which is outside 1..{Math.Max(mix.Tracks.Count, 1)}.",
                    "Side B starting track");
            }
        }

        foreach (var issue in DurationMath.ValidateTransitions(mix).Items)
            issues.Error(issue.Message, issue.Field);

        var timeline = DurationMath.Timeline(mix);
        if (timeline > PlaybackProfile.HardMaximum)
        {
            issues.Error(
                $"The mix is {FormatMinutes(timeline)}, which is over the 120-minute hard maximum. Export is blocked. " +
                "The limit is the complete playback timeline, including inserted silence and subtracting crossfade overlaps. " +
                "It never changes audio quality.");
        }
        else if (timeline > PlaybackProfile.PreferredMaximum)
        {
            issues.Warn(
                $"The mix is {FormatMinutes(timeline)}, longer than the preferred 90-minute cassette length. " +
                "Export is still allowed up to 120 minutes.");
        }

        if (mix.Locked)
            issues.Info(LegalNotice.LockIsNotEncryption);

        issues.Info(LegalNotice.PackagingDoesNotGrantPermission);
        return issues;
    }

    public static string FormatMinutes(TimeSpan time)
    {
        var total = time.TotalMinutes;
        var minutes = (int)Math.Floor(total);
        var seconds = (int)Math.Round((total - minutes) * 60);
        if (seconds == 60)
        {
            minutes++;
            seconds = 0;
        }
        return $"{minutes}:{seconds:00}";
    }

    public static bool CanExport(MixDocument mix, out MixIssueList issues)
    {
        issues = Validate(mix);
        if (mix.Tracks.Count == 0)
        {
            issues.Error("A mix needs at least one track before it can be packaged.");
        }
        return !issues.HasErrors;
    }
}
