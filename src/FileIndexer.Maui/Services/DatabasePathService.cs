namespace FileIndexer.Maui.Services;

public class DatabasePathService
{
    private const string DatabasePathKey = "DatabasePath";

    public string? GetDatabasePath()
    {
        return Preferences.Get(DatabasePathKey, null);
    }

    public void SetDatabasePath(string path)
    {
        Preferences.Set(DatabasePathKey, path);
    }

    public bool HasDatabasePath()
    {
        return !string.IsNullOrEmpty(GetDatabasePath());
    }

    public void ClearDatabasePath()
    {
        Preferences.Remove(DatabasePathKey);
    }
}
