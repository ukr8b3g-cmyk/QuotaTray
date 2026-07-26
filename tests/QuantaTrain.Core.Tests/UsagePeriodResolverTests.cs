using QuantaTrain.Core;

namespace QuantaTrain.Core.Tests;

public sealed class UsagePeriodResolverTests
{
    [Fact]
    public void CurrentWindowUsesQuotaDurationWhenHistoryIsUnavailable()
    {
        var now = DateTimeOffset.Parse("2026-07-26T10:00:00Z");
        var quota = new WeeklyQuotaState(
            "weekly",
            BucketRole.Secondary,
            50,
            50,
            10080,
            DateTimeOffset.Parse("2026-07-29T00:00:00Z"),
            1,
            [],
            "pro",
            now,
            "0.145.0");

        var period = UsagePeriodResolver.Resolve(
            "current-window",
            now,
            quota);

        Assert.Equal(
            DateTimeOffset.Parse("2026-07-22T00:00:00Z"),
            period.FromUtc);
        Assert.True(period.IsStartEstimated);
    }
}
