using System.Text;

namespace K7Mix.Core;

/// <summary>
/// Tekhnexos Markdown import, contract version 1 — independent of the .k7m
/// container version. A standalone entry uses a level-one heading as the mix
/// title and level-two headings as fields. A dictionary export (H1 dictionary,
/// H2 entry title, H3 fields) is accepted when it contains a single entry.
/// </summary>
public static class MarkdownImporter
{
    public const int ContractVersion = 1;

    public static readonly IReadOnlyList<string> RecognisedFields =
    [
        "Description",
        "Link",
        "Text for side A",
        "Text for side B",
        "Font",
        "Text color",
        "Tape color",
        "Sticker color",
        "Transitions in csv format",
        "License",
        "Tracks in csv format",
        "Front artwork",
        "Back artwork",
        "Side B starting track",
    ];

    public static MarkdownImportResult Import(string markdown, string sourceFolder)
    {
        var issues = new MixIssueList();
        var document = new MixDocument
        {
            SourceMarkdownSnapshot = markdown,
        };

        var parsed = ParseFields(markdown, issues);
        if (string.IsNullOrWhiteSpace(parsed.Title))
            issues.Error("The Markdown file has no level-one heading for the mix title.");
        else
            document.Title = parsed.Title.Trim();

        ApplyRecognised(parsed.Fields, document, issues);
        ApplyTracks(parsed.Fields, document, sourceFolder, issues);
        ApplyArtwork(parsed.Fields, document, sourceFolder, issues);

        foreach (var kv in parsed.Unknown)
            document.UnknownFields[kv.Key] = kv.Value;

        if (parsed.Unknown.Count > 0)
        {
            issues.Info(
                "Unknown fields were preserved and are not used for playback: " +
                string.Join(", ", parsed.Unknown.Keys) + ".");
        }

        var needed = Math.Max(document.Tracks.Count - 1, 0);
        parsed.Fields.TryGetValue("Transitions in csv format", out var transitionCsv);
        var transitions = TransitionParser.ParseList(transitionCsv, document.Tracks.Count);
        foreach (var issue in transitions.Issues.Items)
        {
            if (issue.Severity == IssueSeverity.Error) issues.Error(issue.Message, issue.Field);
            else issues.Info(issue.Message, issue.Field);
        }

        for (var i = 0; i < needed; i++)
            document.Tracks[i].TransitionOut = transitions.Transitions[i];

        return new MarkdownImportResult(document, issues, parsed);
    }

    private static void ApplyRecognised(
        IReadOnlyDictionary<string, string> fields,
        MixDocument document,
        MixIssueList issues)
    {
        document.Description = Get(fields, "Description");
        document.SourceUrl = Get(fields, "Link");
        document.SideALabel = Get(fields, "Text for side A");
        document.SideBLabel = Get(fields, "Text for side B");
        document.License = Get(fields, "License");

        if (fields.TryGetValue("Font", out var fontRaw) && fontRaw.Length > 0)
        {
            try { document.Appearance.Font = CassetteFont.Parse(fontRaw); }
            catch (MixValidationException ex) { issues.Error(ex.Message, "Font"); }
        }

        ParseColor(fields, "Text color", c => document.Appearance.TextColor = c, issues);
        ParseColor(fields, "Tape color", c => document.Appearance.TapeColor = c, issues);
        ParseColor(fields, "Sticker color", c => document.Appearance.StickerColor = c, issues);

        if (fields.TryGetValue("Side B starting track", out var sideB) && sideB.Length > 0)
        {
            if (int.TryParse(sideB, out var n) && n >= 1)
                document.SideBStartTrack = n;
            else
                issues.Error($"Side B starting track must be a one-based track number, not '{sideB}'.",
                    "Side B starting track");
        }
    }

    private static void ApplyTracks(
        IReadOnlyDictionary<string, string> fields,
        MixDocument document,
        string sourceFolder,
        MixIssueList issues)
    {
        if (!fields.TryGetValue("Tracks in csv format", out var csv) || csv.Length == 0)
        {
            issues.Info("No 'Tracks in csv format' field. Tracks can be added in K7 Mix.");
            return;
        }

        var rows = TrackCsv.Parse(csv, issues);
        foreach (var row in rows)
        {
            if (!PathSafety.TryResolveInside(sourceFolder, row.Filename, "filename", out var full, out var error))
            {
                issues.Error(error ?? $"Could not resolve '{row.Filename}'.");
                continue;
            }
            if (!File.Exists(full))
            {
                issues.Error($"Audio file not found: {row.Filename}");
                continue;
            }

            var title = row.Title.Length > 0 ? row.Title : Path.GetFileNameWithoutExtension(row.Filename);
            document.Tracks.Add(new MixTrack
            {
                Title = title,
                Codename = row.Codename,
                License = row.License,
                Source = row.Source,
                OriginalSourcePath = full,
                SourceExtension = Path.GetExtension(full),
            });
        }
    }

