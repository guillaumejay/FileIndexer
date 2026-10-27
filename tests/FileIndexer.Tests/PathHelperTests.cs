namespace FileIndexer.Tests;

public class PathHelperTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fi-paths-tests", Guid.NewGuid().ToString("N"));

    public PathHelperTests() => Directory.CreateDirectory(Path.Combine(_root, "D&D and Clones", "OSR"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [WindowsOnlyFact]
    public void WithDiskCase_UsesTheCaseOnDisk()
    {
        var typed = Path.Combine(_root, "d&d AND clones", "osr");

        Assert.Equal(Path.Combine(_root, "D&D and Clones", "OSR"), PathHelper.WithDiskCase(typed, _root));
    }

    [Fact]
    public void WithDiskCase_KeepsMissingSegmentsAsTyped()
    {
        var typed = Path.Combine(_root, "D&D and Clones", "not-yet");

        Assert.Equal(typed, PathHelper.WithDiskCase(typed, _root));
    }
}
