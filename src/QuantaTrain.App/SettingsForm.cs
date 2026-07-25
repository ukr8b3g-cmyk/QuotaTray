using QuantaTrain.Core;

namespace QuantaTrain.App;

internal enum SettingsPreviewKind
{
    Opacity,
    DisplayBehavior,
    DisplayMode,
    Appearance,
}

internal sealed class SettingsPreviewChangedEventArgs(SettingsPreviewKind kind)
    : EventArgs
{
    public SettingsPreviewKind Kind { get; } = kind;
}

internal sealed class SettingsForm : FramelessForm
{
    private readonly AppSettings _settings;
    private readonly LocalizationService _localizer;
    private readonly Panel _contentHost = new();
    private readonly List<NavButton> _navButtons = [];
    private readonly List<Panel> _pages = [];

    private readonly ToggleSwitch _launchAtStartup = new();
    private readonly ComboBox _startupMode = new();
    private readonly ComboBox _refreshInterval = new();
    private readonly ComboBox _theme = new();
    private readonly ComboBox _locale = new();
    private readonly ValueSlider _opacity = new();
    private readonly Label _opacityValue = new();
    private readonly ToggleSwitch _alwaysOnTop = new();
    private readonly ToggleSwitch _lockPosition = new();
    private readonly ToggleSwitch _rememberPosition = new();
    private readonly ToggleSwitch _snapToEdge = new();
    private readonly ToggleSwitch _miniClickThrough = new();
    private readonly ComboBox _displayMode = new();
    private readonly NumericUpDown _historyDays = new();
    private readonly TextBox _codexPath = new();
    private readonly string _initialDisplayMode;
    private string _accent = "green";
    private bool _loadingControls;

