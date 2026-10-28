namespace FileIndexer.Models;

// One file operation as journaled. Destination: the new path (move, rename, copy), null for a
// delete. Error: null on success. Source: the program that did it (FileIndexer.Mcp, .Web, .Maui).
public record FileOperationEntry(DateTime AtUtc, string Operation, string Path, string? Destination, string? Error, string Source);

public record OperationCount(string Operation, int Succeeded, int Failed);