    private static void ApplyArtwork(
        IReadOnlyDictionary<string, string> fields,
        MixDocument document,
        string sourceFolder,
        MixIssueList issues)
    {
        SetArt(fields, "Front artwork", sourceFolder, p => document.Appearance.FrontArtworkPath = p, issues);
        SetArt(fields, "Back artwork", sourceFolder, p => document.Appearance.BackArtworkPath = p, issues);
    }

    private static void SetArt(
        IReadOnlyDictionary<string, string> fields,
        string name,
        string sourceFolder,
        Action<string> assign,
        MixIssueList issues)
    {
        if (!fields.TryGetValue(name, out var relative) || relative.Length == 0) return;
        if (!PathSafety.TryResolveInside(sourceFolder, relative, name, out var full, out var error))
        {
            issues.Error(error ?? $"{name} could not be resolved.");
            return;
        }
        if (!File.Exists(full))
        {
            issues.Error($"{name} file not found: {relative}", name);
            return;
        }
        assign(full);
    }

    private static void ParseColor(
        IReadOnlyDictionary<string, string> fields,
        string name,
        Action<HexColor> assign,
        MixIssueList issues)
    {
        if (!fields.TryGetValue(name, out var raw) || raw.Length == 0) return;
        if (HexColor.TryParse(raw, out var color)) assign(color);
        else issues.Error($"{name} must be a hexadecimal colour such as #000000, not '{raw}'.", name);
    }

    private static string Get(IReadOnlyDictionary<string, string> fields, string name) =>
        fields.TryGetValue(name, out var value) ? value : "";

    public static ParsedMarkdown ParseFields(string markdown, MixIssueList issues)
    {
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string? title = null;
        var fields = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var unknown = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? current = null;
        var sawH1 = false;
        var entryHeadingCount = 0;
        var fieldLevel = 2;

        foreach (var raw in lines)
        {
            var line = raw;
            if (IsHeading(line, 1, out var h1))
            {
                if (!sawH1)
                {
                    title = h1;
                    sawH1 = true;
                }
                else
                {
                    issues.Warn("A second level-one heading was ignored.");
                }
                current = null;
                continue;
            }

            if (IsHeading(line, 2, out var h2))
            {
                if (IsRecognised(h2) || current is not null || entryHeadingCount > 0)
                {
                    BeginField(h2, fields, order, issues, ref current);
                }
                else
                {
                    // Dictionary export: H2 is the entry title.
                    entryHeadingCount++;
                    if (entryHeadingCount == 1 && string.IsNullOrWhiteSpace(title) is false)
                        title = h2;
                    else if (entryHeadingCount > 1)
                        issues.Error("This Markdown file contains more than one entry. Import a standalone entry.");
                    fieldLevel = 3;
                    current = null;
                }
                continue;
            }

            if (IsHeading(line, 3, out var h3) && fieldLevel >= 3)
            {
                BeginField(h3, fields, order, issues, ref current);
                continue;
            }

            if (current is null) continue;
            fields[current].AppendLine(line);
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in order)
        {
            var text = TrimField(fields[key].ToString());
            if (IsRecognised(key)) values[key] = text;
            else unknown[key] = text;
        }

        return new ParsedMarkdown(title ?? "", values, unknown);
    }

    private static void BeginField(
        string name,
        Dictionary<string, StringBuilder> fields,
        List<string> order,
        MixIssueList issues,
        ref string? current)
    {
        if (fields.ContainsKey(name) && IsRecognised(name))
        {
            issues.Error($"Field '{name}' appears more than once, so its value is ambiguous.", name);
        }
        if (!fields.ContainsKey(name))
        {
            fields[name] = new StringBuilder();
            order.Add(name);
        }
        current = name;
    }

    private static bool IsRecognised(string name) =>
        RecognisedFields.Any(f => f.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static bool IsHeading(string line, int level, out string title)
    {
        title = "";
        var prefix = new string('#', level) + " ";
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith(prefix, StringComparison.Ordinal)) return false;
        if (level < 6 && trimmed.StartsWith(new string('#', level + 1) + " ", StringComparison.Ordinal))
            return false;
        title = trimmed[prefix.Length..].Trim();
        return title.Length > 0;
    }

    private static string TrimField(string value)
    {
        var text = value.Replace("\r\n", "\n");
        return text.Trim('\n');
    }
}

public sealed record ParsedMarkdown(
    string Title,
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyDictionary<string, string> Unknown);

public sealed record MarkdownImportResult(
    MixDocument Document,
    MixIssueList Issues,
    ParsedMarkdown Parsed);
