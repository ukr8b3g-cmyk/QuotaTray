namespace QuantaTrain.Core;

public static class UsageAnalyticsAggregator
{
    public static IReadOnlyList<UsageAggregate> Aggregate(
        IEnumerable<UsageTurnRecord> turns,
        TimeZoneInfo? localTimeZone = null)
    {
        ArgumentNullException.ThrowIfNull(turns);
        localTimeZone ??= TimeZoneInfo.Local;

        return turns
            .Where(turn => turn.EndedAtUtc >= turn.StartedAtUtc)
            .GroupBy(turn => new UsageAggregateKey(
                DateOnly.FromDateTime(
                    TimeZoneInfo.ConvertTime(turn.EndedAtUtc, localTimeZone).Date),
                NormalizeModel(turn.Model),
                NormalizeReasoningEffort(turn.ReasoningEffort),
                NormalizeServiceTier(turn.ServiceTier)))
            .Select(group =>
            {
                var tokens = group.Aggregate(
                    UsageTokenTotals.Empty,
                    (current, turn) => current + turn.Tokens);
                return new UsageAggregate(
                    group.Key,
                    tokens,
                    group.LongCount(),
                    group.Where(turn => turn.TimeQuality == UsageTimeQuality.Exact)
                        .Sum(turn => Math.Max(0, turn.ElapsedMilliseconds)),
                    group.Where(turn =>
                            turn.TimeQuality == UsageTimeQuality.EstimatedFromEvents)
                        .Sum(turn => Math.Max(0, turn.ElapsedMilliseconds)),
                    group.LongCount(turn =>
                        turn.TimeQuality == UsageTimeQuality.Exact),
                    group.LongCount(turn =>
                        turn.TimeQuality == UsageTimeQuality.EstimatedFromEvents),
                    group.LongCount(turn =>
                        turn.TimeQuality == UsageTimeQuality.Unknown));
            })
            .OrderBy(row => row.Key.LocalDate)
            .ThenBy(row => row.Key.Model, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Key.ReasoningEffort, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Key.ServiceTier, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<UsageModelSummary> SummarizeModels(
        IEnumerable<UsageAggregate> rows,
        int maximumIndividualModels,
        bool groupOtherModels)
    {
        ArgumentNullException.ThrowIfNull(rows);
        maximumIndividualModels = Math.Clamp(maximumIndividualModels, 1, 5);

        var models = rows
            .GroupBy(row => row.Key.Model, StringComparer.OrdinalIgnoreCase)
            .Select(group => Summarize(group.Key, group))
            .OrderByDescending(model => model.Tokens.EffectiveTotalTokens)
            .ThenBy(model => model.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!groupOtherModels || models.Count <= maximumIndividualModels + 1)
        {
            return models.Take(6).ToArray();
        }

        var visible = models.Take(maximumIndividualModels).ToList();
        visible.Add(MergeSummaries("other", models.Skip(maximumIndividualModels)));
        return visible;
    }

    public static string NormalizeReasoningEffort(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "minimal" or "low" => "low",
            "medium" => "medium",
            "high" => "high",
            "xhigh" or "max" or "maximum" => "maximum",
            "ultra" => "ultra",
            _ => "unknown",
        };

    public static string NormalizeServiceTier(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "standard" or "default" => "standard",
            "fast" or "priority" => "fast",
            _ => "unknown",
        };

    private static string NormalizeModel(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();

    private static UsageModelSummary Summarize(
        string model,
        IEnumerable<UsageAggregate> rows)
    {
        var materialized = rows.ToArray();
        var tokens = materialized.Aggregate(
            UsageTokenTotals.Empty,
            (current, row) => current + row.Tokens);
        var reasoning = materialized
            .GroupBy(row => row.Key.ReasoningEffort)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(row => row.Tokens.EffectiveTotalTokens),
                StringComparer.OrdinalIgnoreCase);
        var tiers = materialized
            .GroupBy(row => row.Key.ServiceTier)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(row => row.Tokens.EffectiveTotalTokens),
                StringComparer.OrdinalIgnoreCase);
        return new UsageModelSummary(
            model,
            tokens,
            materialized.Sum(row =>
                row.ExactElapsedMilliseconds + row.EstimatedElapsedMilliseconds),
            materialized.Sum(row => row.TurnCount),
            reasoning,
            tiers);
    }

    private static UsageModelSummary MergeSummaries(
        string model,
        IEnumerable<UsageModelSummary> summaries)
    {
        var materialized = summaries.ToArray();
        return new UsageModelSummary(
            model,
            materialized.Aggregate(
                UsageTokenTotals.Empty,
                (current, summary) => current + summary.Tokens),
            materialized.Sum(summary => summary.ElapsedMilliseconds),
            materialized.Sum(summary => summary.TurnCount),
            MergeBreakdowns(materialized.Select(summary => summary.ReasoningTokens)),
            MergeBreakdowns(
                materialized.Select(summary => summary.ServiceTierTokens)));
    }

    private static IReadOnlyDictionary<string, long> MergeBreakdowns(
        IEnumerable<IReadOnlyDictionary<string, long>> breakdowns)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var breakdown in breakdowns)
        {
            foreach (var (key, value) in breakdown)
            {
                result[key] = result.TryGetValue(key, out var existing)
                    ? checked(existing + value)
                    : value;
            }
        }
        return result;
    }
}
