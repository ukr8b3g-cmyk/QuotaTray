using QuantaTrain.Core;

namespace QuantaTrain.Core.Tests;

public sealed class UsageAnalyticsAggregatorTests
{
    [Fact]
    public void AggregatesTokensWithoutDoubleCountingCachedOrReasoningSubsets()
    {
        var now = DateTimeOffset.UtcNow;
        var turns = new[]
        {
            new UsageTurnRecord(
                now,
                now.AddSeconds(20),
                "gpt-5.6-sol",
                "high",
                "priority",
                new UsageTokenTotals(100, 80, 5, 20, 7, 125),
                20_000,
                UsageTimeQuality.Exact),
            new UsageTurnRecord(
                now.AddMinutes(1),
                now.AddMinutes(1).AddSeconds(30),
                "gpt-5.6-sol",
                "high",
                "priority",
                new UsageTokenTotals(200, 150, 0, 40, 10, 240),
                30_000,
                UsageTimeQuality.EstimatedFromEvents),
        };

        var rows = UsageAnalyticsAggregator.Aggregate(
            turns,
            TimeZoneInfo.Utc);

        var row = Assert.Single(rows);
        Assert.Equal(365, row.Tokens.EffectiveTotalTokens);
        Assert.Equal(230, row.Tokens.CachedInputTokens);
        Assert.Equal(17, row.Tokens.ReasoningOutputTokens);
        Assert.Equal(2, row.TurnCount);
        Assert.Equal(20_000, row.ExactElapsedMilliseconds);
        Assert.Equal(30_000, row.EstimatedElapsedMilliseconds);
        Assert.Equal("fast", row.Key.ServiceTier);
    }

    [Fact]
    public void SevenModelsBecomeTopFiveAndOther()
    {
        var rows = Enumerable.Range(1, 7)
            .Select(index => new UsageAggregate(
                new UsageAggregateKey(
                    new DateOnly(2026, 7, 26),
                    $"model-{index}",
                    "medium",
                    "standard"),
                new UsageTokenTotals(index * 10, 0, 0, 0, 0, index * 10),
                1,
                0,
                0,
                0,
                0,
                1))
            .ToArray();

        var summaries = UsageAnalyticsAggregator.SummarizeModels(rows, 5, true);

        Assert.Equal(6, summaries.Count);
        Assert.Equal("model-7", summaries[0].Model);
        Assert.Equal("other", summaries[^1].Model);
        Assert.Equal(30, summaries[^1].Tokens.EffectiveTotalTokens);
    }

    [Fact]
    public void SixModelsRemainSixIndividualRows()
    {
        var rows = Enumerable.Range(1, 6)
            .Select(index => new UsageAggregate(
                new UsageAggregateKey(
                    new DateOnly(2026, 7, 26),
                    $"model-{index}",
                    "medium",
                    "standard"),
                new UsageTokenTotals(index, 0, 0, 0, 0, index),
                1,
                0,
                0,
                0,
                0,
                1));

        var summaries = UsageAnalyticsAggregator.SummarizeModels(rows, 5, true);

        Assert.Equal(6, summaries.Count);
        Assert.DoesNotContain(summaries, item => item.Model == "other");
    }

    [Theory]
    [InlineData("low", "low")]
    [InlineData("xhigh", "maximum")]
    [InlineData("Ultra", "ultra")]
    [InlineData("future-value", "unknown")]
    public void NormalizesReasoningEffort(string input, string expected)
    {
        Assert.Equal(
            expected,
            UsageAnalyticsAggregator.NormalizeReasoningEffort(input));
    }
}
