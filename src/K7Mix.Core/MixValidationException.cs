namespace K7Mix.Core;

public sealed class MixValidationException : Exception
{
    public MixValidationException(string message) : base(message) { }
}

public enum IssueSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record MixIssue(IssueSeverity Severity, string Message, string? Field = null)
{
    public bool BlocksExport => Severity == IssueSeverity.Error;
}

public sealed class MixIssueList
{
    private readonly List<MixIssue> _items = [];

    public IReadOnlyList<MixIssue> Items => _items;
    public bool HasErrors => _items.Exists(i => i.BlocksExport);
    public bool HasWarnings => _items.Exists(i => i.Severity == IssueSeverity.Warning);

    public void Info(string message, string? field = null) =>
        _items.Add(new MixIssue(IssueSeverity.Info, message, field));

    public void Warn(string message, string? field = null) =>
        _items.Add(new MixIssue(IssueSeverity.Warning, message, field));

    public void Error(string message, string? field = null) =>
        _items.Add(new MixIssue(IssueSeverity.Error, message, field));

    public override string ToString() =>
        string.Join(Environment.NewLine, _items.Select(i => $"{i.Severity}: {i.Message}"));
}
