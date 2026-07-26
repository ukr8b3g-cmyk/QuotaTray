using System.Text.Json;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class RetentionMaintenanceTests
{
    [Fact]
    public async Task RetainsBoundaryRowsAndDeletesOnlyExpiredRows()
    {
        var root = CreateTemporaryDirectory();
        var history = Path.Combine(root, "history");
        var usage = Path.Combine(root, "usage");
        var logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(history);
        Directory.CreateDirectory(usage);
        Directory.CreateDirectory(logs);
        try
        {
            var now = DateTimeOffset.Parse("2026-07-26T12:00:00Z");
            var keep = now.AddDays(-1094);
            var remove = now.AddDays(-1096);
            var historyPath = Path.Combine(history, "2023-07.jsonl");
            await File.WriteAllLinesAsync(
                historyPath,
                [
                    JsonSerializer.Serialize(new { observedToUtc = keep }),
                    JsonSerializer.Serialize(new { observedToUtc = remove }),
                ]);
            var usageStore = new UsageAggregateStore(usage);
            await usageStore.ReplaceAllAsync(
                [
                    Row(DateOnly.FromDateTime(keep.UtcDateTime)),
                    Row(DateOnly.FromDateTime(remove.UtcDateTime)),
                ],
                CancellationToken.None);

            var maintenance = new RetentionMaintenance(history, usage, logs);
            var ran = await maintenance.RunIfDueAsync(
                1095,
                14,
                now,
                CancellationToken.None);
            var secondRun = await maintenance.RunIfDueAsync(
                1095,
                14,
                now.AddHours(2),
                CancellationToken.None);

            Assert.True(ran);
            Assert.False(secondRun);
            Assert.Single(await File.ReadAllLinesAsync(historyPath));
            var retainedUsage = await usageStore.ReadAsync(
                now.AddYears(-4),
                now,
                CancellationToken.None);
            Assert.Single(retainedUsage);
            Assert.Equal(
                DateOnly.FromDateTime(keep.UtcDateTime),
                retainedUsage[0].Key.LocalDate);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static UsageAggregate Row(DateOnly date) =>
        new(
            new UsageAggregateKey(date, "model", "medium", "standard"),
            new UsageTokenTotals(1, 0, 0, 1, 0, 2),
            1,
            0,
            1,
            0,
            1,
            0);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"quantatray-retention-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
