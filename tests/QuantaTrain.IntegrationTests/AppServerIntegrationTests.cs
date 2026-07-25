using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class AppServerIntegrationTests
{
    [Fact]
    public async Task InitializesAndReadsWeeklyQuotaFromFakeProcess()
    {
        var fakeAssembly = Path.Combine(
            AppContext.BaseDirectory,
            "QuantaTrain.FakeAppServer.dll");
        Assert.True(File.Exists(fakeAssembly), fakeAssembly);
        var dotnet = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe");

        await using var connection = await JsonRpcConnection.StartAsync(
            dotnet,
            [fakeAssembly],
            "0.1.0",
            CancellationToken.None);
        var client = new CodexAccountClient(connection, "fake-1.0");

        var account = await client.ReadAccountAsync(CancellationToken.None);
        var snapshot = await client.ReadRateLimitsAsync(CancellationToken.None);
        var weekly = WeeklyBucketSelector.BuildState(snapshot);

        Assert.True(account.IsSignedIn);
        Assert.NotNull(weekly);
        Assert.Equal(94.4, weekly.RemainingPercent, precision: 10);
        Assert.Equal(2, weekly.ResetCreditCount);
        Assert.Equal("plus", weekly.PlanType);
    }
}
