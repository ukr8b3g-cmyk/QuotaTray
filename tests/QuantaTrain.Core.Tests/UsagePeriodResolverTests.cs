using QuantaTrain.Core;

namespace QuantaTrain.Core.Tests;

public sealed class UsagePeriodResolverTests
{
    [Theory]
    [InlineData("old")]
    [InlineData("future")]
    [InlineData("limit")]
    [InlineData("plan")]
    [InlineData("window")]
    [InlineData("unconfirmed")]
    public void InvalidResetCannotOverrideCurrentQuotaWindow(string scenario)
    {
        var now = DateTimeOffset.Parse("2026-10-24T00:00:00Z");
        var quota = Quota(now);
        var after = quota with
        {
            LimitId = scenario == "limit" ? "other" : quota.LimitId,
            PlanType = scenario == "plan" ? "other" : quota.PlanType,
            WindowDurationMinutes = scenario == "window" ? 60 : quota.WindowDurationMinutes,
        };
        var observed = scenario switch
        {
            "old" => DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            "future" => now.AddDays(1),
            _ => now.AddDays(-1),
        };
        var reset = new ResetEvent(observed.AddMinutes(-1), observed,
            ResetClassification.ScheduledReset, Confidence.High, after, after, [],
            scenario != "unconfirmed");
        var period = UsagePeriodResolver.Resolve("current-window", now, quota, [reset]);
        Assert.Equal(DateTimeOffset.Parse("2026-10-21T00:00:00Z"), period.FromUtc);
        Assert.True(period.IsStartEstimated);
    }

    [Theory]
    [InlineData(ResetClassification.ScheduledReset, false)]
    [InlineData(ResetClassification.UnexpectedResetCandidate, true)]
    public void ConfirmedMatchingResetWithinCurrentWindowIsUsed(
        ResetClassification classification, bool estimated)
    {
        var now = DateTimeOffset.Parse("2026-10-24T00:00:00Z");
        var quota = Quota(now);
        var observed = now.AddDays(-2);
        var reset = new ResetEvent(observed.AddMinutes(-1), observed,
            classification, Confidence.High, quota, quota, [], true);
        var period = UsagePeriodResolver.Resolve("current-window", now, quota, [reset]);
        Assert.Equal(observed, period.FromUtc);
        Assert.Equal(estimated, period.IsStartEstimated);
    }

    private static WeeklyQuotaState Quota(DateTimeOffset now) => new(
        "weekly", BucketRole.Secondary, 10, 90, 10080,
        DateTimeOffset.Parse("2026-10-28T00:00:00Z"), 1, [], "pro", now, "test");

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
