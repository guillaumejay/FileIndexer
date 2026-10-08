namespace FileIndexer;

public static class PathHelper
{
    // Windows and macOS file systems are case-insensitive by default.
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static bool AreSame(string a, string b) => string.Equals(TrimEnd(a), TrimEnd(b), Comparison);

    // True when path is root itself or lies below it. The separator stops "C:\data2" from
    // matching the root "C:\data".
    public static bool IsSameOrUnder(string path, string root) => AreSame(path, root) || IsStrictlyUnder(path, root);

    // True when path lies below root (root itself excluded).
    public static bool IsStrictlyUnder(string path, string root) =>
        TrimEnd(path).StartsWith(WithTrailingSeparator(root), Comparison);

    // "C:\data" and "C:\data\" -> "C:\data\": the prefix shared by everything inside a folder.
    public static string WithTrailingSeparator(string path)
    {
        var trimmed = TrimEnd(path);
        return trimmed.EndsWith(Path.DirectorySeparatorChar) ? trimmed : trimmed + Path.DirectorySeparatorChar;
    }

    // "name (1).ext", "name (2).ext", ... until nothing exists at that path. Folder names keep
    // any dot intact ("v1.2 (1)" rather than "v1 (1).2").
    public static string GenerateUniqueName(string directory, string name, bool isDirectory = false)
    {
        var stem = isDirectory ? name : Path.GetFileNameWithoutExtension(name);
        var ext = isDirectory ? "" : Path.GetExtension(name);

        for (var counter = 1; ; counter++)
        {
            var candidate = $"{stem} ({counter}){ext}";
            if (!Path.Exists(Path.Combine(directory, candidate)))
                return candidate;
        }
    }

    private static string TrimEnd(string path) =>
        path.Length > 1 ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : path;
}