    public SettingsForm(
        AppSettings settings,
        LocalizationService localizer,
        int initialPage = 1,
        string initialDisplayMode = "compact")
    {
        _settings = settings;
        _localizer = localizer;
        _initialDisplayMode = initialDisplayMode;
        Text = $"{_localizer.Text("Common.Settings")} — QuantaTray";
        ClientSize = new Size(448, 570);
        StartPosition = FormStartPosition.Manual;
        AccessibleName = "QuantaTray settings";

        var title = UiFactory.Label(
            _localizer.Text("Common.Settings"),
            new Point(18, 14),
            10F,
            FontStyle.Bold);
        title.Size = new Size(200, 28);
        title.AutoSize = false;
        title.TextAlign = ContentAlignment.MiddleLeft;

        var closeIcon = new IconButton(FluentSymbol.Close)
        {
            AccessibleName = _localizer.Text("Common.Close"),
            Bounds = new Rectangle(407, 10, 31, 32),
        };
        closeIcon.Click += (_, _) => Close();

        var sidebar = new Panel
        {
            Bounds = new Rectangle(10, 55, 116, 448),
            BackColor = Theme.Window,
        };
        var divider = new Panel
        {
            Bounds = new Rectangle(128, 55, 1, 448),
            BackColor = Theme.Border,
        };
        _contentHost.Bounds = new Rectangle(143, 55, 293, 448);
        _contentHost.BackColor = Theme.Window;

        AddNav(sidebar, FluentSymbol.General, _localizer.Text("Settings.General"), 0);
        AddNav(sidebar, FluentSymbol.Display, _localizer.Text("Settings.Display"), 1);
        AddNav(sidebar, FluentSymbol.Language, _localizer.Text("Settings.Language"), 2);
        AddNav(sidebar, FluentSymbol.Notification, _localizer.Text("Settings.Notifications"), 3);
        AddNav(sidebar, FluentSymbol.History, _localizer.Text("Settings.History"), 4);
        AddNav(sidebar, FluentSymbol.Account, _localizer.Text("Settings.Connection"), 5);
        AddNav(sidebar, FluentSymbol.Info, _localizer.Text("Settings.About"), 6);

        _pages.Add(BuildGeneralPage());
        _pages.Add(BuildDisplayPage());
        _pages.Add(BuildLanguagePage());
        _pages.Add(BuildNotificationsPage());
        _pages.Add(BuildHistoryPage());
        _pages.Add(BuildConnectionPage());
        _pages.Add(BuildAboutPage());
        _contentHost.Controls.AddRange([.. _pages]);

        var footerLine = new Panel
        {
            Bounds = new Rectangle(0, 510, 448, 1),
            BackColor = Theme.Border,
        };
        var defaults = UiFactory.TextButton(
            _localizer.Text("Settings.RestoreDefaults"),
            new Rectangle(17, 526, 174, 30),
            danger: true);
        defaults.Click += (_, _) =>
        {
            if (MessageBox.Show(
                    this,
                    _localizer.Text("Settings.ResetAllConfirm"),
                    _localizer.Text("Settings.RestoreDefaults"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning)
                != DialogResult.Yes)
            {
                return;
            }

            ResetAllSettings();
            AllSettingsResetRequested?.Invoke(this, EventArgs.Empty);
            NotifyPreviewChanged(SettingsPreviewKind.Appearance);
        };

        var done = UiFactory.TextButton(
            _localizer.Text("Common.Close"),
            new Rectangle(326, 526, 104, 30),
            primary: true);
        done.Click += (_, _) =>
        {
            ApplyControls();
            SettingsSaved?.Invoke(this, EventArgs.Empty);
            Close();
        };

        Controls.AddRange(
        [
            title, closeIcon, sidebar, divider, _contentHost,
            footerLine, defaults, done,
        ]);
        AcceptButton = done;
        MakeDraggable(this);
        MakeDraggable(title);

        LoadControls(_settings);
        SelectPage(Math.Clamp(initialPage, 0, _pages.Count - 1));
        PanelPlacement.CenterOnPrimary(this);
    }

    public event EventHandler? SettingsSaved;
    public event EventHandler<SettingsPreviewChangedEventArgs>? SettingsPreviewChanged;
    public event EventHandler? PositionResetRequested;
    public event EventHandler? AllSettingsResetRequested;
    public string DisplayMode => _displayMode.SelectedIndex switch
    {
        0 => "mini",
        2 => "detail",
        _ => "compact",
    };

    private void AddNav(
        Control parent,
        string symbol,
        string label,
        int index)
    {
        var nav = new NavButton(symbol, label)
        {
            Bounds = new Rectangle(0, index * 37, 112, 34),
            AccessibleName = label,
        };
        nav.Click += (_, _) => SelectPage(index);
        parent.Controls.Add(nav);
        _navButtons.Add(nav);
    }

    private Panel BuildGeneralPage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.General")));
        AddToggleRow(
            page,
            _localizer.Text("Settings.LaunchAtStartup"),
            _launchAtStartup,
            52);

        ConfigureCombo(_startupMode, ["tray-only", "mini", "compact", "detail"]);
        AddControlRow(
            page,
            _localizer.Text("Settings.StartupMode"),
            _startupMode,
            100);

        ConfigureCombo(_refreshInterval, [60, 120, 300]);
        AddControlRow(
            page,
            _localizer.Text("Settings.RefreshInterval"),
            _refreshInterval,
            148);
        return page;
    }

    private Panel BuildDisplayPage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.Display")));

        ConfigureCombo(_theme, ["dark", "light", "system"]);
        AddControlRow(page, _localizer.Text("Settings.Theme"), _theme, 48);

        var accentLabel = RowLabel(_localizer.Text("Settings.Accent"), 96);
        page.Controls.Add(accentLabel);
        var colors = new[]
        {
            ("green", Theme.Green),
            ("blue", Theme.Blue),
            ("cyan", Color.FromArgb(73, 179, 214)),
            ("purple", Color.FromArgb(171, 94, 230)),
            ("yellow", Theme.Yellow),
            ("gray", Theme.Subtle),
        };
        for (var index = 0; index < colors.Length; index++)
        {
            var swatch = new Button
            {
                Bounds = new Rectangle(143 + index * 25, 92, 21, 21),
                BackColor = colors[index].Item2,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                AccessibleName = colors[index].Item1,
                TabStop = true,
            };
            swatch.FlatAppearance.BorderColor = Theme.Window;
            swatch.FlatAppearance.BorderSize = 2;
            var accent = colors[index].Item1;
            swatch.Click += (_, _) =>
            {
                _accent = accent;
                foreach (var item in page.Controls.OfType<Button>())
                {
                    item.FlatAppearance.BorderColor = string.Equals(
                        item.AccessibleName,
                        _accent,
                        StringComparison.Ordinal)
                        ? Theme.Text
                        : Theme.Window;
                }
                NotifyPreviewChanged(SettingsPreviewKind.Appearance);
            };
            page.Controls.Add(swatch);
        }

