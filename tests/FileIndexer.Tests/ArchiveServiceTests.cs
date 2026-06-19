using System.IO.Compression;
using FileIndexer.Services;

namespace FileIndexer.Tests;

public class ArchiveServiceTests
{
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
        var svc = new ArchiveService();
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

        var svc = new ArchiveService();
        var result = await svc.ExtractSmartAsync(zipPath);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, result.FileCount);
        Assert.All(result.ExtractedFiles, f => Assert.True(File.Exists(f), $"missing: {f}"));
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
