using System.Diagnostics;

namespace FileIndexer.Services;

// Starts external tools with ArgumentList so every argument reaches the program verbatim:
// a raw Arguments string is re-split on spaces and quotes, which breaks paths such as
// "/home/me/My Files/it's.txt".
internal static class ProcessRunner
{
    public static async Task<(int ExitCode, string Error)> RunAsync(string fileName, params string[] arguments)
    {
        using var process = Process.Start(CreateStartInfo(fileName, arguments, redirect: true))
            ?? throw new InvalidOperationException($"Impossible de lancer {fileName}");

        // Drain both streams so a chatty tool cannot block on a full pipe.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        await stdout.ConfigureAwait(false);
        return (process.ExitCode, await stderr.ConfigureAwait(false));
    }

    // Fire-and-forget launch (e.g. a file manager window).
    public static void Launch(string fileName, params string[] arguments)
    {
        using var _ = Process.Start(CreateStartInfo(fileName, arguments, redirect: false));
    }

    // Runs a trash tool and turns its exit code into an OperationResult.
    public static async Task<OperationResult> RunTrashToolAsync(string fileName, params string[] arguments)
    {
        try
        {
            var (exitCode, error) = await RunAsync(fileName, arguments);
            return exitCode == 0
                ? OperationResult.Success()
                : OperationResult.Failure($"Erreur {fileName} : {error}");
        }
        catch (Exception ex)
        {
            return OperationResult.Failure($"Erreur lors de la suppression : {ex.Message}");
        }
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, string[] arguments, bool redirect)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect
        };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);
        return psi;
    }
}
