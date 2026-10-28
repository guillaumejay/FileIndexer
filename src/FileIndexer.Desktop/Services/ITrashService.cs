namespace FileIndexer.Services;

public interface ITrashService
{
    Task<OperationResult> MoveToTrashAsync(string path);
    bool IsSupported { get; }
}

public static class TrashServices
{
    public static ITrashService ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsTrashService()
        : OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst() ? new MacTrashService()
        : new LinuxTrashService();
}
