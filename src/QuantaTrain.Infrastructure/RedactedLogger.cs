namespace QuantaTrain.Infrastructure;

public sealed class RedactedLogger
{
    private const long MaximumFileBytes = 1024 * 1024;
    private const int MaximumFiles = 5;
    private readonly string _directory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RedactedLogger(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public async Task WarningAsync(string message, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RotateIfNeeded();
            var line = $"{DateTimeOffset.UtcNow:O} WARN {Redaction.Redact(message)}{Environment.NewLine}";
            await File.AppendAllTextAsync(
                Path.Combine(_directory, "quantatray.log"),
                line,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task WarningSafelyAsync(string message)
    {
        try
        {
            await WarningAsync(message, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // A diagnostic write failure must not turn a handled refresh failure
            // into an unhandled exception in an async UI event handler.
        }
    }

    private void RotateIfNeeded()
    {
        var current = Path.Combine(_directory, "quantatray.log");
        if (!File.Exists(current) || new FileInfo(current).Length < MaximumFileBytes)
        {
            return;
        }

        for (var index = MaximumFiles - 1; index >= 1; index--)
        {
            var source = Path.Combine(_directory, $"quantatray.{index}.log");
            var destination = Path.Combine(_directory, $"quantatray.{index + 1}.log");
            if (File.Exists(source))
            {
                File.Move(source, destination, overwrite: true);
            }
        }
        File.Move(current, Path.Combine(_directory, "quantatray.1.log"), overwrite: true);
    }
}