        page.Controls.Add(RowLabel(_localizer.Text("Settings.Opacity"), 139));
        _opacity.Bounds = new Rectangle(135, 136, 102, 22);
        _opacityValue.Bounds = new Rectangle(241, 137, 47, 22);
        _opacityValue.Font = Theme.Ui(8.7F);
        _opacityValue.ForeColor = Theme.Text;
        _opacityValue.TextAlign = ContentAlignment.MiddleRight;
        _opacity.ValueChanged += (_, _) =>
        {
            _opacityValue.Text = $"{_opacity.Value}%";
            NotifyPreviewChanged(SettingsPreviewKind.Opacity);
        };
        _theme.SelectedValueChanged += (_, _) =>
            NotifyPreviewChanged(SettingsPreviewKind.Appearance);
        _locale.SelectedValueChanged += (_, _) =>
            NotifyPreviewChanged(SettingsPreviewKind.Appearance);
        page.Controls.AddRange([_opacity, _opacityValue]);

        AddToggleRow(page, _localizer.Text("Menu.AlwaysOnTop"), _alwaysOnTop, 177);
        AddToggleRow(page, _localizer.Text("Menu.LockPosition"), _lockPosition, 215);
        AddToggleRow(
            page,
            _localizer.Text("Settings.RememberPosition"),
            _rememberPosition,
            253);
        AddToggleRow(page, _localizer.Text("Settings.SnapToEdge"), _snapToEdge, 291);
        AddToggleRow(
            page,
            _localizer.Text("Settings.MiniClickThrough"),
            _miniClickThrough,
            329);
        _alwaysOnTop.CheckedChanged += (_, _) =>
            NotifyPreviewChanged(SettingsPreviewKind.DisplayBehavior);
        _lockPosition.CheckedChanged += (_, _) =>
            NotifyPreviewChanged(SettingsPreviewKind.DisplayBehavior);
        _rememberPosition.CheckedChanged += (_, _) =>
            NotifyPreviewChanged(SettingsPreviewKind.DisplayBehavior);
        _snapToEdge.CheckedChanged += (_, _) =>
            NotifyPreviewChanged(SettingsPreviewKind.DisplayBehavior);
        _miniClickThrough.CheckedChanged += (_, _) =>
            NotifyPreviewChanged(SettingsPreviewKind.DisplayBehavior);

