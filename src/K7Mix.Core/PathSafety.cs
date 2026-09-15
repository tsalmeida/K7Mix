namespace K7Mix.Core;

public static class PathSafety
{
    public static string ResolveInside(string root, string relative, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(relative))
            throw new MixValidationException($"{fieldName} is empty.");

        var rootFull = Path.GetFullPath(root);
        if (!rootFull.EndsWith(Path.DirectorySeparatorChar))
            rootFull += Path.DirectorySeparatorChar;

        var combined = Path.GetFullPath(Path.Combine(rootFull, relative));
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new MixValidationException(
                $"{fieldName} '{relative}' leaves the source folder.");
        }

        return combined;
    }

    public static bool TryResolveInside(string root, string relative, string fieldName,
        out string fullPath, out string? error)
    {
        try
        {
            fullPath = ResolveInside(root, relative, fieldName);
            error = null;
            return true;
        }
        catch (MixValidationException ex)
        {
            fullPath = "";
            error = ex.Message;
            return false;
        }
    }
}
