namespace QuantaTrain.Core;

public enum BucketRole
{
    Unknown,
    Primary,
    Secondary,
}

public sealed record RateLimitBucket(
    string? LimitId,
    BucketRole Role,
    double UsedPercent,
    long? WindowDurationMinutes,
    DateTimeOffset? ResetsAtUtc);

public sealed record ResetCredit(DateTimeOffset? ExpiresAtUtc);

public sealed record PurchasedCreditsSnapshot(
    string? Balance,
    bool HasCredits,
    bool Unlimited);

public sealed record RateLimitSnapshot(
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<RateLimitBucket> Buckets,
    long? ResetCreditCount,
    IReadOnlyList<ResetCredit>? ResetCredits,
    string? PlanType,
    string? CodexVersion,
    PurchasedCreditsSnapshot? PurchasedCredits = null);

public sealed record WeeklyQuotaState(
    string? LimitId,
    BucketRole Role,
    double UsedPercent,
    double RemainingPercent,
    long? WindowDurationMinutes,
    DateTimeOffset? ResetsAtUtc,
    long? ResetCreditCount,
    IReadOnlyList<ResetCredit>? ResetCredits,
    string? PlanType,
    DateTimeOffset ObservedAtUtc,
    string? CodexVersion,
    PurchasedCreditsSnapshot? PurchasedCredits = null);

public enum ResetClassification
{
    None,
    ScheduledReset,
    ResetCreditLikely,
    UnexpectedResetCandidate,
    LimitPolicyChange,
    UncertainChange,
}

public enum Confidence
{
    Low,
    Medium,
    High,
}

public sealed record ResetEvent(
    DateTimeOffset ObservedFromUtc,
    DateTimeOffset ObservedToUtc,
    ResetClassification Classification,
    Confidence Confidence,
    WeeklyQuotaState Before,
    WeeklyQuotaState After,
    IReadOnlyList<string> ReasonCodes,
    bool Confirmed);
