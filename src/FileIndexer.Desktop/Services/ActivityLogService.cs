using FileIndexer.Models;

namespace FileIndexer.Services;

public class ActivityLogService
{
    private readonly List<ActivityEntry> _entries = new();
    private readonly object _lock = new();
    private const int MaxEntries = 50;

    public event Action? OnChanged;

    public IReadOnlyList<ActivityEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }

    public int RunningCount
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count(e => e.Status == ActivityStatus.Running);
            }
        }
    }

    public ActivityEntry StartActivity(string description)
    {
        var entry = new ActivityEntry
        {
            Id = Guid.NewGuid(),
            Description = description,
            Status = ActivityStatus.Running,
            StartedAtUtc = DateTime.UtcNow
        };

        lock (_lock)
        {
            _entries.Insert(0, entry);
            Trim();
        }

        OnChanged?.Invoke();
        return entry;
    }

    public void ReportProgress(Guid id, int current, int total, string? currentItem = null)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry != null)
            {
                entry.ProgressCurrent = current;
                entry.ProgressTotal = total;
                entry.CurrentItem = currentItem;
            }
        }

        OnChanged?.Invoke();
    }

    public void CompleteActivity(Guid id, string? resultMessage = null)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry != null)
            {
                entry.Status = ActivityStatus.Success;
                entry.CompletedAtUtc = DateTime.UtcNow;
                entry.ResultMessage = resultMessage;
            }
        }

        OnChanged?.Invoke();
    }

    public void FailActivity(Guid id, string error)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry != null)
            {
                entry.Status = ActivityStatus.Error;
                entry.CompletedAtUtc = DateTime.UtcNow;
                entry.ErrorMessage = error;
            }
        }

        OnChanged?.Invoke();
    }

    private void Trim()
    {
        while (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }
    }
}