        ConfigureCombo(
            _displayMode,
            [
                _localizer.Text("Menu.ShowMini"),
                _localizer.Text("Menu.ShowCompact"),
                _localizer.Text("Menu.ShowDetail"),
            ]);
        _displayMode.SelectedIndexChanged += (_, _) =>
            NotifyPreviewChanged(SettingsPreviewKind.DisplayMode);
        AddControlRow(
            page,
            _localizer.Text("Settings.DisplayState"),
            _displayMode,
            380);
        var resetPosition = UiFactory.TextButton(
            _localizer.Text("Menu.ResetPosition"),
            new Rectangle(0, 413, 270, 28));
        resetPosition.Click += (_, _) =>
            PositionResetRequested?.Invoke(this, EventArgs.Empty);
        page.Controls.Add(resetPosition);
        return page;
    }

    private Panel BuildLanguagePage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.Language")));
        ConfigureCombo(
            _locale,
            [
                "auto", "ja-JP", "en-US", "zh-Hans", "zh-Hant", "ko-KR",
                "de-DE", "fr-FR", "es-ES", "pt-BR", "ru-RU",
            ]);
        _locale.Bounds = new Rectangle(0, 53, 270, 29);
        page.Controls.Add(_locale);
        page.Controls.Add(
            UiFactory.Label(
                _localizer.Text("Settings.LanguageHint"),
                new Point(0, 94),
                8.5F,
                FontStyle.Regular,
                Theme.Muted));
        return page;
    }

    private Panel BuildNotificationsPage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.Notifications")));
        AddToggleRow(
            page,
            _localizer.Text("Settings.ResetNotifications"),
            new ToggleSwitch { Checked = _settings.Notifications.ScheduledReset },
            52);
        AddToggleRow(
            page,
            _localizer.Text("Settings.ConnectionNotifications"),
            new ToggleSwitch
            {
                Checked = _settings.Notifications.PersistentConnectionFailure,
            },
            92);
        page.Controls.Add(
            UiFactory.Label(
                _localizer.Text("Settings.NotificationHint"),
                new Point(0, 145),
                8.5F,
                FontStyle.Regular,
                Theme.Muted));
        return page;
    }

    private Panel BuildHistoryPage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.History")));
        page.Controls.Add(RowLabel(_localizer.Text("Settings.RetentionDays"), 55));
        _historyDays.Bounds = new Rectangle(164, 50, 104, 27);
        _historyDays.Minimum = 1;
        _historyDays.Maximum = 3650;
        _historyDays.BackColor = Theme.SurfaceRaised;
        _historyDays.ForeColor = Theme.Text;
        _historyDays.BorderStyle = BorderStyle.FixedSingle;
        page.Controls.Add(_historyDays);
        return page;
    }

    private Panel BuildConnectionPage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.Connection")));
        page.Controls.Add(RowLabel(_localizer.Text("Settings.CodexExecutable"), 54));
        _codexPath.Bounds = new Rectangle(0, 84, 270, 28);
        _codexPath.BackColor = Theme.SurfaceRaised;
        _codexPath.ForeColor = Theme.Text;
        _codexPath.BorderStyle = BorderStyle.FixedSingle;
        page.Controls.Add(_codexPath);
        page.Controls.Add(
            UiFactory.Label(
                _localizer.Text("Settings.CodexPathHint"),
                new Point(0, 126),
                8.3F,
                FontStyle.Regular,
                Theme.Muted));
        return page;
    }

    private Panel BuildAboutPage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.About")));
        page.Controls.Add(
            UiFactory.Label(
                "QuantaTray",
                new Point(0, 56),
                12F,
                FontStyle.Bold));
        page.Controls.Add(
            UiFactory.Label(
                $"Version {typeof(SettingsForm).Assembly.GetName().Version?.ToString(3) ?? "0.1.2"}",
                new Point(0, 86),
                8.7F,
                FontStyle.Regular,
                Theme.Muted));
        page.Controls.Add(
            UiFactory.Label(
                _localizer.Text("Settings.Unofficial"),
                new Point(0, 119),
                8.5F,
                FontStyle.Regular,
                Theme.Muted));
        return page;
    }

    private void SelectPage(int index)
    {
        for (var item = 0; item < _pages.Count; item++)
        {
            _pages[item].Visible = item == index;
            _navButtons[item].Selected = item == index;
        }
        _pages[index].BringToFront();
    }

    private static Panel CreatePage() => new()
    {
        Bounds = new Rectangle(0, 0, 293, 448),
        BackColor = Theme.Window,
    };

    private static Label PageTitle(string text)
    {
        var label = UiFactory.Label(text, new Point(0, 2), 9F, FontStyle.Bold);
        label.Size = new Size(270, 25);
        label.AutoSize = false;
        return label;
    }

    private static Label RowLabel(string text, int y)
    {
        var label = UiFactory.Label(text, new Point(0, y), 8.8F);
        label.Size = new Size(142, 25);
        label.AutoSize = false;
        label.TextAlign = ContentAlignment.MiddleLeft;
        return label;
    }

    private static void AddToggleRow(
        Control page,
        string label,
        ToggleSwitch toggle,
        int y)
    {
        page.Controls.Add(RowLabel(label, y));
        toggle.Location = new Point(230, y + 1);
        page.Controls.Add(toggle);
    }

    private static void AddControlRow(
        Control page,
        string label,
        Control control,
        int y)
    {
        page.Controls.Add(RowLabel(label, y));
        control.Bounds = new Rectangle(143, y - 1, 127, 28);
        page.Controls.Add(control);
    }

    private static void ConfigureCombo(ComboBox combo, object[] items)
    {
        Theme.StyleCombo(combo);
        combo.Items.Clear();
        combo.Items.AddRange(items);
    }

    private void LoadControls(AppSettings settings)
    {
        _loadingControls = true;
        _launchAtStartup.Checked = settings.General.LaunchAtStartup;
        _startupMode.SelectedItem = settings.General.StartupMode;
        _startupMode.SelectedIndex = _startupMode.SelectedIndex < 0 ? 0 : _startupMode.SelectedIndex;
        _refreshInterval.SelectedItem = settings.General.RefreshIntervalSeconds;
        _refreshInterval.SelectedIndex =
            _refreshInterval.SelectedIndex < 0 ? 0 : _refreshInterval.SelectedIndex;
        _theme.SelectedItem = settings.Display.Theme;
        _theme.SelectedIndex = _theme.SelectedIndex < 0 ? 0 : _theme.SelectedIndex;
        _accent = settings.Display.Accent;
        _opacity.Value = Math.Clamp(settings.Display.OpacityPercent, 60, 100);
        _opacityValue.Text = $"{_opacity.Value}%";
        _alwaysOnTop.Checked = settings.Display.AlwaysOnTop;
        _lockPosition.Checked = settings.Display.LockPosition;
        _rememberPosition.Checked = settings.Display.RememberPosition;
        _snapToEdge.Checked = settings.Display.SnapToEdge;
        _miniClickThrough.Checked = settings.Display.MiniClickThrough;
        _displayMode.SelectedIndex = _initialDisplayMode.ToLowerInvariant() switch
        {
            "mini" => 0,
            "detail" => 2,
            _ => 1,
        };
        _locale.SelectedItem = settings.Language.Mode == "auto"
            ? "auto"
            : settings.Language.Locale;
        _locale.SelectedIndex = _locale.SelectedIndex < 0 ? 0 : _locale.SelectedIndex;
        _historyDays.Value = Math.Clamp(settings.History.RetentionDays ?? 365, 1, 3650);
        _codexPath.Text = settings.Connection.CodexExecutablePath ?? string.Empty;
        _loadingControls = false;
    }

    private void ApplyControls()
    {
        _settings.General.LaunchAtStartup = _launchAtStartup.Checked;
        _settings.General.StartupMode = _startupMode.SelectedItem?.ToString() ?? "tray-only";
        _settings.General.RefreshIntervalSeconds =
            (int?)_refreshInterval.SelectedItem ?? 60;
        _settings.Display.Theme = _theme.SelectedItem?.ToString() ?? "dark";
        _settings.Display.Accent = _accent;
        _settings.Display.OpacityPercent = _opacity.Value;
        _settings.Display.AlwaysOnTop = _alwaysOnTop.Checked;
        _settings.Display.LockPosition = _lockPosition.Checked;
        _settings.Display.RememberPosition = _rememberPosition.Checked;
        _settings.Display.SnapToEdge = _snapToEdge.Checked;
        _settings.Display.MiniClickThrough = _miniClickThrough.Checked;
        var locale = _locale.SelectedItem?.ToString() ?? "auto";
        _settings.Language.Mode = locale == "auto" ? "auto" : "manual";
        if (locale != "auto")
        {
            _settings.Language.Locale = locale;
        }
        _settings.History.RetentionDays = (int)_historyDays.Value;
        _settings.Connection.CodexExecutablePath =
            string.IsNullOrWhiteSpace(_codexPath.Text) ? null : _codexPath.Text.Trim();
        _settings.Connection.CodexPathMode =
            _settings.Connection.CodexExecutablePath is null ? "auto" : "manual";
    }

    private void ResetAllSettings()
    {
        var defaults = new AppSettings();
        _settings.SchemaVersion = defaults.SchemaVersion;
        _settings.General = defaults.General;
        _settings.Display = defaults.Display;
        _settings.Language = defaults.Language;
        _settings.Notifications = defaults.Notifications;
        _settings.History = defaults.History;
        _settings.Connection = defaults.Connection;
        LoadControls(_settings);
    }

    private void NotifyPreviewChanged(SettingsPreviewKind kind)
    {
        if (_loadingControls)
        {
            return;
        }

        ApplyControls();
        SettingsPreviewChanged?.Invoke(
            this,
            new SettingsPreviewChangedEventArgs(kind));
    }

}
