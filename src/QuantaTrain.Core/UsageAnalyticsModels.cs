namespace QuantaTrain.Core;

public enum UsageTimeQuality
{
    Unknown,
    EstimatedFromEvents,
    Exact,
}

public sealed record UsageTokenTotals(
    long InputTokens,
    long CachedInputTokens,
    long CacheWriteInputTokens,
    long OutputTokens,
    long ReasoningOutputTokens,
    long TotalTokens)
{
    public static readonly UsageTokenTotals Empty = new(0, 0, 0, 0, 0, 0);

    public long EffectiveTotalTokens =>
        TotalTokens > 0
            ? TotalTokens
            : SafeAdd(InputTokens, CacheWriteInputTokens, OutputTokens);

    public static UsageTokenTotals operator +(
        UsageTokenTotals left,
        UsageTokenTotals right) =>
        new(
            SafeAdd(left.InputTokens, right.InputTokens),
            SafeAdd(left.CachedInputTokens, right.CachedInputTokens),
            SafeAdd(left.CacheWriteInputTokens, right.CacheWriteInputTokens),
            SafeAdd(left.OutputTokens, right.OutputTokens),
            SafeAdd(left.ReasoningOutputTokens, right.ReasoningOutputTokens),
            SafeAdd(left.TotalTokens, right.TotalTokens));

    private static long SafeAdd(params long[] values)
    {
        var total = 0L;
        foreach (var value in values)
        {
            total = checked(total + Math.Max(0, value));
        }
        return total;
    }
}

public sealed record UsageTurnRecord(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    string Model,
    string ReasoningEffort,
    string ServiceTier,
    UsageTokenTotals Tokens,
    long ElapsedMilliseconds,
    UsageTimeQuality TimeQuality);

public sealed record UsageAggregateKey(
    DateOnly LocalDate,
    string Model,
    string ReasoningEffort,
    string ServiceTier);

public sealed record UsageAggregate(
    UsageAggregateKey Key,
    UsageTokenTotals Tokens,
    long TurnCount,
    long ExactElapsedMilliseconds,
    long EstimatedElapsedMilliseconds,
    long ExactTurnCount,
    long EstimatedTurnCount,
    long UnknownTimeTurnCount);

public sealed record UsageModelSummary(
    string Model,
    UsageTokenTotals Tokens,
    long ElapsedMilliseconds,
    long TurnCount,
    IReadOnlyDictionary<string, long> ReasoningTokens,
    IReadOnlyDictionary<string, long> ServiceTierTokens);

public sealed record UsageAnalysisSnapshot(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    bool IsPeriodStartEstimated,
    IReadOnlyList<UsageAggregate> Rows,
    DateTimeOffset RefreshedAtUtc,
    int ScannedFileCount,
    int SkippedFileCount,
    int ErrorFileCount,
    IReadOnlyList<LocalActivityAggregate>? Activities = null)
{
    public static UsageAnalysisSnapshot Empty(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc) =>
        new(fromUtc, toUtc, true, [], DateTimeOffset.UtcNow, 0, 0, 0);
}

public sealed record UsagePeriod(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    bool IsStartEstimated);

public sealed record AccountDailyUsage(DateOnly Date, long Tokens);

public sealed record AccountUsageSnapshot(
    DateTimeOffset ObservedAtUtc,
    long? LifetimeTokens,
    long? PeakDailyTokens,
    long? LongestRunningTurnSeconds,
    int? CurrentStreakDays,
    int? LongestStreakDays,
    IReadOnlyList<AccountDailyUsage> DailyUsage);

public enum LocalActivityKind
{
    Tool,
    Skill,
}

public sealed record LocalActivityAggregate(
    DateOnly LocalDate,
    LocalActivityKind Kind,
    string Name,
    long Count);
