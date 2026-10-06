using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;
using ZstdSharp;

namespace QuantaTrain.IntegrationTests;

public sealed class CodexSessionCompatibilityTests
{
    [Fact]
    public async Task RepeatedSnapshotsAndContextEstimatesDoNotAddUsageOrPhantomTurns()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(
            Start, Context, Snapshot(300, 300), Snapshot(300, 300),
            Snapshot(2000, 300, estimate: true), Complete,
            Snapshot(300, 300), Start, Snapshot(50, 350), Complete);

        var result = await fixture.ScanAsync();

        var row = Assert.Single(result.Rows);
        Assert.Equal(350, row.Tokens.TotalTokens);
        Assert.Equal(2, row.TurnCount);
    }

    [Fact]
    public async Task NonAdjacentLegacyReplayDoesNotLowerTheCumulativeHighWatermark()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Start, Context,
            Snapshot(100, 100), Snapshot(200, 300),
            Snapshot(100, 100), Snapshot(200, 300), Complete);

        Assert.Equal(300, Assert.Single((await fixture.ScanAsync()).Rows).Tokens.TotalTokens);
    }

    [Fact]
    public async Task LegacyNullLastUsageFallsBackToCumulativeProgression()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Start, Context,
            Snapshot(100, 100, nullLast: true),
            Snapshot(100, 100, nullLast: true),
            Snapshot(200, 300, nullLast: true), Complete);

        Assert.Equal(300, Assert.Single((await fixture.ScanAsync()).Rows).Tokens.TotalTokens);
    }

    [Fact]
    public async Task AmbiguousLastOnlyAndTotalOnlyEstimatesAreNotCounted()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Start, Context,
            Event(new { type = "token_count", info = new { last_token_usage = Tokens(500) } }),
            Snapshot(2000, 2000, estimate: true), Complete);

        Assert.Equal(0, Assert.Single((await fixture.ScanAsync()).Rows).Tokens.TotalTokens);
    }

    [Fact]
    public async Task SyntheticTotalIncreaseWithStaleLastUsageIsNotAResponse()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Start, Context, Snapshot(300, 300),
            Event(new
            {
                type = "token_count",
                info = new
                {
                    last_token_usage = Tokens(300),
                    total_token_usage = new { input_tokens = 300, total_tokens = 2000 },
                },
            }), Complete);

        Assert.Equal(300, Assert.Single((await fixture.ScanAsync()).Rows).Tokens.TotalTokens);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task FullContextMarkersResetOnlyTheBaselineAndNeverCountAsUsage(long contextWindow)
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Start, Context, Snapshot(300, 300),
            Event(new
            {
                type = "token_count",
                info = new
                {
                    last_token_usage = Tokens(Math.Max(0, contextWindow - 300), estimate: true),
                    total_token_usage = Tokens(contextWindow, estimate: true),
                    model_context_window = contextWindow,
                },
            }),
            Event(new
            {
                type = "token_count",
                info = new
                {
                    last_token_usage = Tokens(50),
                    total_token_usage = new { input_tokens = 50, total_tokens = contextWindow + 50 },
                },
            }), Complete);

        Assert.Equal(350, Assert.Single((await fixture.ScanAsync()).Rows).Tokens.TotalTokens);
    }

    [Fact]
    public async Task ResponseRecordsReplaceLegacyFallbackAndDeduplicateReplayedRecords()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Start, Context, Snapshot(900, 900),
            Record("private-response-a", 100, 100, 100),
            Record("private-response-b", 200, 300, 300),
            Record("private-response-a", 100, 100, 100),
            Snapshot(3000, 3900), Complete,
            Record("private-response-b", 200, 300, 300),
            Start, Record("private-response-c", 50, 50, 350), Complete);

        var result = await fixture.ScanAsync();

        var row = Assert.Single(result.Rows);
        Assert.Equal(350, row.Tokens.TotalTokens);
        Assert.Equal(2, row.TurnCount);
        var index = await File.ReadAllTextAsync(fixture.IndexPath);
        Assert.DoesNotContain("private-response", index, StringComparison.Ordinal);
        Assert.DoesNotContain("private-thread", index, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResponseCumulativeRecoversMissingRowsWithoutImportingPriorTurns()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Start, Context, Record("a", 100, 100, 100),
            "{\"type\":\"token_usage_record\",\"payload\":", // Damaged middle response.
            Record("c", 50, 350, 350), Complete,
            Start, Record("e", 50, 50, 700), Complete); // A whole intervening turn is absent.

        Assert.Equal(400, Assert.Single((await fixture.ScanAsync()).Rows).Tokens.TotalTokens);
    }

    [Fact]
    public async Task ContinuedTurnAfterTaskStartedAddsOnlyNewObservedTokens()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Start, Context, Record("a", 100, 100, 100),
            Start, Record("b", 50, 150, 150), Complete);

        Assert.Equal(150, Assert.Single((await fixture.ScanAsync()).Rows).Tokens.TotalTokens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompactionCheckpointSeedsOrResetsObservedBaselineWithoutAddingUsage(bool hasRecord)
    {
        using var fixture = new Fixture();
        var checkpoint = hasRecord
            ? JsonSerializer.SerializeToElement(new { thread_token_usage = Tokens(300) })
            : (JsonElement?)null;
        await fixture.WriteAsync(Start, Context, Record("a", 300, 300, 300), Complete,
            Row("compacted", new { latest_token_usage_record = checkpoint, message = "private-compacted-text" }),
            Start, Record("b", 50, 50, hasRecord ? 350 : 50), Complete);

        Assert.Equal(350, Assert.Single((await fixture.ScanAsync()).Rows).Tokens.TotalTokens);
        Assert.DoesNotContain("private-compacted-text", await File.ReadAllTextAsync(fixture.IndexPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ObservedCumulativePreservesEachTokenComponent()
    {
        using var fixture = new Fixture();
        var first = new { input_tokens = 100, cached_input_tokens = 20, cache_write_input_tokens = 5,
            output_tokens = 50, reasoning_output_tokens = 10, total_tokens = 155 };
        var last = new { input_tokens = 60, cached_input_tokens = 10, cache_write_input_tokens = 0,
            output_tokens = 20, reasoning_output_tokens = 5, total_tokens = 80 };
        var total = new { input_tokens = 160, cached_input_tokens = 30, cache_write_input_tokens = 5,
            output_tokens = 70, reasoning_output_tokens = 15, total_tokens = 235 };
        await fixture.WriteAsync(Start, Context,
            Row("token_usage_record", new { usage = first, turn_token_usage = first, thread_token_usage = first }),
            Row("token_usage_record", new { usage = last, turn_token_usage = total, thread_token_usage = total }),
            Row("token_usage_record", new { usage = first, turn_token_usage = first, thread_token_usage = first }), Complete);

        Assert.Equal(new UsageTokenTotals(160, 30, 5, 70, 15, 235),
            Assert.Single((await fixture.ScanAsync()).Rows).Tokens);
    }

    [Fact]
    public async Task ResponsePreferenceAndDeduplicationSurviveScannerRestart()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Start, Context, Snapshot(700, 700), Record("a", 100, 100, 100));
        Assert.Empty((await fixture.ScanAsync()).Rows);
        await fixture.AppendAsync(Record("a", 100, 100, 100), Record("b", 200, 300, 300), Complete);

        var result = await fixture.ScanAsync();
        Assert.Equal(300, Assert.Single(result.Rows).Tokens.TotalTokens);
        Assert.Equal(1, result.ScannedFileCount);
    }

    [Fact]
    public async Task ThreadSettingsTierIsReadAndClearedWhenFullSnapshotOmitsIt()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Settings("priority"), Start, Snapshot(100, 100), Complete,
            Event(new { type = "thread_settings_applied", thread_settings = new { model = "synthetic", reasoning_effort = "low" } }),
            Start, Snapshot(50, 150), Complete);

        var result = await fixture.ScanAsync();

        Assert.Contains(result.Rows, row => row.Key.ServiceTier == "fast" && row.Tokens.TotalTokens == 100);
        Assert.Contains(result.Rows, row => row.Key.ServiceTier == "unknown" && row.Key.ReasoningEffort == "low" && row.Tokens.TotalTokens == 50);
    }

    [Fact]
    public async Task LegacyTurnContextTierRemainsSupported()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Start,
            Row("turn_context", new { model = "synthetic", service_tier = "default", effort = "high" }),
            Snapshot(100, 100), Complete);

        Assert.Equal("standard", Assert.Single((await fixture.ScanAsync()).Rows).Key.ServiceTier);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteLastRowIsRetriedIncludingSplitUtf8(bool splitUtf8)
    {
        using var fixture = new Fixture();
        var prefix = Encoding.UTF8.GetBytes(Start + "\n" + Context + "\n");
        var row = splitUtf8
            ? Event(new { type = "token_count", info = new { last_token_usage = Tokens(300), total_token_usage = Tokens(300) }, ignored = "é" })
            : Snapshot(300, 300);
        // Serialize with an actual multibyte character to split a UTF-8 sequence.
        row = row.Replace("\\u00E9", "é", StringComparison.OrdinalIgnoreCase);
        var bytes = Encoding.UTF8.GetBytes(row + "\n" + Complete + "\n");
        var cut = splitUtf8 ? Array.IndexOf(bytes, (byte)0xC3) + 1 : bytes.Length / 3;
        Assert.True(cut > 0);
        await File.WriteAllBytesAsync(fixture.SessionPath, [.. prefix, .. bytes[..cut]]);
        Assert.Empty((await fixture.ScanAsync()).Rows);
        await using (var stream = File.Open(fixture.SessionPath, FileMode.Append))
        {
            await stream.WriteAsync(bytes.AsMemory(cut));
        }

        var result = await fixture.ScanAsync();
        Assert.Equal(300, Assert.Single(result.Rows).Tokens.TotalTokens);
        Assert.Equal(1, Assert.Single(result.Rows).TurnCount);
        Assert.Equal(300, Assert.Single((await fixture.ScanAsync()).Rows).Tokens.TotalTokens);
    }

    [Fact]
    public async Task ShortFileCheckpointAndOversizedRowsRemainBoundedAndRecoverable()
    {
        using var fixture = new Fixture();
        await File.WriteAllTextAsync(fixture.SessionPath, "{\"type\":\"turn_context\",\"payload\":");
        Assert.Empty((await fixture.ScanAsync()).Rows);
        await File.AppendAllTextAsync(fixture.SessionPath, "{\"model\":\"synthetic\"}}\n");
        await fixture.AppendAsync(Start, new string('x', 4 * 1024 * 1024 + 1), Snapshot(100, 100), Complete);

        Assert.Equal(100, Assert.Single((await fixture.ScanAsync()).Rows).Tokens.TotalTokens);
    }

    [Fact]
    public async Task CompressedRolloutsAndPlainSiblingAreCountedOnceAcrossMigration()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Start, Context, Snapshot(300, 300), Complete);
        await fixture.CompressAsync();
        var both = await fixture.ScanAsync();
        Assert.Equal(1, both.ScannedFileCount);
        Assert.Equal(300, Assert.Single(both.Rows).Tokens.TotalTokens);

        File.Delete(fixture.SessionPath);
        var compressed = await fixture.ScanAsync();
        Assert.Equal(1, compressed.ScannedFileCount);
        Assert.Equal(300, Assert.Single(compressed.Rows).Tokens.TotalTokens);
        var unchanged = await fixture.ScanAsync();
        Assert.Equal(1, unchanged.SkippedFileCount);
        Assert.Equal(300, Assert.Single(unchanged.Rows).Tokens.TotalTokens);
    }

    [Fact]
    public async Task ConcatenatedZstdFramesAreStreamedAndCorruptionRetainsPreviousAggregate()
    {
        using var fixture = new Fixture();
        using var compressor = new Compressor();
        var first = compressor.Wrap(Encoding.UTF8.GetBytes(Start + "\n" + Context + "\n")).ToArray();
        var second = compressor.Wrap(Encoding.UTF8.GetBytes(Snapshot(300, 300) + "\n" + Complete + "\n")).ToArray();
        await File.WriteAllBytesAsync(fixture.SessionPath + ".zst", [.. first, .. second]);
        var initial = await fixture.ScanAsync();
        Assert.Equal(300, Assert.Single(initial.Rows).Tokens.TotalTokens);
        await File.WriteAllBytesAsync(fixture.SessionPath + ".zst", [.. first, .. second[..^2]]);

        var corrupt = await fixture.ScanAsync();
        Assert.Equal(1, corrupt.ErrorFileCount);
        Assert.Equal(300, Assert.Single(corrupt.Rows).Tokens.TotalTokens);
    }

    [Fact]
    public async Task ParserVersionInvalidatesOldInflatedCache()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(Start, Context, Snapshot(300, 300), Snapshot(300, 300), Complete);
        await fixture.ScanAsync();
        var document = JsonNode.Parse(await File.ReadAllTextAsync(fixture.IndexPath))!;
        document["parserVersion"] = 2255;
        document["files"]![0]!["contributions"]![0]!["tokens"]!["totalTokens"] = 600;
        await File.WriteAllTextAsync(fixture.IndexPath, document.ToJsonString());

        var result = await fixture.ScanAsync();
        Assert.Equal(1, result.ScannedFileCount);
        Assert.Equal(300, Assert.Single(result.Rows).Tokens.TotalTokens);
    }

    private static readonly string Start = Event(new { type = "task_started" });
    private static readonly string Complete = Event(new { type = "task_complete", duration_ms = 10 });
    private static readonly string Context = Row("turn_context", new { model = "synthetic", effort = "high" });
    private static string Settings(string? tier) => Event(new
    {
        type = "thread_settings_applied",
        thread_settings = new { model = "synthetic", service_tier = tier, reasoning_effort = "high" },
    });
    private static object Tokens(long total, bool estimate = false) => new
    {
        input_tokens = estimate ? 0 : total,
        output_tokens = 0,
        cached_input_tokens = 0,
        cache_write_input_tokens = 0,
        reasoning_output_tokens = 0,
        total_tokens = total,
    };
    private static string Snapshot(long last, long total, bool estimate = false, bool nullLast = false) => Event(new
    {
        type = "token_count",
        info = new { last_token_usage = nullLast ? null : Tokens(last, estimate), total_token_usage = Tokens(total), model_context_window = 128000 },
        rate_limits = (object?)null,
    });
    private static string Record(string response, long usage, long turn, long thread) => Row("token_usage_record", new
    {
        thread_id = "private-thread",
        session_id = "private-session",
        turn_id = "private-turn",
        root_turn_id = "private-root-turn",
        response_id = response,
        usage = Tokens(usage),
        turn_token_usage = Tokens(turn),
        thread_token_usage = Tokens(thread),
    });
    private static string Event(object payload) => Row("event_msg", payload);
    private static string Row(string type, object payload) => JsonSerializer.Serialize(new
    {
        timestamp = "2026-10-06T00:00:00Z", type, payload,
    });

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"quantatray-compat-{Guid.NewGuid():N}");
        public Fixture() => Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
        public string SessionPath => Path.Combine(_root, "codex", "sessions", "rollout.jsonl");
        public string IndexPath => Path.Combine(_root, "scan-index.json");
        public Task WriteAsync(params string[] rows) => File.WriteAllLinesAsync(SessionPath, rows, new UTF8Encoding(false));
        public Task AppendAsync(params string[] rows) => File.AppendAllLinesAsync(SessionPath, rows, new UTF8Encoding(false));
        public async Task CompressAsync()
        {
            using var compressor = new Compressor();
            var bytes = await File.ReadAllBytesAsync(SessionPath);
            await File.WriteAllBytesAsync(SessionPath + ".zst", compressor.Wrap(bytes).ToArray());
        }
        public Task<SessionScanResult> ScanAsync() => new CodexSessionScanner(IndexPath,
            new UsageAggregateStore(Path.Combine(_root, "usage"))).ScanAsync(new UsageAnalyticsSettings
            {
                Enabled = true,
                CodexHomeOverride = Path.Combine(_root, "codex"),
                IncludeArchivedSessions = true,
            }, null, CancellationToken.None);
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
