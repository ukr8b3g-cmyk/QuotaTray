using QuantaTrain.Core;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuantaTrain.App.Tests;

public sealed class RuntimeAppearanceTests
{
    [Fact]
    public void ThemeCanSwitchBetweenDarkAndLightAtRuntime()
    {
        Theme.Configure("dark", "green");
        var darkWindow = Theme.Window;
        var darkText = Theme.Text;

        Theme.Configure("light", "green");

        Assert.NotEqual(darkWindow, Theme.Window);
        Assert.NotEqual(darkText, Theme.Text);
        Assert.True(Theme.Window.GetBrightness() > Theme.Text.GetBrightness());
    }

    [Fact]
    public void AccentCanSwitchAtRuntime()
    {
        Theme.Configure("dark", "green");
        var green = Theme.Accent;

        Theme.Configure("dark", "purple");

        Assert.NotEqual(green, Theme.Accent);
        Assert.Equal(Theme.Accent, Theme.QuotaColor(98.4));
        Assert.Equal(Theme.Yellow, Theme.QuotaColor(30));
        Assert.Equal(Theme.Red, Theme.QuotaColor(10));
    }

    [Fact]
    public void LocalizationCanReloadAtRuntime()
    {
        var localizer = new LocalizationService(FindLocalesDirectory());
        localizer.Load(new LanguageSettings { Mode = "manual", Locale = "en-US" });
        var english = localizer.Text("Common.Settings");

        localizer.Load(new LanguageSettings { Mode = "manual", Locale = "ja-JP" });

        Assert.Equal("ja-JP", localizer.CurrentLocale);
        Assert.NotEqual(english, localizer.Text("Common.Settings"));
    }

