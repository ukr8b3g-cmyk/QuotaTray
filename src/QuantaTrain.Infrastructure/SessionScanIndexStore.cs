namespace QuantaTrain.Infrastructure;

internal sealed class SessionScanIndexStore
{
    private readonly string _path;

    public SessionScanIndexStore(string path)
    {
        _path = path;
    }

    public async Task<SessionScanIndexDocument> ReadAsync(
        int parserVersion,
        CancellationToken cancellationToken)
    {
        var document = await AtomicJsonFile.ReadAsync<SessionScanIndexDocument>(
            _path,
            cancellationToken).ConfigureAwait(false);
        return document is { SchemaVersion: 1 } &&
               document.ParserVersion == parserVersion
            ? document
            : new SessionScanIndexDocument(
                1,
                parserVersion,
                DateTimeOffset.MinValue,
                []);
    }

    public Task WriteAsync(
        SessionScanIndexDocument document,
        CancellationToken cancellationToken) =>
        AtomicJsonFile.WriteAsync(_path, document, cancellationToken);

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
        return Task.CompletedTask;
    }
}
