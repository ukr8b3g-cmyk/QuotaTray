using System.Text.Json;
using QuantaTrain.Core;

namespace QuantaTrain.Infrastructure;

public sealed class JsonlHistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private readonly string _historyDirectory;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private string? _lastEventKey;

    public JsonlHistoryStore(string historyDirectory)
    {
        _historyDirectory = historyDirectory;
        Directory.CreateDirectory(historyDirectory);
    }

    public async Task AppendAsync(ResetEvent resetEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resetEvent);
        var key = string.Join(
            '|',
            resetEvent.After.LimitId,
            resetEvent.After.ResetsAtUtc,
            resetEvent.ObservedToUtc.ToUnixTimeSeconds() / 60,
            resetEvent.Before.UsedPercent,
            resetEvent.After.UsedPercent);
        if (string.Equals(key, _lastEventKey, StringComparison.Ordinal))
        {
            return;
        }

        var record = new
        {
            schemaVersion = 1,
            type = "reset-event",
            observedFromUtc = resetEvent.ObservedFromUtc,
            observedToUtc = resetEvent.ObservedToUtc,
            classification = ToSchemaValue(resetEvent.Classification),
            confidence = resetEvent.Confidence.ToString().ToLowerInvariant(),
            limitId = resetEvent.After.LimitId,
            bucketRole = resetEvent.After.Role.ToString().ToLowerInvariant(),
            windowDurationMins = resetEvent.After.WindowDurationMinutes,
            before = ToStoredState(resetEvent.Before),
            after = ToStoredState(resetEvent.After),
            reasonCodes = resetEvent.ReasonCodes.Take(20),
            confirmed = resetEvent.Confirmed,
            appVersion = "0.1.0",
            codexVersion = resetEvent.After.CodexVersion,
        };

        var line = JsonSerializer.Serialize(record, SerializerOptions);
        var path = Path.Combine(
            _historyDirectory,
            $"{resetEvent.ObservedToUtc:yyyy-MM}.jsonl");
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(
                path,
                line + Environment.NewLine,
                cancellationToken).ConfigureAwait(false);
            _lastEventKey = key;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> ReadRecentAsync(
        int maximum,
        CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(_historyDirectory, "*.jsonl")
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(3);
        var records = new List<string>();
        foreach (var file in files)
        {
            var lines = await File.ReadAllLinesAsync(file, cancellationToken)
                .ConfigureAwait(false);
            foreach (var line in lines.Reverse())
            {
                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    var time = root.GetProperty("observedToUtc").GetDateTimeOffset();
                    var classification = root.GetProperty("classification").GetString();
                    records.Add($"{time.ToLocalTime():g}  {classification}");
                    if (records.Count >= maximum)
                    {
                        return records;
                    }
                }
                catch (JsonException)
                {
                    // Skip only the damaged line.
                }
            }
        }

        return records;
    }

    public void Prune(int? retentionDays)
    {
        if (retentionDays is null)
        {
            return;
        }

        var threshold = DateTimeOffset.UtcNow.AddDays(-retentionDays.Value);
        foreach (var path in Directory.EnumerateFiles(_historyDirectory, "*.jsonl"))
        {
            if (File.GetLastWriteTimeUtc(path) < threshold.UtcDateTime)
            {
                File.Delete(path);
            }
        }
    }

    private static object ToStoredState(WeeklyQuotaState state) => new
    {
        usedPercent = state.UsedPercent,
        remainingPercent = state.RemainingPercent,
        resetsAtUtc = state.ResetsAtUtc,
        resetCreditCount = state.ResetCreditCount,
        planType = state.PlanType,
    };

    private static string ToSchemaValue(ResetClassification classification) =>
        classification switch
        {
            ResetClassification.ScheduledReset => "scheduled-reset",
            ResetClassification.ResetCreditLikely => "reset-credit-likely",
            ResetClassification.UnexpectedResetCandidate => "unexpected-reset-candidate",
            ResetClassification.LimitPolicyChange => "limit-policy-change",
            ResetClassification.UncertainChange => "uncertain-change",
            _ => "none",
        };
}
