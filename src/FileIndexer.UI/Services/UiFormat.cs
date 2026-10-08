namespace FileIndexer.UI.Services;

public static class UiFormat
{
    public static string TruncatePath(string path, int maxLength) =>
        string.IsNullOrEmpty(path) || path.Length <= maxLength ? path : path[..(maxLength - 3)] + "...";

    public static string RelativeTime(DateTime utcTime)
    {
        var elapsed = DateTime.UtcNow - utcTime;
        if (elapsed.TotalMinutes < 1) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        return $"{(int)elapsed.TotalDays}d ago";
    }

    public static string Duration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1) return "< 1s";
        if (duration.TotalMinutes < 1) return $"{(int)duration.TotalSeconds}s";
        return $"{(int)duration.TotalMinutes}m{duration.Seconds:D2}s";
    }

    public static string Items(int count) => count == 1 ? "1 item" : $"{count:N0} items";
}
