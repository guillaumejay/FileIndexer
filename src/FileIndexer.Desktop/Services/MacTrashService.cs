using System.Diagnostics;

namespace FileIndexer.Services;

public class MacTrashService : ITrashService
{
    public bool IsSupported => true;

    public async Task<OperationResult> MoveToTrashAsync(string path)
    {
        try
        {
            // Use osascript to tell Finder to move file to trash
            var escapedPath = path.Replace("\"", "\\\"");
            var script = $"tell application \"Finder\" to delete POSIX file \"{escapedPath}\"";

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e '{script}'",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
            {
                return OperationResult.Failure("Impossible de lancer osascript");
            }

            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return OperationResult.Failure($"Erreur osascript : {error}");
            }

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure($"Erreur lors de la suppression : {ex.Message}");
        }
    }
}
