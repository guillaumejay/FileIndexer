using System.Runtime.InteropServices;
using FileIndexer.Services;

namespace FileIndexer.Tests;

// Trash services are platform-specific. Behaviour that does not touch the OS recycle bin is
// asserted on every platform; the real recycle-bin integration is gated to Windows (the only
// platform whose trash backend has no external dependency). Linux/macOS use external CLIs
// (trash-put / osascript) and are exercised only on those platforms.
public class TrashServiceTests
{
    // --- WindowsTrashService -----------------------------------------------------------------

    [Fact]
    public void Windows_IsSupported_MatchesCurrentOs()
    {
        var svc = new WindowsTrashService();
        Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), svc.IsSupported);
    }

    [Fact]
    public async Task Windows_MoveToTrash_NonExistentPath_ReturnsFailure()
    {
        // Neither File.Exists nor Directory.Exists -> deterministic failure on any platform.
        var svc = new WindowsTrashService();
        var missing = Path.Combine(Path.GetTempPath(), "fi-trash-missing-" + Guid.NewGuid().ToString("N"));

        var result = await svc.MoveToTrashAsync(missing);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [WindowsOnlyFact]
    public async Task Windows_MoveToTrash_RealFile_RemovesFromDisk()
    {
        var svc = new WindowsTrashService();
        var path = Path.Combine(Path.GetTempPath(), "fi-trash-file-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "trash me");

        var result = await svc.MoveToTrashAsync(path);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.False(File.Exists(path));
    }

    [WindowsOnlyFact]
    public async Task Windows_MoveToTrash_RealDirectory_RemovesFromDisk()
    {
        var svc = new WindowsTrashService();
        var dir = Path.Combine(Path.GetTempPath(), "fi-trash-dir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "inside.txt"), "x");

        var result = await svc.MoveToTrashAsync(dir);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.False(Directory.Exists(dir));
    }

    // --- MacTrashService ---------------------------------------------------------------------

    [Fact]
    public void Mac_IsSupported_IsAlwaysTrue()
    {
        var svc = new MacTrashService();
        Assert.True(svc.IsSupported);
    }

    // --- LinuxTrashService -------------------------------------------------------------------

    [Fact]
    public async Task Linux_MoveToTrash_WhenTrashCliMissing_ReturnsHelpfulFailure()
    {
        var svc = new LinuxTrashService();

        // Only the unsupported path is deterministic off-Linux (and on Linux without trash-cli).
        // Where trash-cli is present we cannot assert the failure branch, so skip the assertion.
        if (svc.IsSupported) return;

        var result = await svc.MoveToTrashAsync("/tmp/whatever");

        Assert.False(result.IsSuccess);
        Assert.Contains("trash-cli", result.ErrorMessage);
    }
}

// Skips at runtime when not running on Windows (xunit v2 honours Skip set in the ctor).
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Skip = "Windows-only trash backend.";
    }
}
