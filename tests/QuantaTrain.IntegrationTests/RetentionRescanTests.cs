using System.Text;
using System.Text.Json;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class RetentionRescanTests : IDisposable
{
    [Theory]
    [InlineData(false, 1095)]
    [InlineData(true, 1095)]
    [InlineData(false, null)]
    [InlineData(true, null)]
    public async Task ExpandedRetentionAutomaticallyReparsesTruncatedCache(bool restart, int? days)
    {
        var paths = await WriteSessions(500, 0);
        var original = paths.ToDictionary(path => path, File.ReadAllBytes);
        var store = new UsageAggregateStore(Usage);
        var scanner = new CodexSessionScanner(Index, store, _clock);
        await scanner.ScanAsync(Settings, null, CancellationToken.None);
        await Maintain(scanner, 365);
        Assert.Single((await scanner.ScanAsync(Settings, null, CancellationToken.None)).Rows);
        if (restart) scanner = new CodexSessionScanner(Index, store, _clock);
        await Maintain(scanner, days);
        var widened = await scanner.ScanAsync(Settings, null, CancellationToken.None);
        Assert.Equal(2, widened.Rows.Count);
        Assert.Equal(2, widened.Activities!.Count);
        Assert.Equal(0, widened.ErrorFileCount);
        var unchanged = await scanner.ScanAsync(Settings, null, CancellationToken.None);
        Assert.Equal(2, unchanged.SkippedFileCount);
        await Maintain(scanner, 365);
        Assert.Single((await scanner.ScanAsync(Settings, null, CancellationToken.None)).Rows);
        foreach (var (path, bytes) in original) Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task ExpandedRetentionRetriesFailedReparseWithoutClaimingFullCoverage()
    {
        var paths = await WriteSessions(500, 0);
        var scanner = new CodexSessionScanner(Index, new UsageAggregateStore(Usage), _clock);
        await scanner.ScanAsync(Settings, null, CancellationToken.None);
        await Maintain(scanner, 365);
        await Maintain(scanner, 1095);
        using (var locked = File.Open(paths[0], FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var failed = await scanner.ScanAsync(Settings, null, CancellationToken.None);
            Assert.Equal(1, failed.ErrorFileCount);
            Assert.Single(failed.Rows);
        }
        var recovered = await scanner.ScanAsync(Settings, null, CancellationToken.None);
        Assert.Equal(2, recovered.Rows.Count);
        Assert.Equal(0, recovered.ErrorFileCount);
        Assert.Equal(1, recovered.ScannedFileCount);
        Assert.Equal(1, recovered.SkippedFileCount);
    }

    [Fact]
    public async Task LegacyCacheWithoutCoverageMetadataIsReparsedAfterRestart()
    {
        await WriteSessions(500, 0);
        var store = new UsageAggregateStore(Usage);
        var scanner = new CodexSessionScanner(Index, store, _clock);
        await scanner.ScanAsync(Settings, null, CancellationToken.None);
        await Maintain(scanner, 365);
        var document = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(Index))!;
        foreach (var entry in document["files"]!.AsArray())
        {
            entry!.AsObject().Remove("retentionCoverageKnown");
            entry.AsObject().Remove("retainedFromLocalDate");
        }
        await File.WriteAllTextAsync(Index, document.ToJsonString());
        scanner = new CodexSessionScanner(Index, store, _clock);
        await Maintain(scanner, 1095);
        Assert.Equal(2, (await scanner.ScanAsync(Settings, null, CancellationToken.None)).Rows.Count);
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"quantatray-retention-rescan-{Guid.NewGuid():N}");
    private readonly FixedClock _clock = new();
    private string Index => Path.Combine(_root, "cache", "index.json");
    private string Home => Path.Combine(_root, "codex");
    private string Usage => Path.Combine(_root, "usage");
    private UsageAnalyticsSettings Settings => new()
    {
        Enabled = true, CodexHomeOverride = Home, CollectToolUsage = true,
    };
    public RetentionRescanTests() => Directory.CreateDirectory(Path.Combine(Home, "sessions"));

    [Fact]
    public async Task MaintenanceThenCachedScanRestartAndCacheRebuildCannotRestoreExpiredRows()
    {
        var files = await WriteSessions();
        var original = files.ToDictionary(path => path, File.ReadAllBytes);
        var store = new UsageAggregateStore(Usage);
        var scanner = new CodexSessionScanner(Index, store, _clock);
        Assert.Equal(3, (await scanner.ScanAsync(Settings, null, CancellationToken.None)).Rows.Count);
        await Maintain(scanner, 7);
        await AssertRetained(scanner, expectedSkipped: 3);
        var restarted = new CodexSessionScanner(Index, store, _clock);
        await Maintain(restarted, 7);
        await AssertRetained(restarted, expectedSkipped: 3);
        await restarted.ResetCacheAsync(CancellationToken.None);
        await AssertRetained(restarted, expectedSkipped: 0);
        foreach (var (path, bytes) in original)
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task UnlimitedRetentionAndLocalMidnightBoundaryArePreserved()
    {
        await WriteSessions();
        var scanner = new CodexSessionScanner(Index, new UsageAggregateStore(Usage), _clock);
        await Maintain(scanner, null);
        Assert.Equal(3, (await scanner.ScanAsync(Settings, null, CancellationToken.None)).Rows.Count);
        await Maintain(scanner, 7);
        var result = await scanner.ScanAsync(Settings, null, CancellationToken.None);
        var cutoff = DateOnly.FromDateTime(_clock.GetUtcNow().LocalDateTime.Date.AddDays(-7));
        Assert.Contains(result.Rows, row => row.Key.LocalDate == cutoff);
        Assert.All(result.Rows, row => Assert.True(row.Key.LocalDate >= cutoff));
        // The cutoff must advance even if the 24-hour maintenance timer has not fired.
        _clock.Now = _clock.Now.AddDays(1);
        result = await scanner.ScanAsync(Settings, null, CancellationToken.None);
        Assert.Single(result.Rows);
        await Maintain(scanner, null);
        await scanner.ResetCacheAsync(CancellationToken.None);
        Assert.Equal(3, (await scanner.ScanAsync(Settings, null, CancellationToken.None)).Rows.Count);
    }

    [Fact]
    public async Task MaintenanceWaitsForActiveScanAndPrunesItsPublishedRows()
    {
        await WriteSessions();
        var scanner = new CodexSessionScanner(Index, new UsageAggregateStore(Usage), _clock);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var scan = Task.Run(() => scanner.ScanAsync(Settings,
            new InlineProgress(_ =>
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            }), CancellationToken.None));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        var maintenance = Maintain(scanner, 7);
        Assert.False(maintenance.IsCompleted);
        release.Set();
        await scan;
        await maintenance;
        await AssertRetained(scanner, expectedSkipped: 3);
    }

    private async Task Maintain(CodexSessionScanner scanner, int? days)
    {
        var maintenance = new RetentionMaintenance(
            Path.Combine(_root, "history"), Usage, Path.Combine(_root, "logs"), scanner);
        await maintenance.RunIfDueAsync(days, 14, _clock.GetUtcNow(), CancellationToken.None, force: true);
    }

    private async Task AssertRetained(CodexSessionScanner scanner, int expectedSkipped)
    {
        var result = await scanner.ScanAsync(Settings, null, CancellationToken.None);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(expectedSkipped, result.SkippedFileCount);
        Assert.Equal(2, result.Activities!.Count);
        var cutoff = DateOnly.FromDateTime(_clock.GetUtcNow().LocalDateTime.Date.AddDays(-7));
        Assert.All(result.Rows, row => Assert.True(row.Key.LocalDate >= cutoff));
        using var index = JsonDocument.Parse(await File.ReadAllTextAsync(Index));
        foreach (var entry in index.RootElement.GetProperty("files").EnumerateArray())
        {
            foreach (var row in entry.GetProperty("contributions").EnumerateArray())
                Assert.True(DateOnly.Parse(row.GetProperty("key").GetProperty("localDate").GetString()!) >= cutoff);
            foreach (var row in entry.GetProperty("activityContributions").EnumerateArray())
                Assert.True(DateOnly.Parse(row.GetProperty("localDate").GetString()!) >= cutoff);
        }
        var stored = await new UsageAggregateStore(Usage).ReadAsync(
            _clock.Now.AddYears(-1), _clock.Now, CancellationToken.None);
        Assert.Equal(2, stored.Count);
    }

    private async Task<string[]> WriteSessions(params int[] ages)
    {
        var paths = new List<string>();
        foreach (var age in ages.Length == 0 ? new[] { 40, 7, 0 } : ages)
        {
            var start = new DateTimeOffset(_clock.Now.LocalDateTime.Date.AddDays(-age).AddHours(12));
            var end = start.AddSeconds(2);
            var path = Path.Combine(Home, "sessions", $"session-{age}.jsonl");
            await File.WriteAllLinesAsync(path,
            [
                JsonSerializer.Serialize(new { timestamp = start, type = "event_msg", payload = new { type = "task_started", started_at = start } }),
                JsonSerializer.Serialize(new { timestamp = start, type = "turn_context", payload = new { model = "model", effort = "high" } }),
                JsonSerializer.Serialize(new { timestamp = start, type = "response_item", payload = new { type = "custom_tool_call", name = "exec" } }),
                JsonSerializer.Serialize(new { timestamp = end, type = "event_msg", payload = new { type = "token_count", info = new { last_token_usage = new { input_tokens = 1, output_tokens = 1, total_tokens = 2 } } } }),
                JsonSerializer.Serialize(new { timestamp = end, type = "event_msg", payload = new { type = "task_complete", completed_at = end, duration_ms = 2000 } }),
            ], new UTF8Encoding(false));
            paths.Add(path);
        }
        return paths.ToArray();
    }

    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.Parse("2026-10-24T14:59:59Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class InlineProgress(Action<SessionScanProgress> report) : IProgress<SessionScanProgress>
    {
        public void Report(SessionScanProgress value) => report(value);
    }
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
