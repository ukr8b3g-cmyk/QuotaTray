namespace QuantaTrain.Core;

public static class ResetClassifier
{
    private static readonly TimeSpan ScheduledTolerance = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LongOfflineInterval = TimeSpan.FromHours(24);

    public static ResetEvent? Classify(
        WeeklyQuotaState before,
        WeeklyQuotaState after,
        bool confirmed = true)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var usedDrop = before.UsedPercent - after.UsedPercent;
        var largeRecovery = usedDrop >= 20 ||
            (before.UsedPercent >= 15 && after.UsedPercent <= 5) ||
            after.RemainingPercent - before.RemainingPercent >= 20;

        if (!largeRecovery)
        {
            return null;
        }

        var reasons = new List<string> { "large-recovery" };
        var classification = ResetClassification.UncertainChange;
        var confidence = confirmed ? Confidence.Medium : Confidence.Low;

        if (before.LimitId != after.LimitId ||
            before.WindowDurationMinutes != after.WindowDurationMinutes ||
            !string.Equals(before.PlanType, after.PlanType, StringComparison.Ordinal))
        {
            classification = ResetClassification.LimitPolicyChange;
            reasons.Add("limit-configuration-changed");
        }
        else if (after.ObservedAtUtc - before.ObservedAtUtc > LongOfflineInterval)
        {
            reasons.Add("long-offline-interval");
        }
        else if (IsScheduled(before, after))
        {
            classification = ResetClassification.ScheduledReset;
            confidence = confirmed ? Confidence.High : Confidence.Medium;
            reasons.Add("scheduled-window");
        }
        else if (CreditsDecreasedWithoutExpiry(before, after))
        {
            classification = ResetClassification.ResetCreditLikely;
            reasons.Add("reset-credit-count-decreased");
        }
        else if (confirmed)
        {
            classification = ResetClassification.UnexpectedResetCandidate;
            reasons.Add("no-reset-credit-decrease");
        }
        else
        {
            reasons.Add("confirmation-pending");
        }

        return new ResetEvent(
            before.ObservedAtUtc,
            after.ObservedAtUtc,
            classification,
            confidence,
            before,
            after,
            reasons,
            confirmed);
    }

    private static bool IsScheduled(WeeklyQuotaState before, WeeklyQuotaState after)
    {
        if (before.ResetsAtUtc is null)
        {
            return false;
        }

        var reset = before.ResetsAtUtc.Value;
        return reset >= before.ObservedAtUtc - ScheduledTolerance &&
            reset <= after.ObservedAtUtc + ScheduledTolerance;
    }

    private static bool CreditsDecreasedWithoutExpiry(
        WeeklyQuotaState before,
        WeeklyQuotaState after)
    {
        if (before.ResetCreditCount is null || after.ResetCreditCount is null ||
            after.ResetCreditCount >= before.ResetCreditCount)
        {
            return false;
        }

        var expired = before.ResetCredits?.Any(credit =>
            credit.ExpiresAtUtc is not null &&
            credit.ExpiresAtUtc >= before.ObservedAtUtc &&
            credit.ExpiresAtUtc <= after.ObservedAtUtc) == true;
        return !expired;
    }
}
