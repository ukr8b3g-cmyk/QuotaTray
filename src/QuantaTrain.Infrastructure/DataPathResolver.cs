namespace QuantaTrain.Infrastructure;

public sealed record DataPaths(
    string Root,
    string SettingsFile,
    string StateFile,
    string HistoryDirectory,
    string LogsDirectory,
    bool IsPortable);

public static class DataPathResolver
{
    public static DataPaths Resolve(string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        var portable = File.Exists(Path.Combine(applicationDirectory, "portable.flag"));
        var root = portable
            ? Path.Combine(applicationDirectory, "data")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuantaTrain");

        EnsureWritable(root, portable);
        var history = Path.Combine(root, "history");
        var logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(history);
        Directory.CreateDirectory(logs);
        return new DataPaths(
            root,
            Path.Combine(root, "settings.json"),
            Path.Combine(root, "state.json"),
            history,
            logs,
            portable);
    }

    private static void EnsureWritable(string root, bool portable)
    {
        try
        {
            Directory.CreateDirectory(root);
            var probe = Path.Combine(root, $".write-probe-{Guid.NewGuid():N}");
            using (File.Create(probe))
            {
            }
            File.Delete(probe);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException)
        {
            var mode = portable ? "portable" : "installed";
            throw new IOException(
                $"The {mode} data directory is not writable: {root}",
                exception);
        }
    }
}
