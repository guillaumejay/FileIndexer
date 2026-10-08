using System.Diagnostics;

namespace FileIndexer.Services;

// Starts external tools with ArgumentList so every argument reaches the program verbatim:
// a raw Arguments string is re-split on spaces and quotes, which breaks paths such as
// "/home/me/My Files/it's.txt".
internal static class ProcessRunner
{
    // Fire-and-forget launch (e.g. a file manager window).
    public static void Launch(string fileName, params string[] arguments)
    {
        using var _ = Process.Start(CreateStartInfo(fileName, arguments, redirectError: false));
    }

    // Runs a trash tool and turns its exit code into an OperationResult.
    public static async Task<OperationResult> RunTrashToolAsync(string fileName, params string[] arguments)
    {
        try
        {
            using var process = Process.Start(CreateStartInfo(fileName, arguments, redirectError: true))
                ?? throw new InvalidOperationException($"Impossible de lancer {fileName}");
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0
                ? OperationResult.Success()
                : OperationResult.Failure($"Erreur {fileName} : {error}");
        }
        catch (Exception ex)
        {
            return OperationResult.Failure($"Erreur lors de la suppression : {ex.Message}");
        }
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, string[] arguments, bool redirectError)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = redirectError
        };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);
        return psi;
    }
}
