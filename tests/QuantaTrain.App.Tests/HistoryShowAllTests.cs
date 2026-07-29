using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Windows.Forms;
using QuantaTrain.App;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.App.Tests;

public sealed class HistoryShowAllTests
{
    [Fact]
    public async Task ReadAllIncludesEveryMonthAndSkipsDamagedRows()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"quantatray-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            WriteMonth(directory, "2026-04", Row("2026-04-10T01:00:00Z", "uncertain-change"));
            WriteMonth(directory, "2026-05", Row("2026-05-10T01:00:00Z", "limit-policy-change"));
            WriteMonth(
                directory,
                "2026-06",
                "{damaged" + Environment.NewLine +
                Row("2026-06-10T01:00:00Z", "unexpected-reset-candidate"));
            WriteMonth(directory, "2026-07", Row("2026-07-10T01:00:00Z", "scheduled-reset"));

            var store = new JsonlHistoryStore(directory);
            var all = await store.ReadAllAsync(CancellationToken.None);
            var recent = await store.ReadRecentAsync(2, CancellationToken.None);

            Assert.Equal(4, all.Count);
            Assert.Contains("scheduled-reset", all[0], StringComparison.Ordinal);
            Assert.Contains("unexpected-reset-candidate", all[1], StringComparison.Ordinal);
            Assert.Contains("limit-policy-change", all[2], StringComparison.Ordinal);
            Assert.Contains("uncertain-change", all[3], StringComparison.Ordinal);
            Assert.Equal(2, recent.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ShowAllButtonRaisesHistoryRequest()
    {
        RunSta(() =>
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                $"quantatray-locales-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(
                    Path.Combine(directory, "en-US.json"),
                    JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["History.ShowAll"] = "Show all",
                    }));
                var localizer = new LocalizationService(directory);
                localizer.Load(new LanguageSettings
                {
                    Mode = "manual",
                    Locale = "en-US",
                });
                Theme.Configure("dark", "green");
                using var form = new DetailForm(localizer);
                var requested = false;
                form.HistoryRequested += (_, _) => requested = true;
                form.Show();
                Application.DoEvents();

                Descendants(form)
                    .OfType<Button>()
                    .Single(button => button.Text == "Show all")
                    .PerformClick();

                Assert.True(requested);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    private static string Row(string observedToUtc, string classification) =>
        JsonSerializer.Serialize(new { observedToUtc, classification });

    private static void WriteMonth(string directory, string month, string content) =>
        File.WriteAllText(Path.Combine(directory, $"{month}.jsonl"), content + Environment.NewLine);

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
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
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
