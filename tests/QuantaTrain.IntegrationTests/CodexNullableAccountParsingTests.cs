using System.Text.Json;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class CodexNullableAccountParsingTests
{
    private static readonly DateTimeOffset ObservedAt =
        DateTimeOffset.Parse("2026-10-06T00:00:00Z");

    [Fact]
    public void SchemaNullableWindowAndCreditMetadataPreservesWeeklyQuota()
    {
        var snapshot = Parse("""
            {
              "rateLimits": {
                "limitId": "codex",
                "primary": {"usedPercent": 25, "windowDurationMins": null, "resetsAt": null},
                "secondary": {"usedPercent": 40, "windowDurationMins": 10080, "resetsAt": null}
              },
              "rateLimitResetCredits": {"availableCount": 1, "credits": [{"expiresAt": null}]}
            }
            """);

        Assert.Equal(2, snapshot.Buckets.Count);
        Assert.Null(snapshot.Buckets[0].WindowDurationMinutes);
        Assert.All(snapshot.Buckets, bucket => Assert.Null(bucket.ResetsAtUtc));
        Assert.Equal(1, snapshot.ResetCreditCount);
        Assert.Null(Assert.Single(snapshot.ResetCredits!).ExpiresAtUtc);
        var weekly = Assert.IsType<WeeklyQuotaState>(WeeklyBucketSelector.BuildState(snapshot));
        Assert.Equal(60, weekly.RemainingPercent);
        Assert.Equal(BucketRole.Secondary, weekly.Role);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("\"10080\"")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("1.5")]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775808")]
    [InlineData("0")]
    [InlineData("-1")]
    public void InvalidOptionalDurationDoesNotDiscardOtherWindows(string duration)
    {
        var snapshot = Parse($$"""
            {"rateLimits": {
              "primary": {"usedPercent": 25, "windowDurationMins": {{duration}}},
              "secondary": {"usedPercent": 40, "windowDurationMins": 10080}
            } }
            """);

        Assert.Equal(2, snapshot.Buckets.Count);
        Assert.Null(snapshot.Buckets[0].WindowDurationMinutes);
        Assert.Equal(60, WeeklyBucketSelector.BuildState(snapshot)!.RemainingPercent);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("\"1791244800\"")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("1.5")]
    [InlineData("253402300800")]
    [InlineData("-62135596801")]
    [InlineData("9223372036854775807")]
    [InlineData("9223372036854775808")]
    [InlineData("1e100")]
    public void InvalidOptionalTimestampsDoNotDiscardWindowsOrCredits(string timestamp)
    {
        var snapshot = Parse($$"""
            {
              "rateLimits": {"primary": {"usedPercent": 25, "windowDurationMins": 10080, "resetsAt": {{timestamp}} } },
              "rateLimitResetCredits": {"availableCount": 2, "credits": [
                {"expiresAt": {{timestamp}}}, {"expiresAt": 1791244800}
              ]}
            }
            """);

        Assert.Null(Assert.Single(snapshot.Buckets).ResetsAtUtc);
        Assert.NotNull(WeeklyBucketSelector.BuildState(snapshot));
        Assert.Equal(2, snapshot.ResetCreditCount);
        Assert.Equal(2, snapshot.ResetCredits!.Count);
        Assert.Null(snapshot.ResetCredits[0].ExpiresAtUtc);
        Assert.Equal(ObservedAt, snapshot.ResetCredits[1].ExpiresAtUtc);
    }

    [Theory]
    [InlineData(-62135596800L)]
    [InlineData(253402300799L)]
    public void RepresentableTimestampBoundariesArePreserved(long timestamp)
    {
        var snapshot = Parse($$"""
            {"rateLimits": {"primary": {"usedPercent": 25, "windowDurationMins": 10080, "resetsAt": {{timestamp}} } } }
            """);

        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(timestamp),
            Assert.Single(snapshot.Buckets).ResetsAtUtc);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("\"25\"")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("1e10000")]
    public void InvalidUsedPercentOnlySkipsItsOwnWindow(string usedPercent)
    {
        var snapshot = Parse($$"""
            {"rateLimits": {
              "primary": {"usedPercent": {{usedPercent}}, "windowDurationMins": 10080},
              "secondary": {"usedPercent": 40, "windowDurationMins": 10080}
            } }
            """);

        Assert.Equal(BucketRole.Secondary, Assert.Single(snapshot.Buckets).Role);
        Assert.Equal(60, WeeklyBucketSelector.BuildState(snapshot)!.RemainingPercent);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("\"unknown\"")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("1.5")]
    [InlineData("9223372036854775808")]
    [InlineData("-1")]
    public void InvalidResetCountPreservesUsableQuota(string count)
    {
        var snapshot = Parse($$"""
            {
              "rateLimitsByLimitId": {
                "invalid": null,
                "codex": {"primary": {"usedPercent": 25, "windowDurationMins": 10080} }
              },
              "rateLimitResetCredits": {"availableCount": {{count}}, "credits": [null, [], 4, {"expiresAt": null}]}
            }
            """);

        Assert.Equal("codex", Assert.Single(snapshot.Buckets).LimitId);
        Assert.Null(snapshot.ResetCreditCount);
        Assert.Null(Assert.Single(snapshot.ResetCredits!).ExpiresAtUtc);
    }

    [Fact]
    public void KeepsShortWindowsAndExplicitReasonsWithTheirOwnLimit()
    {
        var snapshot = Parse("""
            {"rateLimitsByLimitId": {
              "a": {"limitName":"Example","primary":{"usedPercent":24,"windowDurationMins":300,"resetsAt":1800000000},
                    "secondary":{"usedPercent":50,"windowDurationMins":10080},"spendControlReached":false},
              "b": {"spendControlReached":true,"rateLimitReachedType":"workspace_member_usage_limit_reached"},
              "c": {"spendControlReached":null,"primary":{"usedPercent":100,"windowDurationMins":60}}
            }}
            """);
        Assert.Equal(50, WeeklyBucketSelector.BuildState(snapshot)!.RemainingPercent);
        var details = snapshot.AdditionalLimits!;
        Assert.Equal(2, details.Count);
        var window = Assert.Single(details[0].FiveHourWindows);
        Assert.Equal("Example", details[0].Name);
        Assert.Equal(24, window.UsedPercent);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1800000000), window.ResetsAtUtc);
        Assert.Equal("b", details[1].Name);
        Assert.True(details[1].SpendControlReached);
        Assert.Equal("workspace_member_usage_limit_reached", details[1].ReachedType);
    }

    [Fact]
    public void MalformedDailyUsageRowsDoNotDiscardValidRows()
    {
        using var document = JsonDocument.Parse("""
            {"summary": {"lifetimeTokens": null}, "dailyUsageBuckets": [
              null, [], false, {"startDate": "2026-10-06", "tokens": 50}
            ]}
            """);

        var usage = CodexAccountClient.ParseUsage(document.RootElement, ObservedAt);

        Assert.Null(usage.LifetimeTokens);
        Assert.Equal(50, Assert.Single(usage.DailyUsage).Tokens);
    }

    private static RateLimitSnapshot Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return CodexAccountClient.ParseRateLimits(document.RootElement, ObservedAt, "test");
    }
}
