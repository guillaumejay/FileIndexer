namespace FileIndexer.Services;

public class LinuxTrashService : ITrashService
{
    private static readonly Lazy<bool> IsTrashCliInstalled = new(() =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => File.Exists(Path.Combine(dir, "trash-put"))));

    public bool IsSupported => IsTrashCliInstalled.Value;

    public Task<OperationResult> MoveToTrashAsync(string path)
    {
        if (!IsSupported)
        {
            return Task.FromResult(OperationResult.Failure(
                "trash-cli n'est pas installé.\n" +
                "Installez-le avec : sudo apt install trash-cli\n" +
                "ou : sudo dnf install trash-cli"));
        }

        // "--" so a path starting with '-' is never read as an option.
        return ProcessRunner.RunTrashToolAsync("trash-put", "--", path);
    }
}
