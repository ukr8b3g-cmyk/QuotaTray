using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;
using ZstdSharp;

namespace QuantaTrain.IntegrationTests;

public sealed class CodexSessionScanResilienceTests
{
    private const int SmallLimit = 64 * 1024;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedFileDoesNotAbortHealthyFilesOrCachePersistence(bool oversizedFirst)
    {
        using var fixture = new Fixture();
        // Session roots have a defined order, independent of directory enumeration.
        await fixture.WriteCompressedAsync(oversizedFirst, Turn(900) + new string('x', SmallLimit));
        await fixture.WritePlainAsync(!oversizedFirst, Turn(100));

        var result = await fixture.ScanAsync(SmallLimit);

        AssertResult(result, tokens: 100, scanned: 1, skipped: 0, errors: 1);
        await fixture.AssertPersistedAsync(100, expectedFiles: 1);
        var repeated = await fixture.ScanAsync(SmallLimit);
        AssertResult(repeated, tokens: 100, scanned: 0, skipped: 1, errors: 1);
        await fixture.AssertPersistedAsync(100, expectedFiles: 1);
    }

    [Fact]
    public async Task OversizedChangedFileKeepsCompatiblePreviousContributionAndContinues()
    {
        using var fixture = new Fixture();
        await fixture.WriteCompressedAsync(true, Turn(100));
        AssertResult(await fixture.ScanAsync(SmallLimit), 100, 1, 0, 0);
        await fixture.WriteCompressedAsync(true, Turn(900) + new string('x', SmallLimit));
        await fixture.WritePlainAsync(false, Turn(200));

        var result = await fixture.ScanAsync(SmallLimit);

        AssertResult(result, 300, 1, 0, 1);
        await fixture.AssertPersistedAsync(300, expectedFiles: 2);
        // The failed file is retried, not falsely checkpointed as current.
        AssertResult(await fixture.ScanAsync(SmallLimit), 300, 0, 1, 1);
        await fixture.WriteCompressedAsync(true, Turn(150));
        AssertResult(await fixture.ScanAsync(SmallLimit), 350, 1, 1, 0);
    }

