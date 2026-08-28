using System.Text;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class CodexSessionScannerTests
{
    [Fact]
    public async Task DisabledScannerDoesNotInspectConfiguredRoot()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var scanner = CreateScanner(root);
            var result = await scanner.ScanAsync(
                new UsageAnalyticsSettings
                {
                    Enabled = false,
                    CodexHomeOverride = Path.Combine(root, "missing"),
                },
                null,
                CancellationToken.None);

            Assert.Empty(result.Rows);
            Assert.Equal(0, result.ScannedFileCount);
            Assert.False(File.Exists(Path.Combine(root, "scan-index.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScannerHonorsPreCanceledRequests()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var scanner = CreateScanner(root);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => scanner.ScanAsync(
                    new UsageAnalyticsSettings
                    {
                        Enabled = true,
                        CodexHomeOverride = root,
                    },
                    null,
                    cancellation.Token));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScannerSkipsContentAndMalformedRowsAndResumesAppendOnlyFiles()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var codexHome = Path.Combine(root, "codex");
            var sessionDirectory = Path.Combine(codexHome, "sessions", "2026", "07");
            Directory.CreateDirectory(sessionDirectory);
            var sessionPath = Path.Combine(sessionDirectory, "session.jsonl");
            await File.WriteAllLinesAsync(
                sessionPath,
                FirstTurnRows(),
                new UTF8Encoding(false));

            var scanner = CreateScanner(root);
            var settings = EnabledSettings(codexHome);
            var first = await scanner.ScanAsync(
                settings,
                null,
                CancellationToken.None);

            var firstRow = Assert.Single(first.Rows);
            Assert.Equal("gpt-5.6-sol", firstRow.Key.Model);
            Assert.Equal("high", firstRow.Key.ReasoningEffort);
            Assert.Equal(300, firstRow.Tokens.TotalTokens);
            Assert.Equal(1, firstRow.TurnCount);
            Assert.Equal(12_000, firstRow.ExactElapsedMilliseconds);
            Assert.Equal(1, first.ScannedFileCount);
            Assert.Equal(0, first.ErrorFileCount);

            var unchanged = await scanner.ScanAsync(
                settings,
                null,
                CancellationToken.None);
            Assert.Equal(0, unchanged.ScannedFileCount);
            Assert.Equal(1, unchanged.SkippedFileCount);
            Assert.Equal(300, Assert.Single(unchanged.Rows).Tokens.TotalTokens);

            await File.AppendAllLinesAsync(
                sessionPath,
                SecondTurnRows(),
                new UTF8Encoding(false));
            var appended = await scanner.ScanAsync(
                settings,
                null,
                CancellationToken.None);

            var total = appended.Rows.Sum(row => row.Tokens.TotalTokens);
            Assert.Equal(350, total);
            Assert.Equal(2, appended.Rows.Sum(row => row.TurnCount));
            Assert.Equal(1, appended.ScannedFileCount);
            Assert.Equal(0, appended.ErrorFileCount);

            var indexText = await File.ReadAllTextAsync(
                Path.Combine(root, "scan-index.json"));
            Assert.DoesNotContain(sessionPath, indexText, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "private user message",
                indexText,
                StringComparison.Ordinal);
            Assert.Contains("pathSha256", indexText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScannerRebuildsContributionWhenAFileIsTruncated()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var codexHome = Path.Combine(root, "codex");
            var sessionDirectory = Path.Combine(codexHome, "sessions");
            Directory.CreateDirectory(sessionDirectory);
            var sessionPath = Path.Combine(sessionDirectory, "session.jsonl");
            await File.WriteAllLinesAsync(
                sessionPath,
                FirstTurnRows(),
                new UTF8Encoding(false));
            var scanner = CreateScanner(root);
            var settings = EnabledSettings(codexHome);
            await scanner.ScanAsync(settings, null, CancellationToken.None);

            await File.WriteAllLinesAsync(
                sessionPath,
                SecondTurnRows(),
                new UTF8Encoding(false));
            var rebuilt = await scanner.ScanAsync(
                settings,
                null,
                CancellationToken.None);

            var row = Assert.Single(rebuilt.Rows);
            Assert.Equal(50, row.Tokens.TotalTokens);
            Assert.Equal(1, row.TurnCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CollectionSwitchesChangeStoredAggregatesAndInvalidateCache()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var codexHome = Path.Combine(root, "codex");
            var sessionDirectory = Path.Combine(codexHome, "sessions");
            Directory.CreateDirectory(sessionDirectory);
            await File.WriteAllLinesAsync(
                Path.Combine(sessionDirectory, "session.jsonl"),
                FirstTurnRows(),
                new UTF8Encoding(false));
            var scanner = CreateScanner(root);
            var settings = EnabledSettings(codexHome);
            await scanner.ScanAsync(settings, null, CancellationToken.None);

            settings.CollectModel = false;
            settings.CollectReasoningEffort = false;
            settings.CollectServiceTier = false;
            settings.CollectTokens = false;
            settings.CollectElapsedTime = false;
            settings.CollectTurnCount = false;
            var restricted = await scanner.ScanAsync(
                settings,
                null,
                CancellationToken.None);

            var row = Assert.Single(restricted.Rows);
            Assert.Equal("all-models", row.Key.Model);
            Assert.Equal("unknown", row.Key.ReasoningEffort);
            Assert.Equal("unknown", row.Key.ServiceTier);
            Assert.Equal(0, row.Tokens.EffectiveTotalTokens);
            Assert.Equal(0, row.TurnCount);
            Assert.Equal(0, row.ExactElapsedMilliseconds);
            Assert.Equal(1, restricted.ScannedFileCount);
            Assert.Equal(0, restricted.SkippedFileCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OptionalActivityCollectionStoresOnlySanitizedCounts()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var codexHome = Path.Combine(root, "codex");
            var sessionDirectory = Path.Combine(codexHome, "sessions");
            Directory.CreateDirectory(sessionDirectory);
            await File.WriteAllLinesAsync(
                Path.Combine(sessionDirectory, "session.jsonl"),
                [
                    """{"timestamp":"2026-07-26T01:00:00Z","type":"response_item","payload":{"type":"custom_tool_call","name":"exec","input":"private command"}}""",
                    """{"timestamp":"2026-07-26T01:00:01Z","type":"event_msg","payload":{"type":"item_completed","item":{"type":"Extension","kind":"web.search","query":"private query"}}}""",
                    """{"timestamp":"2026-07-26T01:00:02Z","type":"event_msg","payload":{"type":"item_completed","item":{"type":"Extension","kind":"image_gen.generation","revisedPrompt":"private prompt"}}}""",
                ],
                new UTF8Encoding(false));
            var settings = EnabledSettings(codexHome);
            settings.CollectToolUsage = true;
            settings.CollectSkillUsage = true;

            var result = await CreateScanner(root).ScanAsync(
                settings,
                null,
                CancellationToken.None);

            Assert.Contains(
                result.Activities!,
                row => row.Kind == LocalActivityKind.Tool &&
                       row.Name == "Computer Use" && row.Count == 1);
            Assert.Contains(
                result.Activities!,
                row => row.Kind == LocalActivityKind.Tool &&
                       row.Name == "Browser" && row.Count == 1);
            Assert.Contains(
                result.Activities!,
                row => row.Kind == LocalActivityKind.Skill &&
                       row.Name == "Imagegen" && row.Count == 1);
            var index = await File.ReadAllTextAsync(
                Path.Combine(root, "scan-index.json"));
            Assert.DoesNotContain("private command", index, StringComparison.Ordinal);
            Assert.DoesNotContain("private query", index, StringComparison.Ordinal);
            Assert.DoesNotContain("private prompt", index, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CodexSessionScanner CreateScanner(string root) =>
        new(
            Path.Combine(root, "scan-index.json"),
            new UsageAggregateStore(Path.Combine(root, "usage")));

    private static UsageAnalyticsSettings EnabledSettings(string codexHome) =>
        new()
        {
            Enabled = true,
            CodexHomeOverride = codexHome,
            IncludeArchivedSessions = true,
        };

    private static string[] FirstTurnRows() =>
    [
        """{"timestamp":"2026-07-26T01:00:00Z","type":"event_msg","payload":{"type":"task_started","started_at":"2026-07-26T01:00:00Z"}}""",
        """{"timestamp":"2026-07-26T01:00:01Z","type":"turn_context","payload":{"model":"gpt-5.6-sol","effort":"high","cwd":"D:\\private"}}""",
        """{"type":"turn_context","payload":null}""",
        """{"type":"event_msg","payload":null,"note":"token_count"}""",
        """{"type":"event_msg","payload":{"type":"token_count","info":null}}""",
        """["turn_context"]""",
        """{"timestamp":"2026-07-26T01:00:02Z","type":"response_item","payload":{"type":"message","text":"private user message"}}""",
        """{"timestamp":"2026-07-26T01:00:03Z","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":100,"cached_input_tokens":20,"output_tokens":50,"reasoning_output_tokens":10,"total_tokens":150},"total_token_usage":{"input_tokens":100,"cached_input_tokens":20,"output_tokens":50,"reasoning_output_tokens":10,"total_tokens":150}}}}""",
        """{"timestamp":"2026-07-26T01:00:04Z","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":120,"cached_input_tokens":30,"output_tokens":30,"reasoning_output_tokens":5,"total_tokens":150},"total_token_usage":{"input_tokens":220,"cached_input_tokens":50,"output_tokens":80,"reasoning_output_tokens":15,"total_tokens":300}}}}""",
        """{"type":"event_msg","payload":{"type":"token_count","info":""",
        """{"timestamp":"2026-07-26T01:00:12Z","type":"event_msg","payload":{"type":"task_complete","completed_at":"2026-07-26T01:00:12Z","duration_ms":12000}}""",
    ];

    private static string[] SecondTurnRows() =>
    [
        """{"timestamp":"2026-07-26T02:00:00Z","type":"event_msg","payload":{"type":"task_started","started_at":"2026-07-26T02:00:00Z"}}""",
        """{"timestamp":"2026-07-26T02:00:01Z","type":"turn_context","payload":{"model":"gpt-5.6-terra","effort":"medium"}}""",
        """{"timestamp":"2026-07-26T02:00:02Z","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":40,"cached_input_tokens":5,"output_tokens":10,"reasoning_output_tokens":2,"total_tokens":50},"total_token_usage":{"input_tokens":260,"cached_input_tokens":55,"output_tokens":90,"reasoning_output_tokens":17,"total_tokens":350}}}}""",
        """{"timestamp":"2026-07-26T02:00:05Z","type":"event_msg","payload":{"type":"task_complete","completed_at":"2026-07-26T02:00:05Z","duration_ms":5000}}""",
    ];

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"quantatray-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
