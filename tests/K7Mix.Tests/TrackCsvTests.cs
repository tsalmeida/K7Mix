using K7Mix.Core;

namespace K7Mix.Tests;

public class TrackCsvTests
{
    [Fact]
    public void Quotes_commas_quotes_and_line_breaks()
    {
        var csv = "filename,title,codename,license,source\n"
                  + "\"01, forest.wav\",\"He said \"\"go\"\"\",deep-forest,CC0,\"line 1\nline 2\"\n";
        var issues = new MixIssueList();
        var rows = TrackCsv.Parse(csv, issues);
        Assert.Single(rows);
        Assert.Equal("01, forest.wav", rows[0].Filename);
        Assert.Equal("He said \"go\"", rows[0].Title);
        Assert.Equal("deep-forest", rows[0].Codename);
        Assert.Contains("line 1", rows[0].Source);
        Assert.Contains("line 2", rows[0].Source);
        Assert.False(issues.HasErrors);
    }
}
