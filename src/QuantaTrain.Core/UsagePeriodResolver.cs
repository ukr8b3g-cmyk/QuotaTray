namespace QuantaTrain.Core;

public static class UsagePeriodResolver
{
    public static UsagePeriod Resolve(
        string? period,
        DateTimeOffset nowUtc,
        WeeklyQuotaState? quotaState,
        IReadOnlyList<ResetEvent>? resetEvents = null,
        DateTimeOffset? customFromUtc = null,
        DateTimeOffset? customToUtc = null)
    {
        var toUtc = customToUtc ?? nowUtc;
        return period?.Trim().ToLowerInvariant() switch
        {
            "7-days" => new UsagePeriod(toUtc.AddDays(-7), toUtc, false),
            "30-days" => new UsagePeriod(toUtc.AddDays(-30), toUtc, false),
            "90-days" => new UsagePeriod(toUtc.AddDays(-90), toUtc, false),
            "180-days" => new UsagePeriod(toUtc.AddDays(-180), toUtc, false),
            "all" => new UsagePeriod(DateTimeOffset.MinValue, toUtc, false),
            "custom" when customFromUtc is not null =>
                new UsagePeriod(customFromUtc.Value, toUtc, false),
            _ => ResolveCurrentWindow(nowUtc, quotaState, resetEvents),
        };
    }

    private static UsagePeriod ResolveCurrentWindow(
        DateTimeOffset nowUtc,
        WeeklyQuotaState? quotaState,
        IReadOnlyList<ResetEvent>? resetEvents)
    {
        var latestReset = resetEvents?
            .Where(item => item.Classification is
                ResetClassification.ScheduledReset or
                ResetClassification.UnexpectedResetCandidate)
            .OrderByDescending(item => item.ObservedToUtc)
            .FirstOrDefault();
        if (latestReset is not null)
        {
            return new UsagePeriod(
                latestReset.ObservedToUtc,
                nowUtc,
                latestReset.Classification ==
                    ResetClassification.UnexpectedResetCandidate);
        }

        if (quotaState?.ResetsAtUtc is not null &&
            quotaState.WindowDurationMinutes is > 0)
        {
            return new UsagePeriod(
                quotaState.ResetsAtUtc.Value.AddMinutes(
                    -quotaState.WindowDurationMinutes.Value),
                nowUtc,
                true);
        }

        return new UsagePeriod(nowUtc.AddDays(-7), nowUtc, true);
    }
}
