using System.Text;
using System.Text.Json;

namespace QuantaTrain.Infrastructure;

public sealed class RetentionMaintenance
{
    private static readonly TimeSpan MinimumRunInterval = TimeSpan.FromHours(24);
    private readonly string _historyRoot;
    private readonly string _usageRoot;
    private readonly string _logsRoot;
    private DateTimeOffset? _lastRunUtc;

    public RetentionMaintenance(
        string historyRoot,
        string usageRoot,
        string logsRoot)
    {
        _historyRoot = historyRoot;
        _usageRoot = usageRoot;
        _logsRoot = logsRoot;
    }

    public async Task<bool> RunIfDueAsync(
        int? retentionDays,
        int logRetentionDays,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken,
        bool force = false)
    {
        if (!force &&
            _lastRunUtc is not null &&
            nowUtc - _lastRunUtc.Value < MinimumRunInterval)
        {
            return false;
        }

        _lastRunUtc = nowUtc;
        if (retentionDays is not null)
        {
            var cutoff = DateOnly.FromDateTime(
                nowUtc.UtcDateTime.Date.AddDays(-retentionDays.Value));
            await PruneHistoryAsync(cutoff, cancellationToken).ConfigureAwait(false);
            await PruneUsageAsync(cutoff, cancellationToken).ConfigureAwait(false);
        }
        PruneLogs(
            nowUtc.UtcDateTime.Date.AddDays(-Math.Max(1, logRetentionDays)),
            cancellationToken);
        return true;
    }

    private async Task PruneHistoryAsync(
        DateOnly cutoff,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_historyRoot))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(_historyRoot, "*.jsonl"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var retained = new List<string>();
            await foreach (var line in File.ReadLinesAsync(path, cancellationToken))
            {
                try
                {
                    using var document = JsonDocument.Parse(line);
                    var observed = document.RootElement
                        .GetProperty("observedToUtc")
                        .GetDateTimeOffset();
                    if (DateOnly.FromDateTime(observed.UtcDateTime) >= cutoff)
                    {
                        retained.Add(line);
                    }
                }
                catch (Exception exception) when (
                    exception is JsonException or KeyNotFoundException or
                    InvalidOperationException or FormatException)
                {
                    // Preserve unknown or damaged rows; retention must fail safe.
                    retained.Add(line);
                }
            }
            await ReplaceLinesAsync(path, retained, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task PruneUsageAsync(
        DateOnly cutoff,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_usageRoot))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(_usageRoot, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = await AtomicJsonFile.ReadAsync<UsageMonthDocument>(
                path,
                cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                continue;
            }
            var retained = document.Rows
                .Where(row => row.Key.LocalDate >= cutoff)
                .ToArray();
            if (retained.Length == document.Rows.Count)
            {
                continue;
            }
            if (retained.Length == 0)
            {
                File.Delete(path);
                continue;
            }
            await AtomicJsonFile.WriteAsync(
                path,
                document with
                {
                    GeneratedAtUtc = DateTimeOffset.UtcNow,
                    Rows = retained,
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ReplaceLinesAsync(
        string path,
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            File.Delete(path);
            return;
        }

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporary,
                string.Join(Environment.NewLine, lines) + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private void PruneLogs(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_logsRoot))
        {
            return;
        }
        foreach (var path in Directory.EnumerateFiles(_logsRoot, "*.log"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.GetLastWriteTimeUtc(path) < cutoffUtc)
            {
                File.Delete(path);
            }
        }
    }
}
