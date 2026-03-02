namespace FileIndexer.Models;

public class ActivityEntry
{
    public Guid Id { get; set; }
    public string Description { get; set; } = "";
    public ActivityStatus Status { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }

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
