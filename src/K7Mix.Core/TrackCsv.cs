using System.Text;

namespace K7Mix.Core;

public static class TrackCsv
{
    public static readonly string[] Header =
        ["filename", "title", "codename", "license", "source", "loop"];

    public static IReadOnlyList<TrackCsvRow> Parse(string csv, MixIssueList issues)
    {
        var rows = new List<TrackCsvRow>();
        var records = ReadRecords(csv);
        if (records.Count == 0) return rows;

        var start = 0;
        if (LooksLikeHeader(records[0])) start = 1;

        for (var i = start; i < records.Count; i++)
        {
            var cells = records[i];
            if (cells.Count == 0 || cells.All(string.IsNullOrWhiteSpace)) continue;
            var filename = Cell(cells, 0);
            if (string.IsNullOrWhiteSpace(filename))
            {
                issues.Error($"Tracks in csv format row {i + 1} has no filename.");
                continue;
            }

            rows.Add(new TrackCsvRow(
                filename.Trim(),
                Cell(cells, 1).Trim(),
                Cell(cells, 2).Trim(),
                Cell(cells, 3).Trim(),
                Cell(cells, 4).Trim(),
                IsLoop(Cell(cells, 5))));
        }

        return rows;
    }

    public static string Write(IEnumerable<TrackCsvRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Header));
        foreach (var row in rows)
        {
            sb.Append(Quote(row.Filename)).Append(',');
            sb.Append(Quote(row.Title)).Append(',');
            sb.Append(Quote(row.Codename)).Append(',');
            sb.Append(Quote(row.License)).Append(',');
            sb.Append(Quote(row.Source)).Append(',');
            sb.AppendLine(row.Loop ? "yes" : "");
        }
        return sb.ToString();
    }

    /// <summary>
    /// The loop column is optional and its absence means no loop, so anything that
    /// is not an affirmative reads as off rather than as an error.
    /// </summary>
    private static bool IsLoop(string cell) =>
        cell.Trim() is var text &&
        (text.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
         text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
         text.Equals("loop", StringComparison.OrdinalIgnoreCase) ||
         text == "1");

    private static bool LooksLikeHeader(IReadOnlyList<string> cells) =>
        cells.Count > 0 && cells[0].Trim().Equals("filename", StringComparison.OrdinalIgnoreCase);

    private static string Cell(IReadOnlyList<string> cells, int index) =>
        index < cells.Count ? cells[index] : "";

    private static List<List<string>> ReadRecords(string csv)
    {
        var records = new List<List<string>>();
        var current = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    current.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    current.Add(field.ToString());
                    field.Clear();
                    records.Add(current);
                    current = [];
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (inQuotes || field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString());
            records.Add(current);
        }

        return records;
    }

    private static string Quote(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

public sealed record TrackCsvRow(
    string Filename,
    string Title,
    string Codename,
    string License,
    string Source,
    bool Loop = false);
