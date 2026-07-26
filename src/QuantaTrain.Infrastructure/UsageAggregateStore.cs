using System.Text.Json;
using QuantaTrain.Core;

namespace QuantaTrain.Infrastructure;

public sealed record UsageMonthDocument(
    int SchemaVersion,
    string Month,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<UsageAggregate> Rows);

public sealed class UsageAggregateStore
{
    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public UsageAggregateStore(string root)
    {
        _root = root;
        Directory.CreateDirectory(root);
    }

    public async Task ReplaceAllAsync(
        IEnumerable<UsageAggregate> rows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var groups = rows
            .GroupBy(row => $"{row.Key.LocalDate:yyyy-MM}")
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<UsageAggregate>)group.ToArray(),
                StringComparer.Ordinal);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var (month, monthRows) in groups)
            {
                await AtomicJsonFile.WriteAsync(
                    Path.Combine(_root, $"{month}.json"),
                    new UsageMonthDocument(
                        1,
                        month,
                        DateTimeOffset.UtcNow,
                        monthRows),
                    cancellationToken).ConfigureAwait(false);
            }

            foreach (var path in Directory.EnumerateFiles(_root, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!groups.ContainsKey(Path.GetFileNameWithoutExtension(path)))
                {
                    File.Delete(path);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<UsageAggregate>> ReadAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var fromDate = DateOnly.FromDateTime(fromUtc.LocalDateTime.Date);
        var toDate = DateOnly.FromDateTime(toUtc.LocalDateTime.Date);
        var result = new List<UsageAggregate>();
        foreach (var path in Directory.EnumerateFiles(_root, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = await AtomicJsonFile.ReadAsync<UsageMonthDocument>(
                path,
                cancellationToken).ConfigureAwait(false);
            if (document?.SchemaVersion != 1)
            {
                continue;
            }
            result.AddRange(document.Rows.Where(
                row => row.Key.LocalDate >= fromDate && row.Key.LocalDate <= toDate));
        }
        return result;
    }
}