    [Fact]
    public async Task IncompatibleOldCacheIsNotRestoredForAnExcludedFile()
    {
        using var fixture = new Fixture();
        await fixture.WriteCompressedAsync(true, Turn(100));
        await fixture.ScanAsync(SmallLimit);
        var index = JsonNode.Parse(await File.ReadAllTextAsync(fixture.IndexPath))!;
        index["parserVersion"] = 2255;
        index["files"]![0]!["contributions"]![0]!["tokens"]!["totalTokens"] = 999999;
        await File.WriteAllTextAsync(fixture.IndexPath, index.ToJsonString());
        await fixture.WriteCompressedAsync(true, Turn(900) + new string('x', SmallLimit));
        await fixture.WritePlainAsync(false, Turn(200));

        AssertResult(await fixture.ScanAsync(SmallLimit), 200, 1, 0, 1);
        await fixture.AssertPersistedAsync(200, expectedFiles: 1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public async Task DecompressionBoundaryIsInclusiveAndFailedFileNeverAddsPartialRows(int extraBytes, int errors)
    {
        using var fixture = new Fixture();
        var turn = Turn(100);
        await fixture.WriteCompressedAsync(true, turn + new string('x', SmallLimit - Encoding.UTF8.GetByteCount(turn) + extraBytes));

        var result = await fixture.ScanAsync(SmallLimit);

        Assert.Equal(errors, result.ErrorFileCount);
        Assert.Equal(errors == 0 ? 100 : 0, result.Rows.Sum(row => row.Tokens.TotalTokens));
        await fixture.AssertPersistedAsync(errors == 0 ? 100 : 0, expectedFiles: errors == 0 ? 1 : 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CorruptOrTruncatedCompressedFileStillAllowsLaterHealthyFile(bool truncate)
    {
        using var fixture = new Fixture();
        using var compressor = new Compressor();
        var compressed = compressor.Wrap(Encoding.UTF8.GetBytes(Turn(900))).ToArray();
        await File.WriteAllBytesAsync(fixture.PathFor(true, compressed: true), truncate ? compressed[..^2] : [1, 2, 3, 4, 5]);
        await fixture.WritePlainAsync(false, Turn(100));

        AssertResult(await fixture.ScanAsync(SmallLimit), 100, 1, 0, 1);
        await fixture.AssertPersistedAsync(100, expectedFiles: 1);
    }

    [Fact]
    public async Task Production512MiBLimitIsContainedWithBoundedSyntheticFrames()
    {
        using var fixture = new Fixture();
        using var compressor = new Compressor();
        // A ~170 KiB compressed fixture expands to 512 MiB + 64 KiB. Never
        // allocate/store the expanded file; each decoder window is only 64 KiB.
        var block = Encoding.UTF8.GetBytes(new string('x', SmallLimit - 1) + "\n");
        var frame = compressor.Wrap(block).ToArray();
        await using (var file = File.Create(fixture.PathFor(true, compressed: true)))
        {
            for (var i = 0; i < 8193; i++)
            {
                await file.WriteAsync(frame);
            }
        }
        await fixture.WritePlainAsync(false, Turn(100));

        AssertResult(await fixture.ScanAsync(), 100, 1, 0, 1);
        await fixture.AssertPersistedAsync(100, expectedFiles: 1);
    }

    [Fact]
    public async Task CancellationDuringScanPropagatesAndLeavesPreviousCacheUsable()
    {
        using var fixture = new Fixture();
        await fixture.WriteCompressedAsync(true, Turn(100));
        await fixture.ScanAsync(SmallLimit);
        var previousIndex = await File.ReadAllTextAsync(fixture.IndexPath);
        await fixture.WriteCompressedAsync(true, Turn(900) + new string('x', SmallLimit));
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelProgress(cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.ScanAsync(SmallLimit, progress, cancellation.Token));

        Assert.Equal(previousIndex, await File.ReadAllTextAsync(fixture.IndexPath));
        await fixture.AssertPersistedAsync(100, expectedFiles: 1);
        AssertResult(await fixture.ScanAsync(SmallLimit), 100, 0, 0, 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(536870913)]
    public void TestLimitCannotDisableOrExpandProductionSafetyBound(long limit)
    {
        using var fixture = new Fixture();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CodexSessionScanner(fixture.IndexPath, fixture.Store, limit));
    }

    private static void AssertResult(SessionScanResult result, long tokens, int scanned, int skipped, int errors)
    {
        Assert.Equal(tokens, result.Rows.Sum(row => row.Tokens.TotalTokens));
        Assert.Equal(scanned, result.ScannedFileCount);
        Assert.Equal(skipped, result.SkippedFileCount);
        Assert.Equal(errors, result.ErrorFileCount);
    }

    private static string Turn(long tokens) => string.Join("\n", new[]
    {
        Row("turn_context", new { model = "synthetic", effort = "high" }),
        Row("event_msg", new { type = "task_started" }),
        Row("token_usage_record", new
        {
            usage = new { input_tokens = tokens, total_tokens = tokens },
            turn_token_usage = new { input_tokens = tokens, total_tokens = tokens },
            thread_token_usage = new { input_tokens = tokens, total_tokens = tokens },
        }),
        Row("event_msg", new { type = "task_complete", duration_ms = 10 }),
    }) + "\n";

    private static string Row(string type, object payload) => JsonSerializer.Serialize(new
    {
        timestamp = "2026-10-06T00:00:00Z", type, payload,
    });

    private sealed class CancelProgress(CancellationTokenSource source) : IProgress<SessionScanProgress>
    {
        public void Report(SessionScanProgress value) => source.Cancel();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"quantatray-resilience-{Guid.NewGuid():N}");
        public Fixture()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PathFor(true, false))!);
            Directory.CreateDirectory(Path.GetDirectoryName(PathFor(false, false))!);
            Store = new UsageAggregateStore(Path.Combine(_root, "usage"));
        }
        public string IndexPath => Path.Combine(_root, "scan-index.json");
        public UsageAggregateStore Store { get; }
        public string PathFor(bool firstRoot, bool compressed) => Path.Combine(_root, "codex",
            firstRoot ? "sessions" : "archived_sessions", compressed ? "rollout.jsonl.zst" : "rollout.jsonl");
        public Task WritePlainAsync(bool firstRoot, string content) =>
            File.WriteAllTextAsync(PathFor(firstRoot, false), content, new UTF8Encoding(false));
        public Task WriteCompressedAsync(bool firstRoot, string content)
        {
            using var compressor = new Compressor();
            return File.WriteAllBytesAsync(PathFor(firstRoot, true), compressor.Wrap(Encoding.UTF8.GetBytes(content)).ToArray());
        }
        public Task<SessionScanResult> ScanAsync(long? limit = null, IProgress<SessionScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var scanner = limit is { } bound ? new CodexSessionScanner(IndexPath, Store, bound) : new CodexSessionScanner(IndexPath, Store);
            return scanner.ScanAsync(new UsageAnalyticsSettings
            {
                Enabled = true,
                CodexHomeOverride = Path.Combine(_root, "codex"),
                IncludeArchivedSessions = true,
            }, progress, cancellationToken);
        }
        public async Task AssertPersistedAsync(long tokens, int expectedFiles)
        {
            Assert.True(File.Exists(IndexPath));
            using var index = JsonDocument.Parse(await File.ReadAllTextAsync(IndexPath));
            Assert.Equal(expectedFiles, index.RootElement.GetProperty("files").GetArrayLength());
            var saved = await Store.ReadAsync(DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
                DateTimeOffset.Parse("2026-11-01T00:00:00Z"), CancellationToken.None);
            Assert.Equal(tokens, saved.Sum(row => row.Tokens.TotalTokens));
        }
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
