using System.Text.Json;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class CodexAccountParsingTests
{
    [Fact]
    public void ParsesPurchasedCreditsWithoutConfusingUnavailableAndZero()
    {
        using var document = JsonDocument.Parse(
            """{"rateLimits":{"limitId":"codex","primary":{"usedPercent":2,"windowDurationMins":10080},"credits":{"balance":"0","hasCredits":false,"unlimited":false}}}""");

        var snapshot = CodexAccountClient.ParseRateLimits(
            document.RootElement,
            DateTimeOffset.Parse("2026-08-28T00:00:00Z"),
            "test");

        Assert.NotNull(snapshot.PurchasedCredits);
        Assert.Equal("0", snapshot.PurchasedCredits.Balance);
        Assert.False(snapshot.PurchasedCredits.HasCredits);
        Assert.False(snapshot.PurchasedCredits.Unlimited);
    }

    [Fact]
    public void ParsesOfficialAccountUsageSummaryAndDailyBuckets()
    {
        using var document = JsonDocument.Parse(
            """{"summary":{"lifetimeTokens":563,"peakDailyTokens":180,"longestRunningTurnSec":92,"currentStreakDays":4,"longestStreakDays":7},"dailyUsageBuckets":[{"startDate":"2026-08-27","tokens":40},{"startDate":"2026-08-28","tokens":23}]}""");

        var usage = CodexAccountClient.ParseUsage(
            document.RootElement,
            DateTimeOffset.Parse("2026-08-28T00:00:00Z"));

        Assert.Equal(563, usage.LifetimeTokens);
        Assert.Equal(180, usage.PeakDailyTokens);
        Assert.Equal(4, usage.CurrentStreakDays);
        Assert.Equal(2, usage.DailyUsage.Count);
        Assert.Equal(23, usage.DailyUsage[1].Tokens);
    }
}
