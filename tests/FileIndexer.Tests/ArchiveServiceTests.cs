using System.IO.Compression;
using FileIndexer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileIndexer.Tests;

public class ArchiveServiceTests
{
    private static ArchiveService NewService() => new(NullLogger<ArchiveService>.Instance);

    [Theory]
    [InlineData("backup.zip", true)]
    [InlineData("archive.7z", true)]
    [InlineData("data.tar.gz", true)]
    [InlineData("notes.txt", false)]
    [InlineData("image.png", false)]
    [InlineData("plain.gz", true)] // bare .gz (gzip) is in the archive extension set
    public void IsArchive_DetectsByExtension(string fileName, bool expected)
    {
        Assert.Equal(expected, ArchiveService.IsArchive(fileName));
    }

    [Fact]
    public async Task ExtractSmartAsync_NonExistentFile_ReturnsFailure()
    {
        var svc = NewService();
        var result = await svc.ExtractSmartAsync(Path.Combine(Path.GetTempPath(), "does-not-exist-xyz.zip"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ExtractSmartAsync_ValidZip_ExtractsFiles()
    {
        using var temp = new TempDir();

        // Build a small source tree and zip it.
        var src = Path.Combine(temp.Path, "src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "hello.txt"), "hello");
        File.WriteAllText(Path.Combine(src, "world.txt"), "world");

        var zipPath = Path.Combine(temp.Path, "bundle.zip");
        ZipFile.CreateFromDirectory(src, zipPath);

        var svc = NewService();
        var result = await svc.ExtractSmartAsync(zipPath);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, result.FileCount);
        Assert.All(result.ExtractedFiles, f => Assert.True(File.Exists(f), $"missing: {f}"));
    }

    [Fact]
    public async Task ExtractSmartAsync_PathTraversalEntry_IsSkippedNotWrittenOutside()
    {
        // Regression: a malicious entry must not escape the extraction directory. The sibling
        // name "data-evil" shares the prefix of the extract dir "data" and slipped past the old
        // separator-less StartsWith check.
        using var temp = new TempDir();
        var box = Path.Combine(temp.Path, "box");
        Directory.CreateDirectory(box);
        var zipPath = Path.Combine(box, "data.zip");

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(zip.CreateEntry("keep.txt").Open())) w.Write("safe");
            // ".." forces a second root (so extraction goes into box/data) and points outside it.
            using (var w = new StreamWriter(zip.CreateEntry("../data-evil/escape.txt").Open())) w.Write("pwned");
        }

        var result = await NewService().ExtractSmartAsync(zipPath);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(1, result.FileCount); // only the safe entry
        Assert.True(File.Exists(Path.Combine(box, "data", "keep.txt")));
        Assert.False(File.Exists(Path.Combine(box, "data-evil", "escape.txt")));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fi_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }
}
