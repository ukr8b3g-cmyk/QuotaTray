using QuantaTrain.Core;

namespace QuantaTrain.Core.Tests;

public sealed class WeeklyBucketSelectorTests
{
    [Fact]
    public void SelectsExactWeeklyWindow()
    {
        var selected = WeeklyBucketSelector.Select(
        [
            Bucket(300, BucketRole.Primary),
            Bucket(10080, BucketRole.Secondary),
        ]);

        Assert.NotNull(selected);
        Assert.Equal(10080, selected.WindowDurationMinutes);
        Assert.Equal(BucketRole.Secondary, selected.Role);
    }

    [Fact]
    public void PrefersPrimaryWhenDistancesMatch()
    {
        var selected = WeeklyBucketSelector.Select(
        [
            Bucket(10070, BucketRole.Secondary),
            Bucket(10090, BucketRole.Primary),
        ]);

        Assert.Equal(BucketRole.Primary, selected?.Role);
    }

    [Fact]
    public void SelectsTheSameLimitWhenExactWeeklyBucketsHaveEqualPriority()
    {
        var first = WeeklyBucketSelector.Select(
        [
            Bucket("codex_bengalfox", 10080, BucketRole.Primary, 0),
            Bucket("codex", 10080, BucketRole.Primary, 93),
        ]);
        var second = WeeklyBucketSelector.Select(
        [
            Bucket("codex", 10080, BucketRole.Primary, 93),
            Bucket("codex_bengalfox", 10080, BucketRole.Primary, 0),
        ]);

        Assert.Equal("codex", first?.LimitId);
        Assert.Equal(93, first?.UsedPercent);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ReturnsNullWhenNoWeeklyWindowExists()
    {
        var selected = WeeklyBucketSelector.Select([Bucket(300, BucketRole.Primary)]);

        Assert.Null(selected);
    }

    [Theory]
    [InlineData(-10, 100)]
    [InlineData(0, 100)]
    [InlineData(52.5, 47.5)]
    [InlineData(120, 0)]
    public void RemainingIsClamped(double used, double expected)
    {
        Assert.Equal(expected, RemainingCalculator.FromUsedPercent(used));
    }

    private static RateLimitBucket Bucket(long minutes, BucketRole role) =>
        new("codex", role, 25, minutes, DateTimeOffset.UtcNow.AddDays(1));

    private static RateLimitBucket Bucket(
        string limitId,
        long minutes,
        BucketRole role,
        double usedPercent) =>
        new(limitId, role, usedPercent, minutes, DateTimeOffset.UnixEpoch);
}
