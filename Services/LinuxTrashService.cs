using System.Diagnostics;

namespace FileIndexer.Services;

public class LinuxTrashService : ITrashService
{
    public bool IsSupported => IsTrashCliInstalled();

    private static bool? _isInstalled;

    private static bool IsTrashCliInstalled()
    {
        if (_isInstalled.HasValue) return _isInstalled.Value;

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "trash-put",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
            _isInstalled = process?.ExitCode == 0;
        }
        catch
        {
            _isInstalled = false;
        }

        return _isInstalled.Value;
    }

    public async Task<OperationResult> MoveToTrashAsync(string path)
    {
        if (!IsSupported)
        {
            return OperationResult.Failure(
                "trash-cli n'est pas installé.\n" +
                "Installez-le avec : sudo apt install trash-cli\n" +
                "ou : sudo dnf install trash-cli");
        }

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "trash-put",
                Arguments = $"\"{path}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
            {
                return OperationResult.Failure("Impossible de lancer trash-put");
            }

            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return OperationResult.Failure($"Erreur trash-put : {error}");
            }

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure($"Erreur lors de la suppression : {ex.Message}");
        }
    }
}
