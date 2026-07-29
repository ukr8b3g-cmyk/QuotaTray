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
    private readonly HashSet<string> _knownEventKeys = new(StringComparer.Ordinal);
    private bool _eventKeysLoaded;

    public JsonlHistoryStore(string historyDirectory)
    {
        _historyDirectory = historyDirectory;
        Directory.CreateDirectory(historyDirectory);
    }

    public async Task AppendAsync(ResetEvent resetEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resetEvent);
        var key = ResetEventDeduplicator.CreateKey(resetEvent);

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
            dedupeKey = key,
            appVersion =
                typeof(JsonlHistoryStore).Assembly.GetName().Version?.ToString(3) ?? "0.2.0",
            codexVersion = resetEvent.After.CodexVersion,
        };

        var line = JsonSerializer.Serialize(record, SerializerOptions);
        var path = Path.Combine(
            _historyDirectory,
            $"{resetEvent.ObservedToUtc:yyyy-MM}.jsonl");
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await LoadEventKeysAsync(cancellationToken).ConfigureAwait(false);
            if (_knownEventKeys.Contains(key))
            {
                return;
            }
            await File.AppendAllTextAsync(
                path,
                line + Environment.NewLine,
                cancellationToken).ConfigureAwait(false);
            _knownEventKeys.Add(key);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task LoadEventKeysAsync(CancellationToken cancellationToken)
    {
        if (_eventKeysLoaded)
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(
                     _historyDirectory,
                     "*.jsonl"))
        {
            await foreach (var line in File.ReadLinesAsync(path, cancellationToken))
            {
                try
                {
                    using var document = JsonDocument.Parse(line);
                    if (document.RootElement.TryGetProperty(
                            "dedupeKey",
                            out var keyElement) &&
                        keyElement.GetString() is { Length: > 0 } key)
                    {
                        _knownEventKeys.Add(key);
                    }
                }
                catch (JsonException)
                {
                    // A damaged line does not invalidate the remaining history.
                }
            }
        }
        _eventKeysLoaded = true;
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

    public async Task<IReadOnlyList<string>> ReadAllAsync(
        CancellationToken cancellationToken)
    {
        var records = new List<(DateTimeOffset Time, string Display)>();
        foreach (var file in Directory.EnumerateFiles(
                     _historyDirectory,
                     "*.jsonl"))
        {
            await foreach (var line in File.ReadLinesAsync(file, cancellationToken))
            {
                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    var time = root.GetProperty("observedToUtc").GetDateTimeOffset();
                    var classification = root.GetProperty("classification").GetString();
                    records.Add(
                        (time, $"{time.ToLocalTime():g}  {classification}"));
                }
                catch (Exception exception) when (
                    exception is JsonException or KeyNotFoundException or
                    InvalidOperationException or FormatException)
                {
                    // Skip only the damaged or unsupported row.
                }
            }
        }

        return records
            .OrderByDescending(record => record.Time)
            .Select(record => record.Display)
            .ToArray();
    }

    public async Task<IReadOnlyList<ResetEvent>> ReadRecentEventsAsync(
        int maximum,
        CancellationToken cancellationToken)
    {
        var result = new List<ResetEvent>();
        var files = Directory.EnumerateFiles(_historyDirectory, "*.jsonl")
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase);
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
                    var observedFrom = root.GetProperty(
                        "observedFromUtc").GetDateTimeOffset();
                    var observedTo = root.GetProperty(
                        "observedToUtc").GetDateTimeOffset();
                    var limitId = root.TryGetProperty(
                        "limitId",
                        out var limitElement)
                        ? limitElement.GetString()
                        : null;
                    var role = root.TryGetProperty(
                            "bucketRole",
                            out var roleElement) &&
                        Enum.TryParse<BucketRole>(
                            roleElement.GetString(),
                            true,
                            out var parsedRole)
                            ? parsedRole
                            : BucketRole.Unknown;
                    var windowMinutes = root.TryGetProperty(
                            "windowDurationMins",
                            out var windowElement) &&
                        windowElement.ValueKind == JsonValueKind.Number
                            ? windowElement.GetInt32()
                            : (int?)null;
                    var before = ReadStoredState(
                        root.GetProperty("before"),
                        limitId,
                        role,
                        windowMinutes,
                        observedFrom);
                    var after = ReadStoredState(
                        root.GetProperty("after"),
                        limitId,
                        role,
                        windowMinutes,
                        observedTo);
                    var classification = ParseClassification(
                        root.GetProperty("classification").GetString());
                    var confidence = root.TryGetProperty(
                            "confidence",
                            out var confidenceElement) &&
                        Enum.TryParse<Confidence>(
                            confidenceElement.GetString(),
                            true,
                            out var parsedConfidence)
                            ? parsedConfidence
                            : Confidence.Low;
                    var reasons = root.TryGetProperty(
                            "reasonCodes",
                            out var reasonsElement)
                        ? reasonsElement.EnumerateArray()
                            .Select(item => item.GetString() ?? string.Empty)
                            .Where(item => item.Length > 0)
                            .ToArray()
                        : [];
                    result.Add(new ResetEvent(
                        observedFrom,
                        observedTo,
                        classification,
                        confidence,
                        before,
                        after,
                        reasons,
                        root.TryGetProperty("confirmed", out var confirmed) &&
                        confirmed.GetBoolean()));
                    if (result.Count >= maximum)
                    {
                        return result;
                    }
                }
                catch (Exception exception) when (
                    exception is JsonException or KeyNotFoundException or
                    InvalidOperationException or FormatException)
                {
                    // Skip only the damaged or unsupported row.
                }
            }
        }
        return result;
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

    private static WeeklyQuotaState ReadStoredState(
        JsonElement element,
        string? limitId,
        BucketRole role,
        int? windowDurationMinutes,
        DateTimeOffset observedAtUtc)
    {
        var used = element.GetProperty("usedPercent").GetDouble();
        var remaining = element.GetProperty("remainingPercent").GetDouble();
        var resetAt = element.TryGetProperty("resetsAtUtc", out var reset) &&
                      reset.ValueKind == JsonValueKind.String
            ? reset.GetDateTimeOffset()
            : (DateTimeOffset?)null;
        var credits = element.TryGetProperty("resetCreditCount", out var count) &&
                      count.ValueKind == JsonValueKind.Number
            ? count.GetInt32()
            : (int?)null;
        var plan = element.TryGetProperty("planType", out var planElement) &&
                   planElement.ValueKind == JsonValueKind.String
            ? planElement.GetString()
            : null;
        return new WeeklyQuotaState(
            limitId,
            role,
            used,
            remaining,
            windowDurationMinutes,
            resetAt,
            credits,
            [],
            plan,
            observedAtUtc,
            null);
    }

    private static ResetClassification ParseClassification(string? value) =>
        value switch
        {
            "scheduled-reset" => ResetClassification.ScheduledReset,
            "reset-credit-likely" => ResetClassification.ResetCreditLikely,
            "unexpected-reset-candidate" =>
                ResetClassification.UnexpectedResetCandidate,
            "limit-policy-change" => ResetClassification.LimitPolicyChange,
            "uncertain-change" => ResetClassification.UncertainChange,
            _ => ResetClassification.None,
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
