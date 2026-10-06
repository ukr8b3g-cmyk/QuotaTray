using System.Text.Json;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class AccountUsageRefresherTests : IDisposable
{
    private const string PrivateMessage = "synthetic-private-account-payload";
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "QuantaTray-usage-refresh-tests-" + Guid.NewGuid().ToString("N"));
    private static readonly AccountUsageSnapshot Previous = new(
        DateTimeOffset.Parse("2026-10-06T00:00:00Z"), 100, 50, 10, 1, 2, []);

    [Fact]
    public async Task SuccessfulReadReplacesPreviousSnapshot()
    {
        var next = Previous with { LifetimeTokens = 150 };
        var result = await AccountUsageRefresher.TryReadAsync(
            _ => Task.FromResult(next), new RedactedLogger(_directory), CancellationToken.None) ?? Previous;

        Assert.Same(next, result);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData("rpc")]
    [InlineData("rpc-auth")]
    [InlineData("rpc-server")]
    [InlineData("timeout")]
    [InlineData("io")]
    [InlineData("invalid-operation")]
    [InlineData("json")]
    [InlineData("format")]
    public async Task ExpectedFailuresKeepPreviousSnapshotAndExcludeRawMessages(string failure)
    {
        Exception exception = failure switch
        {
            "rpc" => new AppServerRpcException(-32601, PrivateMessage),
            "rpc-auth" => new AppServerRpcException(401, PrivateMessage),
            "rpc-server" => new AppServerRpcException(-32000, PrivateMessage),
            "timeout" => new TimeoutException(PrivateMessage),
            "io" => new IOException(PrivateMessage),
            "invalid-operation" => new InvalidOperationException(PrivateMessage),
            "json" => new JsonException(PrivateMessage),
            _ => new FormatException(PrivateMessage),
        };

        var result = await AccountUsageRefresher.TryReadAsync(
            _ => Task.FromException<AccountUsageSnapshot>(exception),
            new RedactedLogger(_directory), CancellationToken.None) ?? Previous;

        Assert.Same(Previous, result);
        var log = await File.ReadAllTextAsync(Path.Combine(_directory, "quantatray.log"));
        Assert.Contains("Previous snapshot retained.", log);
        Assert.DoesNotContain(PrivateMessage, log);
        Assert.Contains(exception is AppServerRpcException rpc
            ? $"RPC code {rpc.Code}"
            : exception.GetType().Name, log);
    }

    [Fact]
    public async Task UnsupportedUsageMethodWithoutCachedSnapshotReturnsUnavailable()
    {
        var result = await AccountUsageRefresher.TryReadAsync(
            _ => throw new AppServerRpcException(-32601, PrivateMessage),
            new RedactedLogger(_directory), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LifetimeCancellationBeforeReadIsSilentAndPreservesSnapshot()
    {
        using var lifetime = new CancellationTokenSource();
        lifetime.Cancel();
        var result = await AccountUsageRefresher.TryReadAsync(
            _ => throw new InvalidOperationException("The read must not run after cancellation."),
            new RedactedLogger(_directory), lifetime.Token) ?? Previous;

        Assert.Same(Previous, result);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task LifetimeCancellationDuringReadIsSilentAndPreservesSnapshot()
    {
        using var lifetime = new CancellationTokenSource();
        var result = await AccountUsageRefresher.TryReadAsync(
            token =>
            {
                Assert.Equal(lifetime.Token, token);
                lifetime.Cancel();
                return Task.FromCanceled<AccountUsageSnapshot>(token);
            },
            new RedactedLogger(_directory), lifetime.Token) ?? Previous;

        Assert.Same(Previous, result);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task DiagnosticWriteFailureDoesNotEscapeHandledRefreshFailure()
    {
        var logger = new RedactedLogger(_directory);
        Directory.Delete(_directory);
        var result = await AccountUsageRefresher.TryReadAsync(
            _ => throw new TimeoutException(PrivateMessage),
            logger, CancellationToken.None) ?? Previous;

        Assert.Same(Previous, result);
    }

    [Fact]
    public async Task FailedReadDoesNotRollBackSnapshotUpdatedWhileRequestWasPending()
    {
        var completion = new TaskCompletionSource<AccountUsageSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var current = Previous;
        var latest = Previous with { LifetimeTokens = 200 };

        async Task RefreshAsync()
        {
            current = await AccountUsageRefresher.TryReadAsync(
                _ => completion.Task, new RedactedLogger(_directory), CancellationToken.None) ?? current;
        }

        var refresh = RefreshAsync();
        current = latest;
        completion.SetException(new TimeoutException(PrivateMessage));
        await refresh;

        Assert.Same(latest, current);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
