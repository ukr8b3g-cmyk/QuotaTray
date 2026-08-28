namespace QuantaTrain.Core;

public static class WeeklyBucketSelector
{
    public const long WeeklyWindowMinutes = 7 * 24 * 60;
    private const long MaximumDistanceMinutes = 24 * 60;

    public static RateLimitBucket? Select(IEnumerable<RateLimitBucket> buckets)
    {
        ArgumentNullException.ThrowIfNull(buckets);

        return buckets
            .Where(bucket => bucket.WindowDurationMinutes is not null)
            .Select(bucket => new
            {
                Bucket = bucket,
                Distance = Math.Abs(bucket.WindowDurationMinutes!.Value - WeeklyWindowMinutes),
                RoleOrder = bucket.Role == BucketRole.Primary ? 0 : 1,
            })
            .Where(candidate => candidate.Distance <= MaximumDistanceMinutes)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.RoleOrder)
            // App Server can expose multiple exact seven-day limits. Keep the
            // choice deterministic instead of depending on JSON property order.
            .ThenBy(
                candidate => candidate.Bucket.LimitId ?? "\uffff",
                StringComparer.Ordinal)
            .Select(candidate => candidate.Bucket)
            .FirstOrDefault();
    }

    public static WeeklyQuotaState? BuildState(RateLimitSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var bucket = Select(snapshot.Buckets);
        if (bucket is null)
        {
            return null;
        }

        return new WeeklyQuotaState(
            bucket.LimitId,
            bucket.Role,
            bucket.UsedPercent,
            RemainingCalculator.FromUsedPercent(bucket.UsedPercent),
            bucket.WindowDurationMinutes,
            bucket.ResetsAtUtc,
            snapshot.ResetCreditCount,
            snapshot.ResetCredits,
            snapshot.PlanType,
            snapshot.ObservedAtUtc,
            snapshot.CodexVersion,
            snapshot.PurchasedCredits);
    }
}
