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
        var windowStart = quotaState?.ResetsAtUtc > nowUtc &&
                          quotaState.WindowDurationMinutes is > 0
            ? quotaState.ResetsAtUtc.Value.AddMinutes(-quotaState.WindowDurationMinutes.Value)
            : nowUtc.AddDays(-7);
        if (windowStart > nowUtc) windowStart = nowUtc.AddDays(-7);
        var latestReset = resetEvents?
            .Where(item => item.Confirmed &&
                item.ObservedToUtc >= windowStart && item.ObservedToUtc <= nowUtc &&
                item.ObservedFromUtc <= item.ObservedToUtc &&
                (quotaState is null ||
                 item.After.LimitId == quotaState.LimitId &&
                 item.After.Role == quotaState.Role &&
                 item.After.WindowDurationMinutes == quotaState.WindowDurationMinutes &&
                 item.After.PlanType == quotaState.PlanType))
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

        return new UsagePeriod(windowStart, nowUtc, true);
    }
}
