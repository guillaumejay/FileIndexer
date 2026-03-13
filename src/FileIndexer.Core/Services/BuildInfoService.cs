using System.Reflection;

namespace FileIndexer.Services;

public class BuildInfoService
{
    public string Version { get; }
    public DateTime BuildDate { get; }

    public BuildInfoService()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(BuildInfoService).Assembly;
        
        // Version
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        // In case of git-based versions, it might contain a hash after a +
        Version = infoVersion?.Split('+')[0] ?? "1.0.0";

        // Build Date
        // We try to get it from the assembly file's last write time
        try
        {
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                BuildDate = File.GetLastWriteTime(assembly.Location);
            }
            else
            {
                // Fallback to Now for development
                BuildDate = DateTime.Now;
            }
        }
        catch
        {
            BuildDate = DateTime.Now;
        }
    }
}
