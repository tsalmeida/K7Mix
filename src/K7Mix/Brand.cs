using System.Reflection;

namespace K7Mix;

internal static class Brand
{
    public const string ProjectName = "K7Mix";
    public const string ProductName = "K7 Mix";

    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
}
