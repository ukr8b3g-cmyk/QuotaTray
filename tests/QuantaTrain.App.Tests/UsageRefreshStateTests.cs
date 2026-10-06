using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.App.Tests;

public sealed class UsageRefreshStateTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    public void EnabledNullSnapshotReplacesDisabledPlaceholderInEveryState(string locale)
    {
        RunSta(() =>
        {
            var localizer = Localizer(locale);
            using var form = new DetailForm(localizer);
            var settings = new UsageAnalyticsSettings { Enabled = true };
            foreach (var (scanning, failed, key) in new[]
            {
                (false, false, "Settings.UsageNotScanned"),
                (true, false, "Usage.Scanning"),
                (false, true, "Usage.ScanFailed"),
            })
            {
                form.UpdateUsage(null, settings, scanning, failed);

                Assert.Equal(localizer.Text(key), Field<Label>(form, "_usageStatus").Text);
                Assert.Contains(Labels(Field<Control>(form, "_modelRows")),
                    label => label.Text == localizer.Text(key));
                Assert.DoesNotContain(Labels(form),
                    label => label.Text == localizer.Text("Usage.EnableInSettings"));
                Assert.Equal("—", Assert.Single(Labels(Field<Control>(form, "_tokenDetails"))).Text);
            }
        });
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    public void AccountUsageRemainsVisibleWhenEnabledLocalScanHasNoSnapshot(string locale)
    {
        RunSta(() =>
        {
            var localizer = Localizer(locale);
            using var form = new DetailForm(localizer);
            var settings = new UsageAnalyticsSettings { Enabled = true, ShowAccountUsage = true };
            var account = new AccountUsageSnapshot(DateTimeOffset.UtcNow,
                563, 180, 92, 4, 7,
                [new AccountDailyUsage(new DateOnly(2026, 7, 26), 180)]);
            foreach (var scanning in new[] { true, false })
            {
                form.UpdateUsage(null, account, settings, scanning, refreshFailed: !scanning);
                Assert.Contains(Labels(Field<Control>(form, "_accountSummary")),
                    label => label.Text == "563");
                Assert.DoesNotContain(Labels(form),
                    label => label.Text == localizer.Text("Usage.EnableInSettings"));
                Assert.Equal(localizer.Text(scanning ? "Usage.Scanning" : "Usage.ScanFailed"),
                    Field<Label>(form, "_usageStatus").Text);
            }
        });
    }

    [Theory]
    [InlineData("en-US", true)]
    [InlineData("en-US", false)]
    [InlineData("ja-JP", true)]
    [InlineData("ja-JP", false)]
    public void ReopenedDashboardRendersPreviousSnapshotWhileRefreshingOrAfterFailure(
        string locale, bool scanning)
    {
        RunSta(() =>
        {
            var localizer = Localizer(locale);
            using var form = new DetailForm(localizer);
            var snapshot = Snapshot();
            var settings = new UsageAnalyticsSettings { Enabled = true };

            form.UpdateUsage(snapshot, settings, scanning, refreshFailed: !scanning);

            Assert.Contains(Labels(form), label => label.Text == "gpt-5.6-sol");
            Assert.Contains(Labels(Field<Control>(form, "_tokenDetails")),
                label => label.Text == "150");
            var status = Field<Label>(form, "_usageStatus");
            Assert.Contains(localizer.Text(scanning
                ? "Usage.ScanningPrevious" : "Usage.ScanFailedPrevious"), status.Text);
            Assert.Contains(snapshot.RefreshedAtUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss"),
                status.Text);
            Assert.DoesNotContain(Labels(form),
                label => label.Text == localizer.Text("Usage.EnableInSettings"));
            // The warning is above the account chart, without needing to scroll.
            Assert.True(status.Bottom <= Field<Control>(form, "_accountChart").Parent!.Top);
            form.UpdateUsageScanStatus(snapshot, enabled: true, scanning: false,
                refreshFailed: false, displayFailed: true);
            Assert.Equal(localizer.Text("Usage.DisplayFailed"), status.Text);
            Assert.Contains(Labels(form), label => label.Text == "gpt-5.6-sol");
        });
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    public void PartialResultsRemainExplicitEvenWhenNoRowsCouldBeRecovered(string locale)
    {
        RunSta(() =>
        {
            var localizer = Localizer(locale);
            using var form = new DetailForm(localizer);
            var settings = new UsageAnalyticsSettings { Enabled = true };
            var partial = Snapshot() with { ErrorFileCount = 2 };

            form.UpdateUsage(partial, settings, false);
            Assert.Contains(Labels(form), label => label.Text == "gpt-5.6-sol");
            Assert.Contains(localizer.Text("Usage.PartialResults", 2),
                Field<Label>(form, "_usageStatus").Text);
            Assert.Equal(Theme.Yellow, Field<Label>(form, "_usageStatus").ForeColor);

            foreach (var scanning in new[] { true, false })
            {
                form.UpdateUsage(partial, settings, scanning, refreshFailed: !scanning);
                form.Show();
                Field<Button>(form, "_usageTab").PerformClick();
                Application.DoEvents();
                var status = Field<Label>(form, "_usageStatus");
                Assert.True(status.Visible);
                Assert.Equal(3, status.Text.Split(Environment.NewLine).Length);
                var measured = TextRenderer.MeasureText(status.Text, status.Font,
                    new Size(status.Width, int.MaxValue), TextFormatFlags.WordBreak);
                Assert.True(measured.Height <= status.Height,
                    $"Usage banner text needs {measured.Height}px but has {status.Height}px.");
                Assert.True(status.Bottom <= Field<Control>(form, "_accountChart").Parent!.Top);
                Assert.True(status.Bottom <= status.Parent!.ClientSize.Height);
            }

            form.UpdateUsage(partial with { Rows = [] }, settings, false);
            Assert.Contains(Labels(form), label => label.Text == localizer.Text("Usage.PartialNoData"));
            Assert.DoesNotContain(Labels(form), label => label.Text == localizer.Text("Usage.NoData"));
            Assert.Equal("—", Assert.Single(Labels(Field<Control>(form, "_tokenDetails"))).Text);
            Assert.Contains(localizer.Text("Usage.PartialResults", 2),
                Field<Label>(form, "_usageStatus").Text);

            form.UpdateUsage(partial with { Rows = [], ErrorFileCount = 0 }, settings, false);
            Assert.Contains(Labels(form), label => label.Text == localizer.Text("Usage.NoData"));
            Assert.DoesNotContain(localizer.Text("Usage.PartialResults", 2),
                Field<Label>(form, "_usageStatus").Text);
            Assert.Equal(Theme.Muted, Field<Label>(form, "_usageStatus").ForeColor);
        });
    }

    [Fact]
    public void DisabledStateClearsPreviouslyDisplayedDataEvenDuringScan()
    {
        RunSta(() =>
        {
            var localizer = Localizer("en-US");
            using var form = new DetailForm(localizer);
            var settings = new UsageAnalyticsSettings { Enabled = true };
            form.UpdateUsage(Snapshot(), settings, false);
            settings.Enabled = false;

            form.UpdateUsage(Snapshot(), settings, true, refreshFailed: true);

            Assert.Equal(localizer.Text("Usage.Disabled"), Field<Label>(form, "_usageStatus").Text);
            Assert.Contains(Labels(form), label => label.Text == localizer.Text("Usage.EnableInSettings"));
            Assert.DoesNotContain(Labels(form), label => label.Text == "gpt-5.6-sol");
            Assert.Equal(0, Field<UsageDonutControl>(form, "_reasoningDonut").Total);
        });
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    public void SettingsOpenedMidRefreshUsesCurrentScanStateAndPreservesPartialWarning(string locale)
    {
        RunSta(() =>
        {
            var localizer = Localizer(locale);
            var settings = new AppSettings();
            settings.UsageAnalytics.Enabled = true;
            using var form = new SettingsForm(settings, localizer, initialPage: 5);
            var status = Field<Label>(form, "_usageScanStatus");

            form.UpdateUsageScanStatus(null, enabled: true, scanning: true);
            Assert.Equal(localizer.Text("Usage.Scanning"), status.Text);
            var snapshot = Snapshot() with { ErrorFileCount = 1 };
            form.UpdateUsageScanStatus(snapshot, enabled: true, scanning: true);
            Assert.Contains(localizer.Text("Usage.ScanningPrevious"), status.Text);
            Assert.Contains(localizer.Text("Usage.PartialResults", 1), status.Text);
            form.UpdateUsageScanStatus(snapshot, enabled: true, scanning: false, refreshFailed: true);
            Assert.Contains(localizer.Text("Usage.ScanFailedPrevious"), status.Text);
            Assert.Contains(localizer.Text("Usage.PartialResults", 1), status.Text);
            var hint = Labels(form).Single(label => label.Text == localizer.Text("Settings.UsagePrivacyReadOnly"));
            Assert.True(hint.Top > status.Bottom);
            Assert.True(status.Height > 24);

            form.UpdateUsageScanStatus(null, enabled: true, scanning: false, refreshFailed: true);
            Assert.Equal(localizer.Text("Usage.ScanFailed"), status.Text);
            form.UpdateUsageScanStatus(null, enabled: false, scanning: false);
            Assert.Equal(localizer.Text("Usage.Disabled"), status.Text);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRefreshRetainsSnapshotAndLogsOnlyStageAndExceptionType(bool hasPrevious)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"quantatray-ui-refresh-{Guid.NewGuid():N}");
        try
        {
            var logger = new RedactedLogger(directory);
            var previous = hasPrevious ? Snapshot() : null;
            const string privateMessage = @"C:\private\session.jsonl RAW_CONVERSATION_SENTINEL";
            Exception[] failures =
            [
                new InvalidDataException(privateMessage),
                new IOException(privateMessage),
                new UnauthorizedAccessException(privateMessage),
                new System.Text.Json.JsonException(privateMessage),
                new InvalidOperationException(privateMessage),
            ];
            foreach (var exception in failures)
            {
                var result = await QuantaTrainContext.RefreshUsageSnapshotAsync(
                    previous,
                    () => Task.FromException<UsageAnalysisSnapshot?>(exception),
                    logger,
                    CancellationToken.None,
                    () => "Local usage scan");
                Assert.True(result.Failed);
                Assert.Same(previous, result.Snapshot);
            }
            var log = await File.ReadAllTextAsync(Path.Combine(directory, "quantatray.log"));
            Assert.Contains("Local usage scan failed (InvalidDataException).", log);
            Assert.DoesNotContain(privateMessage, log);
            Assert.DoesNotContain("private", log);
            Assert.DoesNotContain("RAW_CONVERSATION_SENTINEL", log);

            var recovered = Snapshot() with { ScannedFileCount = 2 };
            var success = await QuantaTrainContext.RefreshUsageSnapshotAsync(
                previous, () => Task.FromResult<UsageAnalysisSnapshot?>(recovered), logger,
                CancellationToken.None);
            Assert.False(success.Failed);
            Assert.Same(recovered, success.Snapshot);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FinalRenderFailureIsContainedAndDiagnosticsExcludeContent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"quantatray-ui-render-{Guid.NewGuid():N}");
        try
        {
            var logger = new RedactedLogger(directory);
            var rendered = await QuantaTrainContext.RenderUsageViewsSafelyAsync(
                () => throw new OverflowException("PRIVATE_AGGREGATE_SENTINEL"), logger);
            Assert.False(rendered);
            var log = await File.ReadAllTextAsync(Path.Combine(directory, "quantatray.log"));
            Assert.Contains("Usage display refresh failed (OverflowException).", log);
            Assert.DoesNotContain("PRIVATE_AGGREGATE_SENTINEL", log);
            Assert.True(await QuantaTrainContext.RenderUsageViewsSafelyAsync(() => { }, logger));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CancellationDoesNotBecomeFailureOrReplacePreviousResults()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"quantatray-ui-refresh-{Guid.NewGuid():N}");
        try
        {
            var logger = new RedactedLogger(directory);
            using var cancellation = new CancellationTokenSource();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                QuantaTrainContext.RefreshUsageSnapshotAsync(
                    Snapshot(),
                    () =>
                    {
                        cancellation.Cancel();
                        return Task.FromResult<UsageAnalysisSnapshot?>(Snapshot() with { Rows = [] });
                    },
                    logger,
                    cancellation.Token));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static UsageAnalysisSnapshot Snapshot() => new(
        DateTimeOffset.Parse("2026-07-22T00:00:00Z"),
        DateTimeOffset.Parse("2026-07-29T00:00:00Z"),
        false,
        [new UsageAggregate(
            new UsageAggregateKey(new DateOnly(2026, 7, 26), "gpt-5.6-sol", "high", "fast"),
            new UsageTokenTotals(100, 20, 0, 50, 10, 150), 1, 12_000, 0, 1, 0, 0)],
        DateTimeOffset.Parse("2026-07-26T01:30:45Z"),
        1, 0, 0);

    private static LocalizationService Localizer(string locale)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "src", "QuantaTrain.App", "locales");
            if (Directory.Exists(path))
            {
                var localizer = new LocalizationService(path);
                localizer.Load(new LanguageSettings { Mode = "manual", Locale = locale });
                return localizer;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Source locales directory was not found.");
    }

    private static T Field<T>(object target, string name) where T : class =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static IEnumerable<Label> Labels(Control control)
    {
        foreach (Control child in control.Controls)
        {
            if (child is Label label)
            {
                yield return label;
            }
            foreach (var descendant in Labels(child))
            {
                yield return descendant;
            }
        }
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
