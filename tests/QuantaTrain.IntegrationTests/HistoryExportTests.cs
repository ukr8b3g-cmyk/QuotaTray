using System.Text.Json;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.IntegrationTests;

public sealed class HistoryExportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"quantatray-export-{Guid.NewGuid():N}");
    private string History => Path.Combine(_root, "history");
    public HistoryExportTests() => Directory.CreateDirectory(History);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WindowsExportClosesHandlesBeforeNewOrReplacementSave(bool withRows, bool overwrite)
    {
        var destination = Path.Combine(_root, "export.json");
        if (overwrite) await File.WriteAllTextAsync(destination, "old");
        if (withRows)
        {
            await File.WriteAllLinesAsync(Path.Combine(History, "2026-09.jsonl"),
                ["{\"value\":1}", "broken", ""]);
            await File.WriteAllLinesAsync(Path.Combine(History, "2026-10.jsonl"),
                ["{\"value\":2}"]);
        }
        await HistoryExportService.ExportJsonAsync(History, destination, CancellationToken.None);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(destination));
        Assert.Equal(withRows ? 2 : 0, json.RootElement.GetArrayLength());
        if (withRows)
            Assert.Equal([1, 2], json.RootElement.EnumerateArray().Select(row => row.GetProperty("value").GetInt32()));
        using var exclusive = File.Open(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public async Task FailedReplacementPreservesDestinationAndRemovesTemporary()
    {
        var destination = Path.Combine(_root, "export.json");
        await File.WriteAllTextAsync(destination, "old");
        using (var locked = File.Open(destination, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = await Record.ExceptionAsync(() =>
                HistoryExportService.ExportJsonAsync(History, destination, CancellationToken.None));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("old", await File.ReadAllTextAsync(destination));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public async Task CancellationAndMissingDestinationDoNotLeaveTemporaryFiles()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HistoryExportService.ExportJsonAsync(History, Path.Combine(_root, "export.json"), cancellation.Token));
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            HistoryExportService.ExportJsonAsync(History, Path.Combine(_root, "missing", "export.json"), CancellationToken.None));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        Assert.False(File.Exists(Path.Combine(_root, "export.json")));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
