namespace FileIndexer.Models;

public class ActivityEntry
{
    public Guid Id { get; set; }
    public string Description { get; set; } = "";
    public ActivityStatus Status { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultMessage { get; set; }

    // Progress tracking for batch operations (extract, copy, move, delete, reindex...).
    // Null for non-batch entries.
    public int? ProgressCurrent { get; set; }
    public int? ProgressTotal { get; set; }
    public string? CurrentItem { get; set; }

    public string? ProgressText => ProgressCurrent.HasValue && ProgressTotal.HasValue
        ? $"{ProgressCurrent}/{ProgressTotal}"
        : null;

    public TimeSpan? Duration => CompletedAtUtc.HasValue
        ? CompletedAtUtc.Value - StartedAtUtc
        : DateTime.UtcNow - StartedAtUtc;
}

public enum ActivityStatus
{
    Running,
    Success,
    Error
}
