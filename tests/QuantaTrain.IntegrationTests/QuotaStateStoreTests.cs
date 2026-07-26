using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class QuotaStateStoreTests
{
    [Fact]
    public async Task AtomicStateStoreRoundTripsWeeklyQuota()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "state.json");
            var store = new JsonQuotaStateStore(path);
            var state = new WeeklyQuotaState(
                "weekly",
                BucketRole.Secondary,
                58,
                42,
                10080,
                DateTimeOffset.Parse("2026-07-29T00:00:00Z"),
                3,
                [new ResetCredit(DateTimeOffset.Parse("2026-08-12T00:00:00Z"))],
                "pro",
                DateTimeOffset.Parse("2026-07-26T01:30:45Z"),
                "0.145.0");

            await store.WriteAsync(state, CancellationToken.None);
            var loaded = await store.ReadAsync(CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(2, loaded.SchemaVersion);
            Assert.Equal(state.LimitId, loaded.State.LimitId);
            Assert.Equal(state.Role, loaded.State.Role);
            Assert.Equal(state.UsedPercent, loaded.State.UsedPercent);
            Assert.Equal(state.RemainingPercent, loaded.State.RemainingPercent);
            Assert.Equal(
                state.WindowDurationMinutes,
                loaded.State.WindowDurationMinutes);
            Assert.Equal(state.ResetsAtUtc, loaded.State.ResetsAtUtc);
            Assert.Equal(state.ResetCreditCount, loaded.State.ResetCreditCount);
            Assert.Equal(state.ResetCredits, loaded.State.ResetCredits);
            Assert.Equal(state.PlanType, loaded.State.PlanType);
            Assert.Equal(state.ObservedAtUtc, loaded.State.ObservedAtUtc);
            Assert.Equal(state.CodexVersion, loaded.State.CodexVersion);
            Assert.Empty(Directory.EnumerateFiles(root, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"quantatray-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
