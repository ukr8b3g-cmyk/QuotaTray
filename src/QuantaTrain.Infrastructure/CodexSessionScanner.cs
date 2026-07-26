using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuantaTrain.Core;

namespace QuantaTrain.Infrastructure;

public sealed class CodexSessionScanner
{
    private const int ParserVersion = 1;
    private const int SignatureBytes = 256;
    private const int MaximumMetadataLineBytes = 4 * 1024 * 1024;
    private readonly SessionScanIndexStore _indexStore;
    private readonly UsageAggregateStore _aggregateStore;
    private readonly SemaphoreSlim _scanGate = new(1, 1);

    public CodexSessionScanner(
        string indexPath,
        UsageAggregateStore aggregateStore)
    {
        _indexStore = new SessionScanIndexStore(indexPath);
        _aggregateStore = aggregateStore;
    }

    public async Task ResetCacheAsync(CancellationToken cancellationToken)
    {
        await _scanGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _indexStore.DeleteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _scanGate.Release();
        }
    }

    public async Task<SessionScanResult> ScanAsync(
        UsageAnalyticsSettings settings,
        IProgress<SessionScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.Enabled)
        {
            return new SessionScanResult([], 0, 0, 0);
        }

        await _scanGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var effectiveParserVersion = GetParserVersion(settings);
            var paths = ResolveSessionPaths(settings).ToArray();
            var index = await _indexStore.ReadAsync(
                effectiveParserVersion,
                cancellationToken).ConfigureAwait(false);
            var previousByHash = index.Files.ToDictionary(
                entry => entry.PathSha256,
                StringComparer.Ordinal);
            var currentEntries = new List<SessionScanIndexEntry>(paths.Length);
            var scanned = 0;
            var skipped = 0;
            var errors = 0;

            for (var position = 0; position < paths.Length; position++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new SessionScanProgress(position, paths.Length));
                var path = paths[position];
                var hash = HashPath(path);
                previousByHash.TryGetValue(hash, out var previous);
                try
                {
                    var info = new FileInfo(path);
                    if (previous is not null &&
                        info.Length == previous.SizeBytes &&
                        info.LastWriteTimeUtc == previous.LastWriteUtc.UtcDateTime)
                    {
                        currentEntries.Add(previous);
                        skipped++;
                        continue;
                    }

                    var prefix = await HashSliceAsync(
                        path,
                        0,
                        Math.Min(SignatureBytes, info.Length),
                        cancellationToken).ConfigureAwait(false);
                    var appendOnly = previous is not null &&
                        info.Length >= previous.SizeBytes &&
                        string.Equals(
                            prefix,
                            previous.PrefixSignature,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            await HashSliceAsync(
                                path,
                                Math.Max(0, previous.SizeBytes - SignatureBytes),
                                Math.Min(SignatureBytes, previous.SizeBytes),
                                cancellationToken).ConfigureAwait(false),
                            previous.BoundarySignature,
                            StringComparison.Ordinal);

                    var startOffset = appendOnly ? previous!.SizeBytes : 0;
                    var continuation = appendOnly
                        ? previous!.Continuation
                        : SessionParserContinuation.Empty;
                    var contributions = appendOnly
                        ? previous!.Contributions.ToList()
                        : [];
                    var parsed = await ParseFileAsync(
                        path,
                        startOffset,
                        continuation,
                        settings,
                        cancellationToken).ConfigureAwait(false);
                    contributions = MergeRows(
                        contributions.Concat(
                            ApplyCollectionSettings(
                                UsageAnalyticsAggregator.Aggregate(
                                    parsed.Turns),
                                settings)))
                        .ToList();

                    currentEntries.Add(new SessionScanIndexEntry(
                        hash,
                        info.Length,
                        info.LastWriteTimeUtc,
                        prefix,
                        await HashSliceAsync(
                            path,
                            Math.Max(0, info.Length - SignatureBytes),
                            Math.Min(SignatureBytes, info.Length),
                            cancellationToken).ConfigureAwait(false),
                        contributions,
                        parsed.Continuation));
                    scanned++;
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or
                    JsonException or FormatException or OverflowException)
                {
                    errors++;
                    if (previous is not null)
                    {
                        currentEntries.Add(previous);
                    }
                }
            }

            progress?.Report(new SessionScanProgress(paths.Length, paths.Length));
            var allRows = MergeRows(
                currentEntries.SelectMany(entry => entry.Contributions));
            await _aggregateStore.ReplaceAllAsync(
                allRows,
                cancellationToken).ConfigureAwait(false);
            await _indexStore.WriteAsync(
                new SessionScanIndexDocument(
                    1,
                    effectiveParserVersion,
                    DateTimeOffset.UtcNow,
                    currentEntries),
                cancellationToken).ConfigureAwait(false);
            return new SessionScanResult(allRows, scanned, skipped, errors);
        }
        finally
        {
            _scanGate.Release();
        }
    }

    private static IEnumerable<string> ResolveSessionPaths(
        UsageAnalyticsSettings settings)
    {
        var configuredHome = settings.CodexHomeOverride;
        var environmentHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        var home = !string.IsNullOrWhiteSpace(configuredHome)
            ? configuredHome
            : !string.IsNullOrWhiteSpace(environmentHome)
                ? environmentHome
                : Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    ".codex");
        if (string.IsNullOrWhiteSpace(home))
        {
            yield break;
        }

        var fullHome = Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(home));
        foreach (var path in EnumerateJsonl(Path.Combine(fullHome, "sessions")))
        {
            yield return path;
        }
        if (settings.IncludeArchivedSessions)
        {
            foreach (var path in EnumerateJsonl(
                         Path.Combine(fullHome, "archived_sessions")))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> EnumerateJsonl(string root)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            string[] files;
            string[] directories;
            try
            {
                files = Directory.GetFiles(directory, "*.jsonl");
                directories = Directory.GetDirectories(directory);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }
            foreach (var child in directories)
            {
                pending.Push(child);
            }
        }
    }

    private static async Task<ParsedSessionFile> ParseFileAsync(
        string path,
        long startOffset,
        SessionParserContinuation initial,
        UsageAnalyticsSettings settings,
        CancellationToken cancellationToken)
    {
        var turns = new List<UsageTurnRecord>();
        var state = initial;
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        stream.Seek(startOffset, SeekOrigin.Begin);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: startOffset == 0,
            64 * 1024,
            leaveOpen: false);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)
               is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Encoding.UTF8.GetByteCount(line) > MaximumMetadataLineBytes ||
                !IsRelevantLine(line))
            {
                continue;
            }
            try
            {
                using var document = JsonDocument.Parse(line);
                state = ProcessEvent(
                    document.RootElement,
                    state,
                    turns,
                    settings);
            }
            catch (JsonException)
            {
                // A concurrently written or damaged row must not discard the
                // valid metadata already collected from the rest of the file.
            }
        }

        return new ParsedSessionFile(turns, state);
    }

    private static SessionParserContinuation ProcessEvent(
        JsonElement root,
        SessionParserContinuation state,
        ICollection<UsageTurnRecord> turns,
        UsageAnalyticsSettings settings)
    {
        var timestamp = ReadTimestamp(root, "timestamp") ?? DateTimeOffset.UtcNow;
        var rootType = ReadString(root, "type");
        if (string.Equals(rootType, "turn_context", StringComparison.Ordinal))
        {
            if (!root.TryGetProperty("payload", out var payload))
            {
                return state;
            }
            return state with
            {
                Model = ReadString(payload, "model") ?? state.Model,
                ReasoningEffort =
                    ReadString(payload, "effort") ??
                    ReadString(payload, "reasoning_effort") ??
                    state.ReasoningEffort,
                ServiceTier =
                    ReadString(payload, "service_tier") ??
                    ReadString(payload, "serviceTier") ??
                    state.ServiceTier,
            };
        }

        if (!string.Equals(rootType, "event_msg", StringComparison.Ordinal) ||
            !root.TryGetProperty("payload", out var eventPayload))
        {
            return state;
        }

        var eventType = ReadString(eventPayload, "type");
        if (string.Equals(eventType, "task_started", StringComparison.Ordinal) ||
            string.Equals(eventType, "turn_started", StringComparison.Ordinal))
        {
            if (state.ActiveTurn)
            {
                FinalizeTurn(state, timestamp, null, turns, settings);
            }
            return SessionParserContinuation.Empty with
            {
                ActiveTurn = true,
                StartedAtUtc =
                    ReadTimestamp(eventPayload, "started_at") ?? timestamp,
                Model = state.Model,
                ReasoningEffort = state.ReasoningEffort,
                ServiceTier = state.ServiceTier,
                PreviousCumulative = state.PreviousCumulative,
            };
        }

        if (string.Equals(eventType, "token_count", StringComparison.Ordinal))
        {
            return ApplyTokenCount(eventPayload, state);
        }

        if (string.Equals(eventType, "task_complete", StringComparison.Ordinal) ||
            string.Equals(eventType, "turn_completed", StringComparison.Ordinal) ||
            string.Equals(eventType, "turn_aborted", StringComparison.Ordinal))
        {
            if (!state.ActiveTurn)
            {
                return state;
            }
            var completed =
                ReadTimestamp(eventPayload, "completed_at") ?? timestamp;
            var duration = ReadLong(eventPayload, "duration_ms");
            FinalizeTurn(state, completed, duration, turns, settings);
            return SessionParserContinuation.Empty with
            {
                Model = state.Model,
                ReasoningEffort = state.ReasoningEffort,
                ServiceTier = state.ServiceTier,
                PreviousCumulative = state.PreviousCumulative,
            };
        }

        return state;
    }

    private static SessionParserContinuation ApplyTokenCount(
        JsonElement payload,
        SessionParserContinuation state)
    {
        if (!payload.TryGetProperty("info", out var info))
        {
            return state;
        }
        var cumulative = info.TryGetProperty(
            "total_token_usage",
            out var cumulativeElement)
            ? ReadTokens(cumulativeElement)
            : null;
        UsageTokenTotals delta;
        if (info.TryGetProperty("last_token_usage", out var lastElement))
        {
            delta = ReadTokens(lastElement) ?? UsageTokenTotals.Empty;
        }
        else if (cumulative is not null)
        {
            delta = Difference(cumulative, state.PreviousCumulative);
        }
        else
        {
            return state;
        }

        return state with
        {
            ActiveTurn = true,
            CurrentTokens = state.CurrentTokens + delta,
            PreviousCumulative = cumulative ?? state.PreviousCumulative,
        };
    }

    private static void FinalizeTurn(
        SessionParserContinuation state,
        DateTimeOffset endedAtUtc,
        long? exactDurationMilliseconds,
        ICollection<UsageTurnRecord> turns,
        UsageAnalyticsSettings settings)
    {
        var started = state.StartedAtUtc ?? endedAtUtc;
        var duration = exactDurationMilliseconds ??
            (long)Math.Max(0, (endedAtUtc - started).TotalMilliseconds);
        var quality = exactDurationMilliseconds is >= 0 and <= 86_400_000
            ? UsageTimeQuality.Exact
            : duration is >= 0 and <= 86_400_000
                ? UsageTimeQuality.EstimatedFromEvents
                : UsageTimeQuality.Unknown;
        if (quality == UsageTimeQuality.Unknown)
        {
            duration = 0;
        }
        turns.Add(new UsageTurnRecord(
            started,
            endedAtUtc,
            settings.CollectModel ? state.Model : "all-models",
            settings.CollectReasoningEffort
                ? state.ReasoningEffort
                : "unknown",
            settings.CollectServiceTier ? state.ServiceTier : "unknown",
            settings.CollectTokens
                ? state.CurrentTokens
                : UsageTokenTotals.Empty,
            settings.CollectElapsedTime ? duration : 0,
            settings.CollectElapsedTime
                ? quality
                : UsageTimeQuality.Unknown));
    }

    private static bool IsRelevantLine(string line) =>
        line.Contains("\"turn_context\"", StringComparison.Ordinal) ||
        line.Contains("\"token_count\"", StringComparison.Ordinal) ||
        line.Contains("\"task_started\"", StringComparison.Ordinal) ||
        line.Contains("\"task_complete\"", StringComparison.Ordinal) ||
        line.Contains("\"turn_started\"", StringComparison.Ordinal) ||
        line.Contains("\"turn_completed\"", StringComparison.Ordinal) ||
        line.Contains("\"turn_aborted\"", StringComparison.Ordinal);

    private static UsageTokenTotals? ReadTokens(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return new UsageTokenTotals(
            NonNegative(ReadLong(element, "input_tokens")),
            NonNegative(ReadLong(element, "cached_input_tokens")),
            NonNegative(ReadLong(element, "cache_write_input_tokens")),
            NonNegative(ReadLong(element, "output_tokens")),
            NonNegative(ReadLong(element, "reasoning_output_tokens")),
            NonNegative(ReadLong(element, "total_tokens")));
    }

    private static UsageTokenTotals Difference(
        UsageTokenTotals current,
        UsageTokenTotals? previous)
    {
        if (previous is null)
        {
            return current;
        }
        return new UsageTokenTotals(
            Math.Max(0, current.InputTokens - previous.InputTokens),
            Math.Max(0, current.CachedInputTokens - previous.CachedInputTokens),
            Math.Max(
                0,
                current.CacheWriteInputTokens - previous.CacheWriteInputTokens),
            Math.Max(0, current.OutputTokens - previous.OutputTokens),
            Math.Max(
                0,
                current.ReasoningOutputTokens - previous.ReasoningOutputTokens),
            Math.Max(0, current.TotalTokens - previous.TotalTokens));
    }

    private static IReadOnlyList<UsageAggregate> MergeRows(
        IEnumerable<UsageAggregate> rows) =>
        rows.GroupBy(row => row.Key)
            .Select(group =>
            {
                var tokens = group.Aggregate(
                    UsageTokenTotals.Empty,
                    (current, row) => current + row.Tokens);
                return new UsageAggregate(
                    group.Key,
                    tokens,
                    group.Sum(row => row.TurnCount),
                    group.Sum(row => row.ExactElapsedMilliseconds),
                    group.Sum(row => row.EstimatedElapsedMilliseconds),
                    group.Sum(row => row.ExactTurnCount),
                    group.Sum(row => row.EstimatedTurnCount),
                    group.Sum(row => row.UnknownTimeTurnCount));
            })
            .OrderBy(row => row.Key.LocalDate)
            .ThenBy(row => row.Key.Model, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IEnumerable<UsageAggregate> ApplyCollectionSettings(
        IEnumerable<UsageAggregate> rows,
        UsageAnalyticsSettings settings) =>
        rows.Select(row => row with
        {
            TurnCount = settings.CollectTurnCount ? row.TurnCount : 0,
            ExactElapsedMilliseconds = settings.CollectElapsedTime
                ? row.ExactElapsedMilliseconds
                : 0,
            EstimatedElapsedMilliseconds = settings.CollectElapsedTime
                ? row.EstimatedElapsedMilliseconds
                : 0,
            ExactTurnCount = settings.CollectElapsedTime
                ? row.ExactTurnCount
                : 0,
            EstimatedTurnCount = settings.CollectElapsedTime
                ? row.EstimatedTurnCount
                : 0,
            UnknownTimeTurnCount = settings.CollectElapsedTime
                ? row.UnknownTimeTurnCount
                : 0,
        });

    private static int GetParserVersion(UsageAnalyticsSettings settings)
    {
        var flags = 0;
        flags |= settings.CollectModel ? 1 << 0 : 0;
        flags |= settings.CollectReasoningEffort ? 1 << 1 : 0;
        flags |= settings.CollectServiceTier ? 1 << 2 : 0;
        flags |= settings.CollectTokens ? 1 << 3 : 0;
        flags |= settings.CollectElapsedTime ? 1 << 4 : 0;
        flags |= settings.CollectTurnCount ? 1 << 5 : 0;
        return ParserVersion * 1000 + flags;
    }

    private static string HashPath(string path) =>
        Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        Path.GetFullPath(path).ToUpperInvariant())))
            .ToLowerInvariant();

    private static async Task<string> HashSliceAsync(
        string path,
        long offset,
        long count,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant();
        }
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.Asynchronous | FileOptions.RandomAccess);
        stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[checked((int)count)];
        var read = await stream.ReadAsync(
            buffer.AsMemory(),
            cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, read)))
            .ToLowerInvariant();
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? ReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }
        return value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt64(out var number)
            ? number
            : null;
    }

    private static long NonNegative(long? value) => Math.Max(0, value ?? 0);

    private static DateTimeOffset? ReadTimestamp(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(value.GetString(), out var timestamp))
        {
            return timestamp;
        }
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out var numeric))
        {
            return numeric > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(numeric)
                : DateTimeOffset.FromUnixTimeSeconds(numeric);
        }
        return null;
    }

    private sealed record ParsedSessionFile(
        IReadOnlyList<UsageTurnRecord> Turns,
        SessionParserContinuation Continuation);
}
