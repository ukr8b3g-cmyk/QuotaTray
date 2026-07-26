using QuantaTrain.Core;

namespace QuantaTrain.Infrastructure;

public sealed class JsonQuotaStateStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonQuotaStateStore(string path)
    {
        _path = path;
    }

    public async Task<QuotaStateEnvelope?> ReadAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var value = await AtomicJsonFile.ReadAsync<QuotaStateEnvelope>(
                _path,
                cancellationToken).ConfigureAwait(false);
            return value?.SchemaVersion == 2 ? value : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteAsync(
        WeeklyQuotaState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await AtomicJsonFile.WriteAsync(
                _path,
                QuotaStateEnvelope.Create(state),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
