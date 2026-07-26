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
        Assert.Equal(Theme.Orange, Theme.QuotaColor(30));
        Assert.Equal(Theme.Red, Theme.QuotaColor(10));
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("cyan")]
    public void BlueFamilyAccentsUseGreenForUsedQuota(string accent)
    {
        Theme.Configure("dark", accent);
        try
        {
            Assert.Equal(Theme.Green, Theme.UsedQuota);
            Assert.NotEqual(Theme.Accent, Theme.UsedQuota);
            Assert.Equal(Theme.Accent, Theme.QuotaColor(82));
            Assert.Equal(Theme.Orange, Theme.QuotaColor(30));
            Assert.Equal(Theme.Red, Theme.QuotaColor(10));
        }
        finally
        {
            Theme.Configure("dark", "green");
        }
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
            Assert.Equal(DrawMode.OwnerDrawFixed, locale.DrawMode);
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
            Assert.Equal("manual", settings.Language.Mode);
            Assert.Equal("en-US", settings.Language.Locale);
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
            Assert.Equal(new Size(220, 95), form.ClientSize);
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
        var size = new Size(220, 95);

        Assert.Equal(
            new Point(100, 50),
            PanelPlacement.ClampToWorkingArea(area, size, new Point(-50, -50)));
        Assert.Equal(
            new Point(880, 655),
            PanelPlacement.SnapToWorkingArea(
                area,
                size,
                new Point(888, 648),
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
                Size = new Size(220, 95),
                Location = new Point(
                    secondary.WorkingArea.Right - 220,
                    secondary.WorkingArea.Bottom - 95),
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
    public void DetailAndSettingsUseFixed800WidthAndNineSettingsPages()
    {
        RunSta(() =>
        {
            Theme.Configure("dark", "green");
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings());
            using var detail = new DetailForm(localizer);
            using var settings = new SettingsForm(new AppSettings(), localizer);
            detail.UpdateState(
                new WeeklyQuotaState(
                    "weekly",
                    BucketRole.Secondary,
                    58,
                    42,
                    10080,
                    DateTimeOffset.Now.AddDays(3),
                    3,
                    [
                        new ResetCredit(DateTimeOffset.Now.AddDays(17)),
                        new ResetCredit(DateTimeOffset.Now.AddDays(41)),
                        new ResetCredit(DateTimeOffset.Now.AddDays(76)),
                    ],
                    "ChatGPT Pro",
                    DateTimeOffset.Now,
                    "0.145.0"),
                false,
                null,
                ["2026/07/26 09:00  scheduled-reset"]);
            detail.Show();
            settings.Show();
            Application.DoEvents();

            Assert.Equal(800, detail.ClientSize.Width);
            Assert.Equal(800, settings.ClientSize.Width);
            Assert.Equal(650, settings.ClientSize.Height);
            Assert.Equal(520, detail.MinimumSize.Height);
            Assert.Equal(520, settings.MinimumSize.Height);
            Assert.Equal(AutoScaleMode.Dpi, detail.AutoScaleMode);
            Assert.Equal(AutoScaleMode.Dpi, settings.AutoScaleMode);
            var detailHeight = detail.Height;
            detail.Size = new Size(940, detailHeight + 40);
            settings.Size = new Size(940, settings.Height + 40);
            Assert.Equal(800, detail.Width);
            Assert.Equal(800, settings.Width);
            Assert.Equal(detailHeight + 40, detail.Height);
            Assert.Equal(
                9,
                Descendants(settings).OfType<NavButton>().Count());
            SaveSnapshotIfRequested(detail, "overview.png");
            SaveSnapshotIfRequested(settings, "settings.png");
        });
    }

    [Fact]
    public void SettingsOpensAtReadableHeightWithoutInitialPageScroll()
    {
        RunSta(() =>
        {
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings
            {
                Mode = "manual",
                Locale = "ja-JP",
            });
            using var usage = new SettingsForm(
                new AppSettings(),
                localizer,
                initialPage: 6);
            var generalSettings = new AppSettings();
            generalSettings.General.StartupMode = "compact";
            generalSettings.Language.Mode = "manual";
            generalSettings.Language.Locale = "ja-JP";
            using var general = new SettingsForm(
                generalSettings,
                localizer,
                initialPage: 0);
            using var notifications = new SettingsForm(
                new AppSettings(),
                localizer,
                initialPage: 2);
            using var acquisition = new SettingsForm(
                new AppSettings(),
                localizer,
                initialPage: 5);
            using var advanced = new SettingsForm(
                new AppSettings(),
                localizer,
                initialPage: 7);
            usage.Show();
            general.Show();
            notifications.Show();
            acquisition.Show();
            advanced.Show();
            Application.DoEvents();

            Assert.Equal(650, usage.ClientSize.Height);
            Assert.Equal(650, acquisition.ClientSize.Height);
            Assert.Equal(650, advanced.ClientSize.Height);
            var usagePage = GetField<List<Panel>>(usage, "_pages")
                .Single(page => page.Visible);
            var generalPage = GetField<List<Panel>>(general, "_pages")
                .Single(page => page.Visible);
            var notificationsPage =
                GetField<List<Panel>>(notifications, "_pages")
                    .Single(page => page.Visible);
            var acquisitionPage = GetField<List<Panel>>(acquisition, "_pages")
                .Single(page => page.Visible);
            var advancedPage = GetField<List<Panel>>(advanced, "_pages")
                .Single(page => page.Visible);
            Assert.False(
                usagePage.VerticalScroll.Visible,
                $"usage client={usagePage.ClientSize} display={usagePage.DisplayRectangle} maxBottom={usagePage.Controls.Cast<Control>().Max(control => control.Bottom)}");
            Assert.False(
                generalPage.VerticalScroll.Visible,
                $"general client={generalPage.ClientSize} display={generalPage.DisplayRectangle} maxBottom={generalPage.Controls.Cast<Control>().Max(control => control.Bottom)}");
            Assert.False(notificationsPage.VerticalScroll.Visible);
            Assert.False(
                acquisitionPage.VerticalScroll.Visible,
                $"acquisition client={acquisitionPage.ClientSize} display={acquisitionPage.DisplayRectangle} maxBottom={acquisitionPage.Controls.Cast<Control>().Max(control => control.Bottom)}");
            Assert.False(
                advancedPage.VerticalScroll.Visible,
                $"advanced client={advancedPage.ClientSize} display={advancedPage.DisplayRectangle} maxBottom={advancedPage.Controls.Cast<Control>().Max(control => control.Bottom)}");
            var navigation = Descendants(usage)
                .OfType<NavButton>()
                .Select(button => button.AccessibleName)
                .ToArray();
            Assert.DoesNotContain(
                localizer.Text("Settings.UsageEstimates"),
                navigation);
            Assert.DoesNotContain(
                localizer.Text("Settings.Backup"),
                navigation);
            Assert.DoesNotContain(
                localizer.Text("Settings.Language"),
                navigation);
            Assert.DoesNotContain(
                localizer.Text("Settings.Connection"),
                navigation);
            Assert.Same(
                generalPage,
                GetField<ComboBox>(general, "_locale").Parent);
            var locale = GetField<ComboBox>(general, "_locale");
            var languageHeading = generalPage.Controls
                .OfType<Label>()
                .Single(label =>
                    label.Text == localizer.Text("Settings.Language"));
            AssertNoOverlap(
                languageHeading,
                locale,
                "language heading / locale");
            Assert.Equal(
                FlatStyle.Standard,
                locale.FlatStyle);
            Assert.Equal(
                "ja-JP",
                locale.SelectedItem);
            Assert.Same(
                advancedPage,
                GetField<ComboBox>(advanced, "_codexPathMode").Parent);
            var codexPath = GetField<TextBox>(advanced, "_codexPath");
            var codexLabel = advancedPage.Controls
                .OfType<Label>()
                .Single(label =>
                    label.Text == localizer.Text("Settings.CodexExecutable"));
            var backupHint = advancedPage.Controls
                .OfType<Label>()
                .Single(label =>
                    label.Text == localizer.Text("Settings.BackupHint"));
            var logRetentionLabel = advancedPage.Controls
                .OfType<Label>()
                .Single(label =>
                    label.Text == localizer.Text("Settings.LogRetention"));
            var logRetention = GetField<ComboBox>(advanced, "_logRetention");
            AssertNoOverlap(
                codexLabel,
                codexPath,
                "Codex executable label / path");
            AssertNoOverlap(
                backupHint,
                logRetentionLabel,
                "backup hint / log retention label");
            AssertNoOverlap(
                backupHint,
                logRetention,
                "backup hint / log retention combo");
            Assert.True(
                GetField<ToggleSwitch>(
                    notifications,
                    "_remaining30Notification").Checked);
            Assert.True(
                GetField<ToggleSwitch>(
                    notifications,
                    "_remaining10Notification").Checked);
            Assert.True(
                GetField<ToggleSwitch>(
                    notifications,
                    "_scheduledResetNotification").Checked);
            SaveSnapshotIfRequested(usage, "settings-usage-display.png");
            SaveSnapshotIfRequested(general, "settings-general.png");
            SaveSnapshotIfRequested(
                notifications,
                "settings-notifications.png");
            SaveSnapshotIfRequested(
                acquisition,
                "settings-usage-acquisition.png");
            SaveSnapshotIfRequested(advanced, "settings-advanced.png");
        });
    }

    [Fact]
    public void DetailHeaderReturnsToMiniAndCompactAndOverviewUsesDashboardLayout()
    {
        RunSta(() =>
        {
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings
            {
                Mode = "manual",
                Locale = "ja-JP",
            });
            using var detail = new DetailForm(localizer);
            var miniRequested = false;
            var compactRequested = false;
            var usageRequested = false;
            detail.MiniRequested += (_, _) => miniRequested = true;
            detail.CompactRequested += (_, _) => compactRequested = true;
            detail.UsageViewRequested += (_, _) => usageRequested = true;
            detail.UpdateState(
                new WeeklyQuotaState(
                    "weekly",
                    BucketRole.Primary,
                    7,
                    93,
                    10080,
                    DateTimeOffset.Now.AddDays(6),
                    3,
                    [
                        new ResetCredit(DateTimeOffset.Now.AddDays(17)),
                        new ResetCredit(DateTimeOffset.Now.AddDays(41)),
                        new ResetCredit(DateTimeOffset.Now.AddDays(76)),
                    ],
                    "ChatGPT Pro",
                    DateTimeOffset.Now,
                    "0.2.0"),
                false,
                null,
                []);
            detail.Show();
            Application.DoEvents();

            Descendants(detail)
                .OfType<Button>()
                .Single(button =>
                    button.Text == localizer.Text("Menu.ShowMini"))
                .PerformClick();
            Descendants(detail)
                .OfType<Button>()
                .Single(button =>
                    button.Text == localizer.Text("Menu.ShowCompact"))
                .PerformClick();
            Descendants(detail)
                .OfType<Button>()
                .Single(button =>
                    button.Text == localizer.Text("Detail.UsageTab"))
                .PerformClick();

            Assert.True(miniRequested);
            Assert.True(compactRequested);
            Assert.True(usageRequested);
            Assert.Equal(
                3,
                GetField<FlowLayoutPanel>(detail, "_credits")
                    .Controls
                    .OfType<RoundedPanel>()
                    .Count());
            Assert.True(GetField<Label>(detail, "_resetAt").Font.Bold);
            var plan = GetField<Label>(detail, "_plan");
            var connection = GetField<Label>(detail, "_connection");
            var version = GetField<Label>(detail, "_version");
            Assert.Equal(plan.Left, connection.Left);
            Assert.Equal(plan.Left, version.Left);
            Assert.StartsWith("● ", connection.Text);
            Assert.Equal(Theme.Green, connection.ForeColor);
            Assert.Contains(
                localizer.Text("Detail.DataSource"),
                GetField<Label>(detail, "_status").Text);
            Assert.True(
                GetField<Label>(detail, "_status").Parent!.Bottom <=
                detail.ClientSize.Height - 96);
            Assert.Contains(
                "93%",
                GetField<QuotaRingControl>(detail, "_quotaRing").Controls
                    .OfType<Label>()
                    .Select(label => label.Text));
        });
    }

    [Fact]
    public void BlueFamilyDashboardUsesDistinctGreenForUsedQuota()
    {
        RunSta(() =>
        {
            Theme.Configure("dark", "cyan");
            try
            {
                var localizer = new LocalizationService(FindLocalesDirectory());
                localizer.Load(new LanguageSettings
                {
                    Mode = "manual",
                    Locale = "ja-JP",
                });
                using var detail = new DetailForm(localizer);
                detail.UpdateState(
                    new WeeklyQuotaState(
                        "weekly",
                        BucketRole.Primary,
                        18,
                        82,
                        10080,
                        DateTimeOffset.Now.AddDays(6),
                        0,
                        [],
                        "ChatGPT Pro",
                        DateTimeOffset.Now,
                        "0.2.0"),
                    false,
                    null,
                    []);
                detail.Show();
                Application.DoEvents();

                Assert.Equal(
                    Theme.Green,
                    GetField<Label>(detail, "_usedShare").ForeColor);
                Assert.Equal(
                    Theme.Accent,
                    GetField<Label>(detail, "_remainingShare").ForeColor);
                Assert.NotEqual(Theme.UsedQuota, Theme.Accent);
                SaveSnapshotIfRequested(detail, "overview-cyan.png");
            }
            finally
            {
                Theme.Configure("dark", "green");
            }
        });
    }

    [Fact]
    public void SettingsCancelRestoresPreviewAndSaveCommitsIt()
    {
        RunSta(() =>
        {
            var appSettings = new AppSettings();
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(appSettings.Language);
            using (var cancelled = new SettingsForm(appSettings, localizer))
            {
                cancelled.Show();
                Application.DoEvents();
                GetField<ComboBox>(cancelled, "_theme").SelectedItem = "light";
                Assert.Equal("light", appSettings.Display.Theme);
                cancelled.Close();
                Application.DoEvents();
            }
            Assert.Equal("dark", appSettings.Display.Theme);

            using var saved = new SettingsForm(appSettings, localizer);
            saved.Show();
            Application.DoEvents();
            GetField<ComboBox>(saved, "_theme").SelectedItem = "light";
            GetField<ComboBox>(saved, "_usageRefreshInterval").SelectedIndex = 0;
            GetField<ToggleSwitch>(saved, "_usageRefreshWhenOpened").Checked = false;
            var save = Descendants(saved)
                .OfType<Button>()
                .Single(button => button.Text == localizer.Text("Common.Save"));
            save.PerformClick();
            Assert.Equal("light", appSettings.Display.Theme);
            Assert.Equal(1, appSettings.UsageAnalytics.RefreshIntervalMinutes);
            Assert.False(appSettings.UsageAnalytics.RefreshWhenOpened);
        });
    }

    [Fact]
    public void VisibleSettingsWindowClosesOnSecondSettingsRequest()
    {
        RunSta(() =>
        {
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings());
            using var settings = new SettingsForm(
                new AppSettings(),
                localizer);
            settings.Show();
            Application.DoEvents();

            Assert.True(
                QuantaTrainContext.TryCloseVisibleSettings(settings));
            Application.DoEvents();
            Assert.False(settings.Visible);
            Assert.False(
                QuantaTrainContext.TryCloseVisibleSettings(settings));
        });
    }

    [Fact]
    public void UsagePageHandlesDisabledAndPopulatedSnapshotsWithoutErrors()
    {
        RunSta(() =>
        {
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings());
            using var form = new DetailForm(localizer);
            var settings = new UsageAnalyticsSettings();
            form.UpdateUsage(null, settings, false);
            Assert.Contains(
                Descendants(form).OfType<Label>(),
                label => label.Text == localizer.Text("Usage.Disabled"));

            settings.Enabled = true;
            var key = new UsageAggregateKey(
                new DateOnly(2026, 7, 26),
                "gpt-5.6-sol",
                "high",
                "fast");
            var row = new UsageAggregate(
                key,
                new UsageTokenTotals(100, 20, 0, 50, 10, 150),
                1,
                12_000,
                0,
                1,
                0,
                0);
            var terra = new UsageAggregate(
                key with { Model = "gpt-5.6-terra", ReasoningEffort = "medium" },
                new UsageTokenTotals(72, 10, 0, 28, 4, 100),
                2,
                8_000,
                0,
                2,
                0,
                0);
            var luna = new UsageAggregate(
                key with { Model = "gpt-5.6-luna", ReasoningEffort = "low" },
                new UsageTokenTotals(40, 4, 0, 12, 2, 54),
                1,
                3_000,
                0,
                1,
                0,
                0);
            var other = new UsageAggregate(
                key with { Model = "other", ReasoningEffort = "maximum" },
                new UsageTokenTotals(15, 0, 0, 5, 1, 20),
                1,
                1_000,
                0,
                1,
                0,
                0);
            var snapshot = new UsageAnalysisSnapshot(
                DateTimeOffset.Parse("2026-07-22T00:00:00Z"),
                DateTimeOffset.Parse("2026-07-29T00:00:00Z"),
                false,
                [row, terra, luna, other],
                DateTimeOffset.Parse("2026-07-26T01:30:45Z"),
                1,
                0,
                0);
            form.UpdateUsage(snapshot, settings, false);
            form.Show();
            Application.DoEvents();
            var usageTab = Descendants(form)
                .OfType<Button>()
                .Single(button =>
                    button.Text == localizer.Text("Detail.UsageTab"));
            usageTab.PerformClick();
            Application.DoEvents();
            SaveSnapshotIfRequested(form, "usage-analysis.png");

            Assert.Contains(
                Descendants(form).OfType<Label>(),
                label => label.Text == "gpt-5.6-sol");
            Assert.Contains(
                Descendants(form).OfType<Label>(),
                label => label.Text.Contains(
                    localizer.Text("Usage.Effort.high"),
                    StringComparison.Ordinal));
            Assert.Equal(
                324,
                GetField<UsageDonutControl>(form, "_reasoningDonut").Total);
            Assert.Equal(
                8,
                Enumerable.Range(0, 8)
                    .Select(index => UsageVisuals.ModelColor(index).ToArgb())
                    .Distinct()
                    .Count());
            Assert.NotEqual(
                UsageVisuals.ModelColor(0).ToArgb(),
                UsageVisuals.ModelColor(0, isOther: true).ToArgb());
        });
    }

    [Fact]
    public void MiniRemainsUnchangedAndCompactUsesCondensedDashboard()
    {
        RunSta(() =>
        {
            Theme.Configure("dark", "green");
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings
            {
                Mode = "manual",
                Locale = "ja-JP",
            });
            using var mini = new MiniForm(localizer);
            using var compact = new CompactForm(localizer);
            var state = new WeeklyQuotaState(
                "weekly",
                BucketRole.Primary,
                9,
                91,
                10080,
                DateTimeOffset.Now.AddDays(6).AddHours(2),
                1,
                [new ResetCredit(DateTimeOffset.Now.AddDays(12))],
                "ChatGPT Pro",
                DateTimeOffset.Now,
                "0.2.0");
            mini.UpdateState(state);
            compact.UpdateState(
                state,
                false,
                null,
                ["2026/07/25 11:18  reset-credit-likely"]);
            mini.Show();
            compact.Show();
            Application.DoEvents();

            Assert.Equal(new Size(220, 95), mini.ClientSize);
            Assert.Equal(new Size(240, 338), compact.ClientSize);
            Assert.Equal(
                3,
                Descendants(compact).OfType<RoundedPanel>().Count());
            Assert.Contains(
                Descendants(compact).OfType<Label>(),
                label => label.Text.Contains("(6D", StringComparison.Ordinal));
            Assert.DoesNotContain(
                Descendants(compact).OfType<Label>(),
                label => label.Text == localizer.Text("Status.Latest"));
            SaveSnapshotIfRequested(compact, "compact.png");
            SaveSnapshotIfRequested(mini, "mini.png");
        });
    }

    [Fact]
    public void CompactUpdatingStatusIsFullyVisible()
    {
        RunSta(() =>
        {
            Theme.Configure("dark", "green");
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(new LanguageSettings
            {
                Mode = "manual",
                Locale = "ja-JP",
            });
            using var compact = new CompactForm(localizer);
            var state = new WeeklyQuotaState(
                "weekly",
                BucketRole.Primary,
                16,
                84,
                10080,
                DateTimeOffset.Now.AddDays(6),
                1,
                [new ResetCredit(DateTimeOffset.Now.AddDays(12))],
                "ChatGPT Pro",
                DateTimeOffset.Now,
                "0.2.0");
            compact.UpdateState(
                state,
                updating: true,
                error: null,
                ["2026/07/25 11:18  reset-credit-likely"]);
            compact.Show();
            Application.DoEvents();

            var card = GetField<RoundedPanel>(compact, "_quotaCard");
            var status = GetField<Label>(compact, "_status");
            Assert.Equal(localizer.Text("Status.Updating"), status.Text);
            Assert.True(status.Top >= 0);
            Assert.True(status.Bottom <= card.ClientSize.Height);
            Assert.True(status.Height >= status.PreferredHeight);
            SaveSnapshotIfRequested(compact, "compact-updating.png");
        });
    }

    [Fact]
    public void QuotaWarningColorsOverrideSelectedAccent()
    {
        Theme.Configure("dark", "purple");
        try
        {
            Assert.Equal(Theme.Accent, Theme.QuotaColor(31));
            Assert.Equal(Theme.Orange, Theme.QuotaColor(30));
            Assert.Equal(Theme.Red, Theme.QuotaColor(10));
        }
        finally
        {
            Theme.Configure("dark", "green");
        }
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

    private static void SaveSnapshotIfRequested(Form form, string fileName)
    {
        var directory = Environment.GetEnvironmentVariable(
            "QUANTATRAY_QA_OUTPUT");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
        using var bitmap = new Bitmap(
            form.ClientSize.Width,
            form.ClientSize.Height);
        form.DrawToBitmap(bitmap, form.ClientRectangle);
        bitmap.Save(Path.Combine(directory, fileName));
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

        if (form is MiniForm)
        {
            Assert.True(
                weekly.Right <= prefix.Left,
                $"Weekly {weekly.Bounds} overlaps prefix {prefix.Bounds}.");
        }
        else
        {
            Assert.True(
                weekly.Bottom <= percent.Top,
                $"Weekly {weekly.Bounds} overlaps percent {percent.Bounds}.");
        }
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
        if (form is MiniForm)
        {
            var reset = GetField<Label>(form, "_reset");
            var resetValue = GetField<Label>(form, "_countdown");
            Assert.True(
                reset.Right <= resetValue.Left,
                $"Reset caption {reset.Bounds} overlaps value {resetValue.Bounds}.");
            Assert.True(
                resetValue.Right <= card.ClientSize.Width,
                $"Reset value {resetValue.Bounds} exceeds card {card.ClientRectangle}.");
        }
    }

    private static void AssertNoOverlap(
        Control first,
        Control second,
        string description)
    {
        var overlap = Rectangle.Intersect(first.Bounds, second.Bounds);
        Assert.True(
            overlap.IsEmpty,
            $"{description}: {first.Bounds} overlaps {second.Bounds} at {overlap}.");
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);
}
