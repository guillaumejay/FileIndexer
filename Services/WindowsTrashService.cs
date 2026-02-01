using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;

namespace FileIndexer.Services;

public class WindowsTrashService : ITrashService
{
    public bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public Task<OperationResult> MoveToTrashAsync(string path)
    {
        return Task.Run(() =>
        {
            try
            {
                if (File.Exists(path))
                {
                    FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                else if (Directory.Exists(path))
                {
                    FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                else
                {
                    return OperationResult.Failure("Le fichier n'existe pas");
                }
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"Impossible de supprimer : {ex.Message}");
            }
        });
    }
}
