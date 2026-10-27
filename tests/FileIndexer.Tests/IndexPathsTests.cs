using FileIndexer.Mcp;

namespace FileIndexer.Tests;

public class IndexPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fi-paths-tests", Guid.NewGuid().ToString("N"));

    public IndexPathsTests() => Directory.CreateDirectory(Path.Combine(_root, "D&D and Clones", "OSR"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [WindowsOnlyFact]
    public void Canonical_UsesTheCaseOnDisk()
    {
        var typed = Path.Combine(_root, "d&d AND clones", "osr");

        Assert.Equal(Path.Combine(_root, "D&D and Clones", "OSR"), IndexPaths.Canonical(typed, _root));
    }

    [Fact]
    public void Canonical_KeepsMissingSegmentsAsTyped()
    {
        var typed = Path.Combine(_root, "D&D and Clones", "not-yet");

        Assert.Equal(typed, IndexPaths.Canonical(typed, _root));
    }
}
