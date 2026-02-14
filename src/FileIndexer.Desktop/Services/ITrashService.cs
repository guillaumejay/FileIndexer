namespace FileIndexer.Services;

public interface ITrashService
{
    Task<OperationResult> MoveToTrashAsync(string path);
    bool IsSupported { get; }
}