    [Fact]
    public void OpacitySliderUpdatesPanelsButKeepsSettingsOpaque()
    {
        RunSta(() =>
        {
            var settings = new AppSettings();
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(settings.Language);
            using var form = new SettingsForm(settings, localizer);
            SettingsPreviewKind? previewKind = null;
            form.SettingsPreviewChanged += (_, eventArgs) =>
                previewKind = eventArgs.Kind;
            var slider = (ValueSlider?)typeof(SettingsForm)
                .GetField(
                    "_opacity",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(form);

            Assert.NotNull(slider);
            slider.Value = 72;

            Assert.Equal(72, settings.Display.OpacityPercent);
            Assert.Equal(1d, form.Opacity, 2);
            Assert.Equal(SettingsPreviewKind.Opacity, previewKind);
        });
    }

    [Fact]
    public void ThemeAndLanguageSelectionsPreviewWithoutClosingSettings()
    {
        RunSta(() =>
        {
            var settings = new AppSettings();
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(settings.Language);
            var appearancePreviews = 0;

            using (var themeForm = new SettingsForm(settings, localizer))
            {
                themeForm.SettingsPreviewChanged += (_, eventArgs) =>
                {
                    if (eventArgs.Kind == SettingsPreviewKind.Appearance)
                    {
                        appearancePreviews++;
                    }
                };
                var theme = GetField<ComboBox>(themeForm, "_theme");
                theme.SelectedItem = "light";

                Assert.Equal("light", settings.Display.Theme);
                Assert.False(themeForm.IsDisposed);
            }

            using var languageForm = new SettingsForm(settings, localizer, 2);
            languageForm.SettingsPreviewChanged += (_, eventArgs) =>
            {
                if (eventArgs.Kind == SettingsPreviewKind.Appearance)
                {
                    appearancePreviews++;
                }
            };
            var locale = GetField<ComboBox>(languageForm, "_locale");
            locale.SelectedItem = "ja-JP";

            Assert.Equal("manual", settings.Language.Mode);
            Assert.Equal("ja-JP", settings.Language.Locale);
            Assert.False(languageForm.IsDisposed);
            Assert.Equal(2, appearancePreviews);
        });
    }

    [Fact]
    public void EveryLiveDisplaySettingKeepsDialogOpen()
    {
        RunSta(() =>
        {
            var settings = new AppSettings();
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(settings.Language);
            using var form = new SettingsForm(settings, localizer);
            var theme = GetField<ComboBox>(form, "_theme");
            var opacity = GetField<ValueSlider>(form, "_opacity");
            var alwaysOnTop = GetField<ToggleSwitch>(form, "_alwaysOnTop");
            var lockPosition = GetField<ToggleSwitch>(form, "_lockPosition");
            var rememberPosition = GetField<ToggleSwitch>(form, "_rememberPosition");
            var snapToEdge = GetField<ToggleSwitch>(form, "_snapToEdge");
            var miniClickThrough = GetField<ToggleSwitch>(form, "_miniClickThrough");
            var displayMode = GetField<ComboBox>(form, "_displayMode");
            var locale = GetField<ComboBox>(form, "_locale");
            var blueAccent = Descendants(form).OfType<Button>().Single(
                button => button.AccessibleName == "blue");
            Action[] changes =
            [
                () => theme.SelectedItem = "light",
                blueAccent.PerformClick,
                () => opacity.Value = 83,
                () => alwaysOnTop.Checked = true,
                () => lockPosition.Checked = true,
                () => rememberPosition.Checked = true,
                () => snapToEdge.Checked = true,
                () => miniClickThrough.Checked = true,
                () => displayMode.SelectedIndex = 0,
                () => locale.SelectedItem = "en-US",
            ];
            var changed = 0;
            var remainedOpen = true;
            using var timer = new System.Windows.Forms.Timer { Interval = 25 };
            timer.Tick += (_, _) =>
            {
                if (changed < changes.Length)
                {
                    changes[changed++]();
                    remainedOpen &= form.Visible && !form.IsDisposed;
                    return;
                }

                timer.Stop();
                form.Close();
            };
            form.Shown += (_, _) => timer.Start();

            form.ShowDialog();

            Assert.Equal(changes.Length, changed);
            Assert.True(remainedOpen);
        });
    }

    [Fact]
    public void EveryLanguageCanBeSelectedInsideDialogLoopWithoutClosingIt()
    {
        RunSta(() =>
        {
            var settings = new AppSettings
            {
                Language = new LanguageSettings
                {
                    Mode = "manual",
                    Locale = "en-US",
                },
            };
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(settings.Language);
            using var form = new SettingsForm(settings, localizer, 2);
            var locale = GetField<ComboBox>(form, "_locale");
            string[] choices =
            {
                "ja-JP", "en-US", "zh-Hans", "zh-Hant", "ko-KR",
                "de-DE", "fr-FR", "es-ES", "pt-BR", "ru-RU", "auto",
            };
            var selected = 0;
            using var timer = new System.Windows.Forms.Timer { Interval = 25 };
            timer.Tick += (_, _) =>
            {
                if (selected < choices.Length)
                {
                    locale.SelectedItem = choices[selected++];
                    return;
                }

                timer.Stop();
                form.Close();
            };
            form.Shown += (_, _) => timer.Start();

            form.ShowDialog();

            Assert.Equal(choices.Length, selected);
            Assert.Equal("auto", settings.Language.Mode);
        });
    }

    [Fact]
    public void DisplayStateOffersMiniCompactAndDetail()
    {
        RunSta(() =>
        {
            var settings = new AppSettings();
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(settings.Language);
            using var form = new SettingsForm(
                settings,
                localizer,
                initialDisplayMode: "mini");
            var displayMode = GetField<ComboBox>(form, "_displayMode");

            Assert.Equal(3, displayMode.Items.Count);
            Assert.Equal("mini", form.DisplayMode);
            displayMode.SelectedIndex = 1;
            Assert.Equal("compact", form.DisplayMode);
            displayMode.SelectedIndex = 2;
            Assert.Equal("detail", form.DisplayMode);
        });
    }

    [Fact]
    public void MiniClickThroughCanToggleWithoutRecreatingTheForm()
    {
        RunSta(() =>
        {
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings());
            using var form = new MiniForm(localizer);
            var handle = form.Handle;

            form.SetClickThrough(true);
            var enabledStyle = GetWindowLongPtr(handle, -20).ToInt64();
            form.SetClickThrough(false);
            var disabledStyle = GetWindowLongPtr(handle, -20).ToInt64();

            Assert.Equal(handle, form.Handle);
            Assert.Equal(new Size(232, 126), form.ClientSize);
            Assert.NotEqual(0, enabledStyle & 0x20L);
            Assert.Equal(0, disabledStyle & 0x20L);
        });
    }

