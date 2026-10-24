using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class PollingRefreshTests
{
    [Theory]
    [InlineData("timeout")]
    [InlineData("missing")]
    [InlineData("disconnect")]
    public async Task FailedRefreshRetainsDisplayCacheButExplicitlyReportsFailure(string failure)
    {
        var reads = 0;
        await using var polling = new PollingCoordinator(_ =>
        {
            if (reads++ == 0) return Task.FromResult(Snapshot(0));
            return failure switch
            {
                "missing" => Task.FromResult(Snapshot(0) with { Buckets = [] }),
                "timeout" => Task.FromException<RateLimitSnapshot>(new TimeoutException("timeout")),
                _ => Task.FromException<RateLimitSnapshot>(new IOException("disconnected")),
            };
        }, TimeSpan.FromHours(1));
        var candidate = await polling.RefreshAsync(CancellationToken.None);
        var failed = await polling.RefreshAsync(CancellationToken.None);
        Assert.True(candidate.Succeeded);
        Assert.False(failed.Succeeded);
        Assert.NotNull(failed.Error);
        Assert.Same(candidate.State, failed.State);
        Assert.Equal(candidate.SuccessSequence, failed.SuccessSequence);
        Assert.True(failed.RefreshSequence > candidate.RefreshSequence);
        Assert.Equal(failed.RefreshSequence, polling.RefreshSequence);
        Assert.Same(candidate.State, polling.Current);
    }

    [Fact]
    public async Task EqualSuccessfulValuesAreNewObservationsAndConcurrentReadsCoalesce()
    {
        var completion = new TaskCompletionSource<RateLimitSnapshot>();
        var reads = 0;
        await using var polling = new PollingCoordinator(_ =>
        {
            reads++;
            return reads == 1 ? Task.FromResult(Snapshot(0)) : completion.Task;
        }, TimeSpan.FromHours(1));
        var first = await polling.RefreshAsync(CancellationToken.None);
        var secondTask = polling.RefreshAsync(CancellationToken.None);
        Assert.True(polling.RefreshSequence > first.RefreshSequence);
        Assert.Same(secondTask, polling.RefreshAsync(CancellationToken.None));
        completion.SetResult(Snapshot(0));
        var second = await secondTask;
        Assert.True(second.Succeeded);
        Assert.True(second.SuccessSequence > first.SuccessSequence);
        Assert.Equal(second.RefreshSequence, polling.RefreshSequence);
        Assert.Equal(2, reads);
    }

    private static RateLimitSnapshot Snapshot(double used) => new(
        DateTimeOffset.UtcNow, [new("weekly", BucketRole.Secondary, used, 10080,
            DateTimeOffset.UtcNow.AddDays(3))], 1, [], "pro", "test");
}
