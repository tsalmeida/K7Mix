using System.Globalization;
using System.Text.RegularExpressions;

namespace K7Mix.Core;

public enum TransitionKind
{
    None,
    Crossfade,
    FadeOut,
    Silence,
}

/// <summary>
/// The boundary from one track to the next. Stored on the incoming track, so after
/// a reorder it stays with the track it leads into. The first track's is the loop
/// back from the end of the tape.
/// </summary>
public sealed record TrackTransition(TransitionKind Kind, TimeSpan Duration)
{
    public static TrackTransition None { get; } = new(TransitionKind.None, TimeSpan.Zero);

    public bool IsInstant => Kind == TransitionKind.None || Duration <= TimeSpan.Zero;

    public TimeSpan AddedSilence => Kind == TransitionKind.Silence ? Duration : TimeSpan.Zero;

    public TimeSpan SubtractedOverlap => Kind == TransitionKind.Crossfade ? Duration : TimeSpan.Zero;

    public string ToToken()
    {
        if (Kind == TransitionKind.None) return "none";
        var seconds = Duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        return Kind switch
        {
            TransitionKind.Crossfade => $"crossfade {seconds}s",
            TransitionKind.FadeOut => $"fadeout {seconds}s",
            TransitionKind.Silence => $"silence {seconds}s",
            _ => "none",
        };
    }

    public override string ToString() => ToToken();
}

public static class TransitionParser
{
    private static readonly Regex Timed = new(
        @"^(crossfade|fadeout|silence)\s+(\d+(?:\.\d+)?)\s*s$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static TrackTransition Parse(string? token, int boundaryIndex)
    {
        var text = (token ?? "").Trim();
        if (text.Length == 0 || text.Equals("none", StringComparison.OrdinalIgnoreCase))
            return TrackTransition.None;

        var match = Timed.Match(text);
        if (!match.Success)
        {
            throw new MixValidationException(
                $"Transition {boundaryIndex + 1} is not recognised: '{token}'. " +
                "Use none, crossfade Ds, fadeout Ds or silence Ds.");
        }

        var seconds = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        if (seconds <= 0)
        {
            throw new MixValidationException(
                $"Transition {boundaryIndex + 1} duration must be a positive number of seconds.");
        }

        var kind = match.Groups[1].Value.ToLowerInvariant() switch
        {
            "crossfade" => TransitionKind.Crossfade,
            "fadeout" => TransitionKind.FadeOut,
            "silence" => TransitionKind.Silence,
            _ => TransitionKind.None,
        };
        return new TrackTransition(kind, TimeSpan.FromSeconds(seconds));
    }

    /// <summary>
    /// Parse the 'Transitions in csv format' field. One value per track, in track
    /// order: value <em>i</em> is the transition into track <em>i</em>, so the first
    /// is the loop back to the start. Missing trailing values default to none;
    /// excess trailing values are ignored. Empty cells between commas are none.
    /// </summary>
    public static TransitionImportResult ParseList(string? csv, int trackCount)
    {
        var needed = Math.Max(trackCount, 0);
        var cells = SplitKeepingEmpties(csv);
        var issues = new MixIssueList();
        var transitions = new List<TrackTransition>(needed);

        if (cells.Count < needed)
        {
            issues.Info(
                $"Transitions in csv format supplied {cells.Count} value(s) for {needed} track(s); " +
                $"{needed - cells.Count} trailing transition(s) defaulted to none.");
        }
        else if (cells.Count > needed)
        {
            issues.Info(
                $"Transitions in csv format supplied {cells.Count} value(s) for {needed} track(s); " +
                $"{cells.Count - needed} trailing value(s) ignored.");
        }

        for (var i = 0; i < needed; i++)
        {
            var token = i < cells.Count ? cells[i] : "";
            try
            {
                transitions.Add(Parse(token, i));
            }
            catch (MixValidationException ex)
            {
                issues.Error(ex.Message, "Transitions in csv format");
                transitions.Add(TrackTransition.None);
            }
        }

        return new TransitionImportResult(transitions, issues);
    }

    private static List<string> SplitKeepingEmpties(string? csv)
    {
        if (csv is null) return [];
        var trimmed = csv.Trim();
        if (trimmed.Length == 0) return [];
        return trimmed.Split(',').Select(s => s.Trim()).ToList();
    }
}

public sealed record TransitionImportResult(
    IReadOnlyList<TrackTransition> Transitions,
    MixIssueList Issues);
