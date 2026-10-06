using System.Runtime.InteropServices;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class AccountUsageProcessTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "QuantaTray-usage-process-tests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("unsupported")]
    [InlineData("timeout")]
    public async Task FailedUsageRequestPreservesCacheAndDoesNotInterruptQuotaReads(string scenario)
    {
        var fakeAssembly = Path.Combine(AppContext.BaseDirectory, "QuantaTrain.FakeAppServer.dll");
        Assert.True(File.Exists(fakeAssembly), fakeAssembly);
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Path.GetFullPath(
            Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..",
                OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        await using var connection = await JsonRpcConnection.StartAsync(
            dotnet, [fakeAssembly, $"--usage={scenario}"], "test", CancellationToken.None);
        var client = new CodexAccountClient(connection, "fake");
        var previous = new AccountUsageSnapshot(
            DateTimeOffset.Parse("2026-10-06T00:00:00Z"), 100, 50, 10, 1, 2, []);

        var current = await AccountUsageRefresher.TryReadAsync(
            client.ReadUsageAsync, new RedactedLogger(_directory), CancellationToken.None) ?? previous;
        var quota = await client.ReadRateLimitsAsync(CancellationToken.None);

        Assert.Same(previous, current);
        Assert.NotNull(WeeklyBucketSelector.BuildState(quota));
        var log = await File.ReadAllTextAsync(Path.Combine(_directory, "quantatray.log"));
        Assert.DoesNotContain("synthetic-private-account-payload", log);
        Assert.Contains(scenario == "unsupported" ? "RPC code -32601" : "TimeoutException", log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
