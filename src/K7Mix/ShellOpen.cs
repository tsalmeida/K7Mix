using System.Diagnostics;

namespace K7Mix;

/// <summary>
/// Opens files and folders through the operating system's shell associations.
/// No particular file manager or file type handler is selected by K7 Mix.
/// </summary>
internal static class ShellOpen
{
    /// <summary>Open a file or folder with its OS-default application.</summary>
    public static void Path(string path)
    {
        var target = System.IO.Path.GetFullPath(path);
        if (!File.Exists(target) && !Directory.Exists(target))
            throw new FileNotFoundException("The path does not exist.", target);

        Process.Start(DesktopStartInfo(target));
    }

    /// <summary>
    /// Open the nearest existing folder containing a file. There is no
    /// file-manager-neutral way to request file selection, so this deliberately
    /// opens the folder itself through the default folder association.
    /// </summary>
    public static void ContainingFolder(string path)
    {
        var target = System.IO.Path.GetFullPath(path);
        var folder = Directory.Exists(target)
            ? target
            : System.IO.Path.GetDirectoryName(target);
        while (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            folder = System.IO.Path.GetDirectoryName(folder);

        if (string.IsNullOrEmpty(folder))
            throw new DirectoryNotFoundException($"No containing folder exists for '{target}'.");

        Path(folder);
    }

    internal static ProcessStartInfo DesktopStartInfo(string path) => new()
    {
        FileName = path,
        UseShellExecute = true,
    };
}