    [Fact]
    public void ClickThroughMiniCanBeShownWithoutActivation()
    {
        RunSta(() =>
        {
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings());
            using var form = new MiniForm(localizer);
            form.SetClickThrough(true);

            form.Show();
            form.EnsureVisibleWithoutActivation(alwaysOnTop: false);
            Application.DoEvents();

            var style = GetWindowLongPtr(form.Handle, -20).ToInt64();
            Assert.True(form.Visible);
            Assert.NotEqual(0, style & 0x20L);
        });
    }

    [Fact]
    public void ClickThroughMiniSurvivesThreeAppearanceRebuilds()
    {
        RunSta(() =>
        {
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings());
            MiniForm? form = null;
            try
            {
                foreach (var accent in new[] { "blue", "purple", "green" })
                {
                    form?.Hide();
                    form?.Dispose();
                    Theme.Configure("dark", accent);
                    form = new MiniForm(localizer);
                    form.SetClickThrough(true);
                    form.Show();
                    form.EnsureVisibleWithoutActivation(alwaysOnTop: false);
                    Application.DoEvents();

                    Assert.True(form.Visible);
                    Assert.False(form.IsDisposed);
                }
            }
            finally
            {
                form?.Dispose();
            }
        });
    }

    [Fact]
    public void ClickThroughMiniRemainsVisibleAfterSettingsCloses()
    {
        RunSta(() =>
        {
            var appSettings = new AppSettings();
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(appSettings.Language);
            using var mini = new MiniForm(localizer);
            using var settings = new SettingsForm(appSettings, localizer);
            mini.SetClickThrough(true);
            mini.Show();
            mini.EnsureVisibleWithoutActivation(alwaysOnTop: false);
            settings.FormClosed += (_, _) =>
                mini.EnsureVisibleWithoutActivation(alwaysOnTop: false);
            settings.Show();

            settings.Close();
            Application.DoEvents();

            Assert.True(mini.Visible);
            Assert.False(mini.IsDisposed);
        });
    }

    [Fact]
    public void MiniViewDoubleClickRequestsCompactView()
    {
        RunSta(() =>
        {
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings());
            using var form = new MiniForm(localizer);
            var requested = false;
            form.CompactRequested += (_, _) => requested = true;
            var card = GetField<RoundedPanel>(form, "_quotaCard");
            var onDoubleClick = typeof(Control).GetMethod(
                "OnDoubleClick",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException("Control.OnDoubleClick");

            onDoubleClick.Invoke(card, [EventArgs.Empty]);

            Assert.True(requested);
        });
    }

    [Fact]
    public void MiniClickThroughDoesNotAffectCompactOrDetail()
    {
        RunSta(() =>
        {
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings());
            using var mini = new MiniForm(localizer);
            using var compact = new CompactForm(localizer);
            using var detail = new DetailForm(localizer);
            using var settings = new SettingsForm(
                new AppSettings(),
                localizer);
            mini.SetClickThrough(true);
            mini.Show();
            settings.Show();
            compact.Show();
            detail.Show();
            Application.DoEvents();

            var miniStyle = GetWindowLongPtr(mini.Handle, -20).ToInt64();
            var compactStyle = GetWindowLongPtr(compact.Handle, -20).ToInt64();
            var detailStyle = GetWindowLongPtr(detail.Handle, -20).ToInt64();

            Assert.NotEqual(0, miniStyle & 0x20L);
            Assert.Equal(0, compactStyle & 0x20L);
            Assert.Equal(0, detailStyle & 0x20L);
            Assert.True(compact.Enabled);
            Assert.True(detail.Enabled);
            Assert.True(settings.Visible);
        });
    }

    [Fact]
    public void EveryQuotaPanelRendersDecimalQuotaWithoutError()
    {
        RunSta(() =>
        {
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings
            {
                Mode = "manual",
                Locale = "ja-JP",
            });
            var state = new WeeklyQuotaState(
                "weekly",
                BucketRole.Primary,
                1.6,
                98.4,
                10080,
                DateTimeOffset.Now.AddDays(6),
                0,
                [],
                "test",
                DateTimeOffset.Now,
                "test");
            using var mini = new MiniForm(localizer);
            using var compact = new CompactForm(localizer);
            using var detail = new DetailForm(localizer);
            mini.UpdateState(state);
            compact.UpdateState(state, false, null);
            detail.UpdateState(state, false, null, []);

            foreach (var form in new Form[] { mini, compact, detail })
            {
                _ = form.Handle;
                using var bitmap = new Bitmap(
                    form.ClientSize.Width,
                    form.ClientSize.Height);
                form.DrawToBitmap(bitmap, form.ClientRectangle);
                Assert.Contains(
                    Descendants(form).OfType<Label>(),
                    label => label.Text == "98.4%");
            }
        });
    }

    [Theory]
    [InlineData(98.4, "98.4%")]
    [InlineData(98.0, "98%")]
    public void QuotaPercentUsesOneOptionalDecimal(double value, string expected)
    {
        Assert.Equal(expected, QuotaDisplay.Percent(value));
    }

    [Theory]
    [InlineData("ja-JP")]
    [InlineData("en-US")]
    [InlineData("zh-Hans")]
    [InlineData("zh-Hant")]
    [InlineData("ko-KR")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("es-ES")]
    [InlineData("pt-BR")]
    [InlineData("ru-RU")]
    public void QuotaHeaderDoesNotOverlapInAnyLanguage(string locale)
    {
        RunSta(() =>
        {
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings
            {
                Mode = "manual",
                Locale = locale,
            });
            var state = new WeeklyQuotaState(
                "weekly",
                BucketRole.Primary,
                5.6,
                94.4,
                10080,
                DateTimeOffset.Now.AddDays(6),
                0,
                [],
                "test",
                DateTimeOffset.Now,
                "test");
            using var compact = new CompactForm(localizer);
            using var mini = new MiniForm(localizer);
            compact.UpdateState(state, false, null);
            mini.UpdateState(state);

            AssertQuotaHeaderLayout(compact, localizer);
            AssertQuotaHeaderLayout(mini, localizer);
        });
    }

    [Fact]
    public void PanelPlacementClampsAndSnapsWithinWorkingArea()
    {
        var area = new Rectangle(100, 50, 1000, 700);
        var size = new Size(232, 126);

        Assert.Equal(
            new Point(100, 50),
            PanelPlacement.ClampToWorkingArea(area, size, new Point(-50, -50)));
        Assert.Equal(
            new Point(868, 624),
            PanelPlacement.SnapToWorkingArea(
                area,
                size,
                new Point(860, 620),
                16));
    }

    [Fact]
    public void PanelPlacementKeepsRightBottomAnchorAcrossDisplaySizes()
    {
        RunSta(() =>
        {
            var secondary = Screen.AllScreens.FirstOrDefault(screen => !screen.Primary);
            if (secondary is null)
            {
                return;
            }
            using var compact = new Form
            {
                StartPosition = FormStartPosition.Manual,
                Size = new Size(232, 126),
                Location = new Point(
                    secondary.WorkingArea.Right - 232,
                    secondary.WorkingArea.Bottom - 126),
            };
            using var detail = new Form
            {
                StartPosition = FormStartPosition.Manual,
                Size = new Size(420, 520),
            };
            var position = new PanelPositionSettings();

            PanelPlacement.Capture(compact, position);
            var restored = PanelPlacement.TryRestore(detail, position);

            Assert.True(restored);
            Assert.Equal("right", position.HorizontalAnchor);
            Assert.Equal("bottom", position.VerticalAnchor);
            Assert.Equal(secondary.DeviceName, Screen.FromRectangle(detail.Bounds).DeviceName);
            Assert.Equal(secondary.WorkingArea.Right, detail.Right);
            Assert.Equal(secondary.WorkingArea.Bottom, detail.Bottom);
        });
    }

    [Fact]
    public void MissingMonitorPositionFallsBackInsidePrimaryWorkingArea()
    {
        RunSta(() =>
        {
            var primary = Screen.PrimaryScreen;
            Assert.NotNull(primary);
            using var form = new Form
            {
                StartPosition = FormStartPosition.Manual,
                Size = new Size(420, 520),
            };
            var position = new PanelPositionSettings
            {
                MonitorDeviceName = @"\\.\DISPLAY-MISSING",
                X = 100_000,
                Y = 100_000,
            };

            Assert.True(PanelPlacement.TryRestore(form, position));
            Assert.True(primary.WorkingArea.Contains(form.Bounds));
        });
    }

    [Fact]
    public void SettingsOffersPositionResetAndStaysOnPrimaryMonitor()
    {
        RunSta(() =>
        {
            var settings = new AppSettings();
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings
            {
                Mode = "manual",
                Locale = "ja-JP",
            });
            using var form = new SettingsForm(settings, localizer);
            var requested = false;
            form.PositionResetRequested += (_, _) => requested = true;
            var reset = Descendants(form)
                .OfType<Button>()
                .Single(button => button.Text == "表示位置をリセット");
            var onClick = typeof(Button).GetMethod(
                "OnClick",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException("Button.OnClick");

            onClick.Invoke(reset, [EventArgs.Empty]);

            Assert.True(requested);
            var primary = Screen.PrimaryScreen;
            Assert.NotNull(primary);
            Assert.True(primary.WorkingArea.Contains(form.Bounds));
        });
    }

    [Fact]
    public void AboutDialogIncludesGitHubRepositoryLink()
    {
        RunSta(() =>
        {
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings
            {
                Mode = "manual",
                Locale = "en-US",
            });
            using var form = new AboutForm(localizer, "0.1.3");
            var repository = Descendants(form).OfType<LinkLabel>().Single();

            Assert.Contains("ukr8b3g-cmyk/QuotaTray", repository.Text);
            Assert.Equal(
                AboutForm.RepositoryUrl,
                repository.Links[0].LinkData);
        });
    }

    private static string FindLocalesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "QuantaTrain.App",
                "locales");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Source locales directory was not found.");
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
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(failure)
                .Throw();
        }
    }

    private static T GetField<T>(object target, string name)
        where T : class
    {
        return target.GetType()
            .GetField(
                name,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(target) as T
            ?? throw new InvalidOperationException(name);
    }

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

    private static void AssertQuotaHeaderLayout(
        Form form,
        LocalizationService localizer)
    {
        var card = GetField<RoundedPanel>(form, "_quotaCard");
        var weekly = card.Controls.OfType<Label>().Single(
            label => label.Text == localizer.Text("Quota.Weekly"));
        var prefix = GetField<Label>(form, "_remainingPrefix");
        var percent = GetField<Label>(form, "_remainingPercent");
        var progress = GetField<QuotaProgressBar>(form, "_progress");

        Assert.True(
            weekly.Bottom <= percent.Top,
            $"Weekly {weekly.Bounds} overlaps percent {percent.Bounds}.");
        Assert.True(prefix.Left >= 0, $"Prefix {prefix.Bounds} starts outside card {card.ClientRectangle}.");
        Assert.True(
            prefix.Right <= percent.Left,
            $"Prefix {prefix.Bounds} overlaps percent {percent.Bounds}.");
        Assert.True(
            percent.Right <= card.ClientSize.Width,
            $"Percent {percent.Bounds} exceeds card {card.ClientRectangle}.");
        Assert.True(
            percent.Bottom <= progress.Top,
            $"Percent {percent.Bounds} overlaps progress {progress.Bounds}.");
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);
}
