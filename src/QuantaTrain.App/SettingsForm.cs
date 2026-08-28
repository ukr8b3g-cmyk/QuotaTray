using System.Text.Json;
using QuantaTrain.Core;

namespace QuantaTrain.App;

internal enum SettingsPreviewKind
{
    Opacity,
    DisplayBehavior,
    DisplayMode,
    Appearance,
}

internal sealed class UsageExportRequestedEventArgs(string format)
    : EventArgs
{
    public string Format { get; } = format;
}

internal sealed class OpenDocumentRequestedEventArgs(string fileName)
    : EventArgs
{
    public string FileName { get; } = fileName;
}

internal sealed class SettingsPreviewChangedEventArgs(SettingsPreviewKind kind)
    : EventArgs
{
    public SettingsPreviewKind Kind { get; } = kind;
}

internal sealed class SettingsForm : FixedWidthResizableForm
{
    private readonly AppSettings _settings;
    private readonly AppSettings _originalSettings;
    private readonly LocalizationService _localizer;
    private readonly ToolTip _help = UiHelp.Create();
    private readonly Panel _contentHost = new();
    private readonly List<NavButton> _navButtons = [];
    private readonly List<Panel> _pages = [];

    private readonly ToggleSwitch _launchAtStartup = new();
    private readonly ToggleSwitch _refreshOnOpen = new();
    private readonly ToggleSwitch _showCachedOnFailure = new();
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
    private readonly ToggleSwitch _rememberDetailHeight = new();
    private readonly ToggleSwitch _rememberSettingsHeight = new();
    private readonly ComboBox _displayMode = new();
    private readonly NumericUpDown _historyDays = new();
    private readonly ComboBox _retention = new();
    private readonly TextBox _codexPath = new();
    private readonly ToggleSwitch _scheduledResetNotification = new();
    private readonly ToggleSwitch _remaining30Notification = new();
    private readonly ToggleSwitch _remaining10Notification = new();
    private readonly ToggleSwitch _unexpectedResetNotification = new();
    private readonly ToggleSwitch _creditExpiryNotification = new();
    private readonly ToggleSwitch _connectionFailureNotification = new();
    private readonly ToggleSwitch _storeQuotaState = new();
    private readonly ToggleSwitch _detectRecovery = new();
    private readonly ToggleSwitch _confirmRecovery = new();
    private readonly NumericUpDown _recentHistoryCount = new();
    private readonly ToggleSwitch _usageEnabled = new();
    private readonly ComboBox _usageRefreshInterval = new();
    private readonly ToggleSwitch _usageRefreshWhenOpened = new();
    private readonly ToggleSwitch _includeArchives = new();
    private readonly ToggleSwitch _collectModel = new();
    private readonly ToggleSwitch _collectReasoning = new();
    private readonly ToggleSwitch _collectTier = new();
    private readonly ToggleSwitch _collectTokens = new();
    private readonly ToggleSwitch _collectElapsed = new();
    private readonly ToggleSwitch _collectTurns = new();
    private readonly ToggleSwitch _collectTools = new();
    private readonly ToggleSwitch _collectSkills = new();
    private readonly ComboBox _usagePeriod = new();
    private readonly ComboBox _usageMetric = new();
    private readonly ComboBox _chartStyle = new();
    private readonly ComboBox _sortOrder = new();
    private readonly ComboBox _numberFormat = new();
    private readonly NumericUpDown _maximumModels = new();
    private readonly ToggleSwitch _showElapsed = new();
    private readonly ToggleSwitch _showTurns = new();
    private readonly ToggleSwitch _showReasoning = new();
    private readonly ToggleSwitch _showTier = new();
    private readonly ToggleSwitch _groupOtherModels = new();
    private readonly ToggleSwitch _usageEstimate = new();
    private readonly ToggleSwitch _showAccountUsage = new();
    private readonly ToggleSwitch _showActivityBreakdown = new();
    private readonly ComboBox _logRetention = new();
    private readonly ComboBox _codexPathMode = new();
    private readonly Label _connectionStatus = new();
    private readonly Label _usageScanStatus = new();
    private readonly string _initialDisplayMode;
    private string _accent = "green";
    private bool _loadingControls;
    private bool _saved;

    public SettingsForm(
        AppSettings settings,
        LocalizationService localizer,
        int initialPage = 1,
        string initialDisplayMode = "compact")
    {
        _settings = settings;
        _originalSettings = settings.Clone();
        _localizer = localizer;
        _initialDisplayMode = initialDisplayMode;
        Text = $"{_localizer.Text("Common.Settings")} — QuantaTray";
        ConfigureFixedLogicalWidth(800, 720, 680);
        StartPosition = FormStartPosition.Manual;
        AccessibleName = "QuantaTray settings";
        var contentHeight = ClientSize.Height - 122;
        var footerTop = ClientSize.Height - 59;
        var footerButtonTop = ClientSize.Height - 45;

        var title = UiFactory.Label(
            _localizer.Text("Common.Settings"),
            new Point(22, 14),
            10F,
            FontStyle.Bold);
        title.Size = new Size(400, 28);
        title.AutoSize = false;
        title.TextAlign = ContentAlignment.MiddleLeft;

        var closeIcon = new IconButton(FluentSymbol.Close)
        {
            AccessibleName = _localizer.Text("Common.Close"),
            Bounds = new Rectangle(755, 10, 31, 32),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        closeIcon.Click += (_, _) => Close();
        _help.SetToolTip(closeIcon, _localizer.Text("Help.Close"));

        var sidebar = new Panel
        {
            Bounds = new Rectangle(14, 55, 190, contentHeight),
            BackColor = Theme.Window,
            Anchor = AnchorStyles.Left | AnchorStyles.Top |
                AnchorStyles.Bottom,
        };
        var divider = new Panel
        {
            Bounds = new Rectangle(210, 55, 1, contentHeight),
            BackColor = Theme.Border,
            Anchor = AnchorStyles.Left | AnchorStyles.Top |
                AnchorStyles.Bottom,
        };
        _contentHost.Bounds = new Rectangle(228, 55, 552, contentHeight);
        _contentHost.BackColor = Theme.Window;
        _contentHost.Anchor = AnchorStyles.Left | AnchorStyles.Top |
            AnchorStyles.Right | AnchorStyles.Bottom;

        AddNav(sidebar, FluentSymbol.General, _localizer.Text("Settings.General"), 0);
        AddNav(sidebar, FluentSymbol.Display, _localizer.Text("Settings.Display"), 1);
        AddNav(sidebar, FluentSymbol.Notification, _localizer.Text("Settings.Notifications"), 2);
        AddNav(sidebar, FluentSymbol.Refresh, _localizer.Text("Settings.QuotaReset"), 3);
        AddNav(sidebar, FluentSymbol.History, _localizer.Text("Settings.HistoryData"), 4);
        AddNav(sidebar, FluentSymbol.Account, _localizer.Text("Settings.UsageAcquisition"), 5);
        AddNav(sidebar, FluentSymbol.Display, _localizer.Text("Settings.UsageDisplay"), 6);
        AddNav(sidebar, FluentSymbol.Settings, _localizer.Text("Settings.Advanced"), 7);
        AddNav(sidebar, FluentSymbol.Info, _localizer.Text("Settings.About"), 8);

        _pages.Add(BuildGeneralPage());
        _pages.Add(BuildDisplayPage());
        _pages.Add(BuildNotificationsPage());
        _pages.Add(BuildQuotaResetPage());
        _pages.Add(BuildHistoryPage());
        _pages.Add(BuildUsageAcquisitionPage());
        _pages.Add(BuildUsageDisplayPage());
        _pages.Add(BuildAdvancedPage());
        _pages.Add(BuildAboutPage());
        _contentHost.Controls.AddRange([.. _pages]);

        var footerLine = new Panel
        {
            Bounds = new Rectangle(0, footerTop, 800, 1),
            BackColor = Theme.Border,
            Anchor = AnchorStyles.Left | AnchorStyles.Right |
                AnchorStyles.Bottom,
        };
        var defaults = UiFactory.TextButton(
            _localizer.Text("Settings.RestoreDefaults"),
            new Rectangle(18, footerButtonTop, 174, 32),
            danger: true);
        defaults.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        _help.SetToolTip(
            defaults,
            _localizer.Text("Help.RestoreDefaults"));
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

        var cancel = UiFactory.TextButton(
            _localizer.Text("Common.Cancel"),
            new Rectangle(570, footerButtonTop, 96, 32));
        cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        cancel.Click += (_, _) => Close();

        var done = UiFactory.TextButton(
            _localizer.Text("Common.Save"),
            new Rectangle(680, footerButtonTop, 102, 32),
            primary: true);
        done.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        done.Click += (_, _) =>
        {
            ApplyControls();
            _saved = true;
            SettingsSaved?.Invoke(this, EventArgs.Empty);
            Close();
        };

        Controls.AddRange(
        [
            title, closeIcon, sidebar, divider, _contentHost,
            footerLine, defaults, cancel, done,
        ]);
        AcceptButton = done;
        MakeDraggable(this);
        MakeDraggable(title);

        LoadControls(_settings);
        ConfigureHelp();
        SelectPage(Math.Clamp(initialPage, 0, _pages.Count - 1));
        PanelPlacement.CenterOnPrimary(this);
    }

    public event EventHandler? SettingsSaved;
    public event EventHandler<SettingsPreviewChangedEventArgs>? SettingsPreviewChanged;
    public event EventHandler? PositionResetRequested;
    public event EventHandler? AllSettingsResetRequested;
    public event EventHandler? UsageRescanRequested;
    public event EventHandler? UsageCacheRebuildRequested;
    public event EventHandler? ConnectionDiagnosticRequested;
    public event EventHandler? OpenHistoryRequested;
    public event EventHandler? HistoryExportRequested;
    public event EventHandler<UsageExportRequestedEventArgs>? UsageExportRequested;
    public event EventHandler? OpenLogsRequested;
    public event EventHandler? ClearCacheRequested;
    public event EventHandler? ReconnectRequested;
    public event EventHandler? OpenStorageRequested;
    public event EventHandler<OpenDocumentRequestedEventArgs>? OpenDocumentRequested;
    public string DisplayMode => _displayMode.SelectedIndex switch
    {
        0 => "mini",
        2 => "detail",
        _ => "compact",
    };

    public void SetConnectionStatus(string text)
    {
        _connectionStatus.Text = text;
    }

    public void SetUsageScanStatus(string text)
    {
        _usageScanStatus.Text = text;
    }

    private void AddNav(
        Control parent,
        string symbol,
        string label,
        int index)
    {
        var nav = new NavButton(symbol, label)
        {
            Bounds = new Rectangle(0, index * 36, 186, 33),
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
        AddToggleRow(
            page,
            _localizer.Text("Settings.RefreshOnOpen"),
            _refreshOnOpen,
            196);
        AddToggleRow(
            page,
            _localizer.Text("Settings.ShowCachedOnFailure"),
            _showCachedOnFailure,
            238);
        var languageHeading = PageTitle(_localizer.Text("Settings.Language"));
        languageHeading.Location = new Point(0, 286);
        languageHeading.Width = 340;
        languageHeading.Font = Theme.Ui(8.8F, FontStyle.Bold);
        page.Controls.Add(languageHeading);
        ConfigureCombo(
            _locale,
            [
                _localizer.Text("Settings.LanguageAuto"),
                _localizer.Text("Settings.LanguageJapanese"),
                _localizer.Text("Settings.LanguageEnglish"),
            ]);
        _locale.Bounds = new Rectangle(350, 284, 184, 28);
        page.Controls.Add(_locale);
        page.Controls.Add(
            Hint(
                _localizer.Text("Settings.LanguageHint"),
                0,
                326,
                534));
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
                Bounds = new Rectangle(355 + index * 28, 92, 22, 22),
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
        _opacity.Bounds = new Rectangle(350, 136, 130, 22);
        _opacityValue.Bounds = new Rectangle(485, 137, 50, 22);
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
        AddToggleRow(
            page,
            _localizer.Text("Settings.RememberDetailHeight"),
            _rememberDetailHeight,
            367);
        AddToggleRow(
            page,
            _localizer.Text("Settings.RememberSettingsHeight"),
            _rememberSettingsHeight,
            405);
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
            447);
        var resetPosition = UiFactory.TextButton(
            _localizer.Text("Menu.ResetPosition"),
            new Rectangle(282, 482, 252, 28));
        resetPosition.Click += (_, _) =>
            PositionResetRequested?.Invoke(this, EventArgs.Empty);
        var resetDetailHeight = UiFactory.TextButton(
            _localizer.Text("Settings.ResetDetailHeight"),
            new Rectangle(282, 518, 122, 28));
        resetDetailHeight.Click += (_, _) =>
            _settings.Display.DetailWindowHeightLogical = 700;
        var resetSettingsHeight = UiFactory.TextButton(
            _localizer.Text("Settings.ResetSettingsHeight"),
            new Rectangle(412, 518, 122, 28));
        resetSettingsHeight.Click += (_, _) =>
            _settings.Display.SettingsWindowHeightLogical = 720;
        page.Controls.AddRange(
        [
            resetPosition, resetDetailHeight, resetSettingsHeight,
        ]);
        return page;
    }

    private Panel BuildNotificationsPage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.Notifications")));
        AddToggleRow(
            page,
            _localizer.Text("Settings.Remaining30Notification"),
            _remaining30Notification,
            52);
        AddToggleRow(
            page,
            _localizer.Text("Settings.Remaining10Notification"),
            _remaining10Notification,
            92);
        AddToggleRow(
            page,
            _localizer.Text("Settings.ResetNotifications"),
            _scheduledResetNotification,
            132);
        AddToggleRow(
            page,
            _localizer.Text("Settings.UnexpectedResetNotifications"),
            _unexpectedResetNotification,
            172);
        AddToggleRow(
            page,
            _localizer.Text("Settings.CreditExpiryNotifications"),
            _creditExpiryNotification,
            212);
        AddToggleRow(
            page,
            _localizer.Text("Settings.ConnectionNotifications"),
            _connectionFailureNotification,
            252);
        page.Controls.Add(
            UiFactory.Label(
                _localizer.Text("Settings.NotificationHint"),
                new Point(0, 305),
                8.5F,
                FontStyle.Regular,
                Theme.Muted));
        return page;
    }

    private Panel BuildHistoryPage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.HistoryData")));
        page.Controls.Add(RowLabel(_localizer.Text("Settings.RetentionDays"), 55));
        ConfigureCombo(
            _retention,
            [
                _localizer.Text("Settings.Retention1Year"),
                _localizer.Text("Settings.Retention3Years"),
                _localizer.Text("Settings.Retention5Years"),
                _localizer.Text("Settings.RetentionUnlimited"),
            ]);
        _retention.Bounds = new Rectangle(350, 50, 184, 29);
        page.Controls.Add(_retention);
        page.Controls.Add(
            Hint(
                _localizer.Text("Settings.RetentionInternalCleanup"),
                0,
                102,
                534));
        return page;
    }

    private void AddConnectionControls(Control page, int top)
    {
        var heading = PageTitle(_localizer.Text("Settings.Connection"));
        heading.Location = new Point(0, top);
        heading.Font = Theme.Ui(8.8F, FontStyle.Bold);
        page.Controls.Add(heading);
        ConfigureCombo(_codexPathMode, ["auto", "manual"]);
        _codexPathMode.SelectedIndexChanged += (_, _) =>
            _codexPath.Enabled =
                string.Equals(
                    _codexPathMode.SelectedItem?.ToString(),
                    "manual",
                    StringComparison.Ordinal);
        AddControlRow(
            page,
            _localizer.Text("Settings.CodexPathMode"),
            _codexPathMode,
            top + 28);
        page.Controls.Add(
            RowLabel(
                _localizer.Text("Settings.CodexExecutable"),
                top + 62));
        _codexPath.Bounds = new Rectangle(0, top + 92, 534, 28);
        _codexPath.BackColor = Theme.SurfaceRaised;
        _codexPath.ForeColor = Theme.Text;
        _codexPath.BorderStyle = BorderStyle.FixedSingle;
        page.Controls.Add(_codexPath);
        var diagnose = UiFactory.TextButton(
            _localizer.Text("Settings.ConnectionDiagnostic"),
            new Rectangle(282, top + 128, 252, 28));
        diagnose.Click += (_, _) =>
            ConnectionDiagnosticRequested?.Invoke(this, EventArgs.Empty);
        _connectionStatus.Bounds = new Rectangle(0, top + 164, 534, 32);
        _connectionStatus.Font = Theme.Ui(8.3F);
        _connectionStatus.ForeColor = Theme.Muted;
        _connectionStatus.BackColor = Theme.Window;
        _connectionStatus.Text = _localizer.Text("Settings.ConnectionStatusHint");
        var codexPathHint = Hint(
            _localizer.Text("Settings.CodexPathHint"),
            0,
            top + 200,
            534);
        codexPathHint.Height = 32;
        page.Controls.AddRange(
        [
            diagnose,
            _connectionStatus,
            codexPathHint,
        ]);
    }

    private Panel BuildQuotaResetPage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.QuotaReset")));
        AddToggleRow(
            page,
            _localizer.Text("Settings.StoreQuotaState"),
            _storeQuotaState,
            52);
        AddToggleRow(
            page,
            _localizer.Text("Settings.DetectUnexpectedRecovery"),
            _detectRecovery,
            94);
        AddToggleRow(
            page,
            _localizer.Text("Settings.ConfirmRecovery"),
            _confirmRecovery,
            136);
        page.Controls.Add(
            RowLabel(_localizer.Text("Settings.RecentHistoryCount"), 178));
        _recentHistoryCount.Bounds = new Rectangle(450, 178, 84, 27);
        _recentHistoryCount.Minimum = 1;
        _recentHistoryCount.Maximum = 100;
        _recentHistoryCount.BackColor = Theme.SurfaceRaised;
        _recentHistoryCount.ForeColor = Theme.Text;
        page.Controls.Add(_recentHistoryCount);
        var openHistory = UiFactory.TextButton(
            _localizer.Text("Settings.OpenHistory"),
            new Rectangle(282, 222, 252, 30));
        openHistory.Click += (_, _) =>
            OpenHistoryRequested?.Invoke(this, EventArgs.Empty);
        page.Controls.Add(openHistory);
        page.Controls.Add(
            Hint(
                _localizer.Text("Settings.ResetReadOnlyHint"),
                0,
                275,
                534));
        return page;
    }

    private Panel BuildUsageAcquisitionPage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.UsageAcquisition")));
        AddToggleRow(
            page,
            _localizer.Text("Settings.EnableUsageCollection"),
            _usageEnabled,
            46);
        ConfigureCombo(
            _usageRefreshInterval,
            [
                "1", "5", "15", "30",
                _localizer.Text("Settings.ManualOnly"),
            ]);
        AddControlRow(
            page,
            _localizer.Text("Settings.UsageRefreshInterval"),
            _usageRefreshInterval,
            78);
        AddToggleRow(
            page,
            _localizer.Text("Settings.UsageRefreshWhenOpened"),
            _usageRefreshWhenOpened,
            110);
        AddToggleRow(
            page,
            _localizer.Text("Settings.IncludeArchives"),
            _includeArchives,
            142);
        AddToggleRow(page, _localizer.Text("Settings.CollectModel"), _collectModel, 174);
        AddToggleRow(
            page,
            _localizer.Text("Settings.CollectReasoning"),
            _collectReasoning,
            206);
        AddToggleRow(page, _localizer.Text("Settings.CollectTier"), _collectTier, 238);
        AddToggleRow(page, _localizer.Text("Settings.CollectTokens"), _collectTokens, 270);
        AddToggleRow(
            page,
            _localizer.Text("Settings.CollectElapsed"),
            _collectElapsed,
            302);
        AddToggleRow(page, _localizer.Text("Settings.CollectTurns"), _collectTurns, 334);
        AddToggleRow(
            page,
            _localizer.Text("Settings.CollectTools"),
            _collectTools,
            366);
        AddToggleRow(
            page,
            _localizer.Text("Settings.CollectSkills"),
            _collectSkills,
            398);
        var rescan = UiFactory.TextButton(
            _localizer.Text("Usage.Rescan"),
            new Rectangle(282, 436, 122, 30));
        rescan.Click += (_, _) => UsageRescanRequested?.Invoke(this, EventArgs.Empty);
        var rebuild = UiFactory.TextButton(
            _localizer.Text("Settings.RebuildUsageCache"),
            new Rectangle(412, 436, 122, 30));
        rebuild.Click += (_, _) =>
            UsageCacheRebuildRequested?.Invoke(this, EventArgs.Empty);
        _help.SetToolTip(rescan, _localizer.Text("Help.Rescan"));
        _help.SetToolTip(
            rebuild,
            _localizer.Text("Help.RebuildCache"));
        page.Controls.AddRange([rescan, rebuild]);
        var roots = Hint(
            _localizer.Text("Settings.UsageKnownRoots"),
            0,
            474,
            534);
        roots.Height = 32;
        page.Controls.Add(roots);
        _usageScanStatus.Bounds = new Rectangle(0, 512, 534, 24);
        _usageScanStatus.Font = Theme.Ui(8.3F);
        _usageScanStatus.ForeColor = Theme.Muted;
        _usageScanStatus.BackColor = Theme.Window;
        _usageScanStatus.Text = _localizer.Text("Settings.UsageNotScanned");
        page.Controls.Add(_usageScanStatus);
        page.Controls.Add(
            Hint(
                _localizer.Text("Settings.UsagePrivacyReadOnly"),
                0,
                542,
                534));
        return page;
    }

    private Panel BuildUsageDisplayPage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.UsageDisplay")));
        ConfigureCombo(_usagePeriod, ["current-window", "7-days", "30-days", "90-days"]);
        ConfigureCombo(_usageMetric, ["total-tokens", "elapsed-time", "turn-count"]);
        ConfigureCombo(_chartStyle, ["horizontal-bar"]);
        ConfigureCombo(_sortOrder, ["descending", "ascending", "model"]);
        ConfigureCombo(_numberFormat, ["grouped", "compact"]);
        AddControlRow(page, _localizer.Text("Usage.Period"), _usagePeriod, 46);
        AddControlRow(page, _localizer.Text("Usage.Metric"), _usageMetric, 80);
        AddControlRow(page, _localizer.Text("Settings.ChartStyle"), _chartStyle, 114);
        AddControlRow(page, _localizer.Text("Settings.SortOrder"), _sortOrder, 148);
        AddControlRow(page, _localizer.Text("Settings.NumberFormat"), _numberFormat, 182);
        page.Controls.Add(RowLabel(_localizer.Text("Settings.MaximumModels"), 216));
        _maximumModels.Bounds = new Rectangle(450, 216, 84, 27);
        _maximumModels.Minimum = 1;
        _maximumModels.Maximum = 5;
        _maximumModels.BackColor = Theme.SurfaceRaised;
        _maximumModels.ForeColor = Theme.Text;
        page.Controls.Add(_maximumModels);
        AddToggleRow(page, _localizer.Text("Usage.ElapsedTime"), _showElapsed, 250);
        AddToggleRow(page, _localizer.Text("Usage.TurnCount"), _showTurns, 284);
        AddToggleRow(
            page,
            _localizer.Text("Usage.ReasoningBreakdown"),
            _showReasoning,
            318);
        AddToggleRow(
            page,
            _localizer.Text("Settings.ShowTierBreakdown"),
            _showTier,
            352);
        AddToggleRow(
            page,
            _localizer.Text("Settings.GroupOtherModels"),
            _groupOtherModels,
            386);
        AddToggleRow(
            page,
            _localizer.Text("Settings.ShowAccountUsage"),
            _showAccountUsage,
            420);
        AddToggleRow(
            page,
            _localizer.Text("Settings.ShowActivityBreakdown"),
            _showActivityBreakdown,
            454);
        var estimateHeading = UiFactory.Label(
            _localizer.Text("Settings.UsageEstimates"),
            new Point(0, 490),
            8.8F,
            FontStyle.Bold);
        estimateHeading.Size = new Size(534, 25);
        estimateHeading.AutoSize = false;
        page.Controls.Add(estimateHeading);
        _usageEstimate.Enabled = false;
        AddToggleRow(
            page,
            _localizer.Text("Settings.EnableUsageEstimate"),
            _usageEstimate,
            516);
        page.Controls.Add(
            Hint(
                _localizer.Text("Settings.EstimateUnavailable"),
                0,
                548,
                534));
        return page;
    }

    private void AddBackupControls(Control page, int top)
    {
        var heading = UiFactory.Label(
            _localizer.Text("Settings.Backup"),
            new Point(0, top),
            8.8F,
            FontStyle.Bold);
        heading.Size = new Size(534, 25);
        heading.AutoSize = false;
        var export = UiFactory.TextButton(
            _localizer.Text("Settings.ExportSettings"),
            new Rectangle(0, top + 27, 250, 30));
        export.Click += async (_, _) => await ExportSettingsAsync();
        var import = UiFactory.TextButton(
            _localizer.Text("Settings.ImportSettings"),
            new Rectangle(282, top + 27, 252, 30));
        import.Click += async (_, _) => await ImportSettingsAsync();
        var history = UiFactory.TextButton(
            _localizer.Text("Settings.ExportHistoryJson"),
            new Rectangle(0, top + 63, 250, 30));
        history.Click += (_, _) =>
            HistoryExportRequested?.Invoke(this, EventArgs.Empty);
        var usageJson = UiFactory.TextButton(
            _localizer.Text("Settings.ExportUsageJson"),
            new Rectangle(282, top + 63, 122, 30));
        usageJson.Click += (_, _) =>
            UsageExportRequested?.Invoke(
                this,
                new UsageExportRequestedEventArgs("json"));
        var usageCsv = UiFactory.TextButton(
            _localizer.Text("Settings.ExportUsageCsv"),
            new Rectangle(412, top + 63, 122, 30));
        usageCsv.Click += (_, _) =>
            UsageExportRequested?.Invoke(
                this,
                new UsageExportRequestedEventArgs("csv"));
        page.Controls.AddRange(
        [
            heading, export, import, history, usageJson, usageCsv,
        ]);
        var backupHint = Hint(
            _localizer.Text("Settings.BackupHint"),
            0,
            top + 99,
            534);
        backupHint.Height = 32;
        page.Controls.Add(backupHint);
    }

    private Panel BuildAdvancedPage()
    {
        var page = CreatePage();
        page.Controls.Add(PageTitle(_localizer.Text("Settings.Advanced")));
        AddConnectionControls(page, 38);
        AddBackupControls(page, 276);
        ConfigureCombo(_logRetention, [7, 14, 30, 90]);
        AddControlRow(
            page,
            _localizer.Text("Settings.LogRetention"),
            _logRetention,
            412);
        var logs = UiFactory.TextButton(
            _localizer.Text("Settings.OpenLogs"),
            new Rectangle(0, 446, 166, 30));
        logs.Click += (_, _) => OpenLogsRequested?.Invoke(this, EventArgs.Empty);
        var cache = UiFactory.TextButton(
            _localizer.Text("Settings.ClearLocalCache"),
            new Rectangle(184, 446, 166, 30),
            danger: true);
        cache.Click += (_, _) =>
        {
            if (MessageBox.Show(
                    this,
                    _localizer.Text("Settings.ClearCacheConfirm"),
                    _localizer.Text("Settings.ClearLocalCache"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                ClearCacheRequested?.Invoke(this, EventArgs.Empty);
            }
        };
        var reconnect = UiFactory.TextButton(
            _localizer.Text("Settings.ReconnectAppServer"),
            new Rectangle(368, 446, 166, 30));
        reconnect.Click += (_, _) =>
            ReconnectRequested?.Invoke(this, EventArgs.Empty);
        page.Controls.AddRange([logs, cache, reconnect]);
        var advancedHint = Hint(
            _localizer.Text("Settings.AdvancedHint"),
            0,
            482,
            534);
        advancedHint.Height = 36;
        page.Controls.Add(advancedHint);
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
                $"Version {typeof(SettingsForm).Assembly.GetName().Version?.ToString(3) ?? "0.2.0"}",
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
        var license = UiFactory.TextButton(
            _localizer.Text("Settings.License"),
            new Rectangle(0, 168, 166, 32));
        license.Click += (_, _) =>
            OpenDocumentRequested?.Invoke(
                this,
                new OpenDocumentRequestedEventArgs("LICENSE"));
        var privacy = UiFactory.TextButton(
            _localizer.Text("Settings.Privacy"),
            new Rectangle(184, 168, 166, 32));
        privacy.Click += (_, _) =>
            OpenDocumentRequested?.Invoke(
                this,
                new OpenDocumentRequestedEventArgs("PRIVACY.md"));
        var storage = UiFactory.TextButton(
            _localizer.Text("Settings.OpenStorage"),
            new Rectangle(368, 168, 166, 32));
        storage.Click += (_, _) =>
            OpenStorageRequested?.Invoke(this, EventArgs.Empty);
        page.Controls.AddRange([license, privacy, storage]);
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
        Dock = DockStyle.Fill,
        BackColor = Theme.Window,
        AutoScroll = true,
    };

    private static Label PageTitle(string text)
    {
        var label = UiFactory.Label(text, new Point(0, 2), 9F, FontStyle.Bold);
        label.Size = new Size(534, 25);
        label.AutoSize = false;
        return label;
    }

    private static Label RowLabel(string text, int y)
    {
        var label = UiFactory.Label(text, new Point(0, y), 8.8F);
        label.Size = new Size(340, 25);
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
        toggle.Location = new Point(494, y + 1);
        page.Controls.Add(toggle);
    }

    private static void AddControlRow(
        Control page,
        string label,
        Control control,
        int y)
    {
        page.Controls.Add(RowLabel(label, y));
        control.Bounds = new Rectangle(350, y - 1, 184, 28);
        page.Controls.Add(control);
    }

    private static Label Hint(string text, int x, int y, int width)
    {
        var label = UiFactory.Label(
            text,
            new Point(x, y),
            8.3F,
            FontStyle.Regular,
            Theme.Muted);
        label.AutoSize = false;
        label.Size = new Size(width, 48);
        return label;
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
        _refreshOnOpen.Checked = settings.General.RefreshOnPanelOpen;
        _showCachedOnFailure.Checked = settings.General.ShowCachedOnFailure;
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
        _rememberDetailHeight.Checked =
            settings.Display.RememberDetailHeight;
        _rememberSettingsHeight.Checked =
            settings.Display.RememberSettingsHeight;
        _displayMode.SelectedIndex = _initialDisplayMode.ToLowerInvariant() switch
        {
            "mini" => 0,
            "detail" => 2,
            _ => 1,
        };
        _locale.SelectedIndex = settings.Language.Mode == "auto"
            ? 0
            : string.Equals(
                settings.Language.Locale,
                "ja-JP",
                StringComparison.OrdinalIgnoreCase)
                ? 1
                : 2;
        _retention.SelectedIndex = settings.History.RetentionDays switch
        {
            365 => 0,
            1825 => 2,
            null => 3,
            _ => 1,
        };
        _codexPath.Text = settings.Connection.CodexExecutablePath ?? string.Empty;
        SelectOrFirst(_codexPathMode, settings.Connection.CodexPathMode);
        _remaining30Notification.Checked =
            settings.Notifications.Remaining30;
        _remaining10Notification.Checked =
            settings.Notifications.Remaining10;
        _scheduledResetNotification.Checked =
            settings.Notifications.ScheduledReset;
        _unexpectedResetNotification.Checked =
            settings.Notifications.UnexpectedResetCandidate;
        _creditExpiryNotification.Checked =
            settings.Notifications.ResetCreditExpiring;
        _connectionFailureNotification.Checked =
            settings.Notifications.PersistentConnectionFailure;
        _storeQuotaState.Checked = settings.ResetDetection.StoreQuotaState;
        _detectRecovery.Checked =
            settings.ResetDetection.DetectUnexpectedRecovery;
        _confirmRecovery.Checked = settings.ResetDetection.ConfirmRecovery;
        _recentHistoryCount.Value = Math.Clamp(
            settings.ResetDetection.RecentHistoryCount,
            1,
            100);
        _usageEnabled.Checked = settings.UsageAnalytics.Enabled;
        _usageRefreshInterval.SelectedIndex =
            settings.UsageAnalytics.RefreshIntervalMinutes switch
            {
                1 => 0,
                15 => 2,
                30 => 3,
                0 => 4,
                _ => 1,
            };
        _usageRefreshWhenOpened.Checked =
            settings.UsageAnalytics.RefreshWhenOpened;
        _includeArchives.Checked =
            settings.UsageAnalytics.IncludeArchivedSessions;
        _collectModel.Checked = settings.UsageAnalytics.CollectModel;
        _collectReasoning.Checked =
            settings.UsageAnalytics.CollectReasoningEffort;
        _collectTier.Checked = settings.UsageAnalytics.CollectServiceTier;
        _collectTokens.Checked = settings.UsageAnalytics.CollectTokens;
        _collectElapsed.Checked =
            settings.UsageAnalytics.CollectElapsedTime;
        _collectTurns.Checked = settings.UsageAnalytics.CollectTurnCount;
        _collectTools.Checked = settings.UsageAnalytics.CollectToolUsage;
        _collectSkills.Checked = settings.UsageAnalytics.CollectSkillUsage;
        SelectOrFirst(_usagePeriod, settings.UsageAnalytics.DefaultPeriod);
        SelectOrFirst(_usageMetric, settings.UsageAnalytics.DefaultMetric);
        SelectOrFirst(_chartStyle, settings.UsageAnalytics.ChartStyle);
        SelectOrFirst(_sortOrder, settings.UsageAnalytics.SortOrder);
        SelectOrFirst(_numberFormat, settings.UsageAnalytics.NumberFormat);
        _maximumModels.Value = Math.Clamp(
            settings.UsageAnalytics.MaxIndividualModels,
            1,
            5);
        _showElapsed.Checked = settings.UsageAnalytics.ShowElapsedTime;
        _showTurns.Checked = settings.UsageAnalytics.ShowTurnCount;
        _showReasoning.Checked =
            settings.UsageAnalytics.ShowReasoningBreakdown;
        _showTier.Checked =
            settings.UsageAnalytics.ShowServiceTierBreakdown;
        _groupOtherModels.Checked =
            settings.UsageAnalytics.GroupOtherModels;
        _usageEstimate.Checked =
            settings.UsageAnalytics.ShowEstimatedConsumption;
        _showAccountUsage.Checked = settings.UsageAnalytics.ShowAccountUsage;
        _showActivityBreakdown.Checked =
            settings.UsageAnalytics.ShowToolAndSkillBreakdown;
        SelectOrFirst(_logRetention, settings.Diagnostics.LogRetentionDays);
        _loadingControls = false;
    }

    private void ConfigureHelp()
    {
        _help.SetToolTip(_locale, _localizer.Text("Help.Language"));
        _help.SetToolTip(_opacity, _localizer.Text("Help.Opacity"));
        _help.SetToolTip(_alwaysOnTop, _localizer.Text("Help.AlwaysOnTop"));
        _help.SetToolTip(_lockPosition, _localizer.Text("Help.LockPosition"));
        _help.SetToolTip(
            _rememberPosition,
            _localizer.Text("Help.RememberPosition"));
        _help.SetToolTip(_snapToEdge, _localizer.Text("Help.SnapToEdge"));
        _help.SetToolTip(
            _miniClickThrough,
            _localizer.Text("Help.MiniClickThrough"));
        _help.SetToolTip(_displayMode, _localizer.Text("Help.DisplayMode"));
        _help.SetToolTip(
            _usageEnabled,
            _localizer.Text("Help.UsageEnabled"));
        _help.SetToolTip(
            _usageRefreshInterval,
            _localizer.Text("Help.UsageInterval"));
        _help.SetToolTip(
            _usageRefreshWhenOpened,
            _localizer.Text("Help.UsageOnOpen"));
        _help.SetToolTip(
            _includeArchives,
            _localizer.Text("Help.IncludeArchives"));
        _help.SetToolTip(
            _usagePeriod,
            _localizer.Text("Help.UsagePeriodDefault"));
        _help.SetToolTip(
            _usageMetric,
            _localizer.Text("Help.UsageMetricDefault"));
        _help.SetToolTip(
            _groupOtherModels,
            _localizer.Text("Help.GroupOther"));
    }

    private void ApplyControls()
    {
        _settings.General.LaunchAtStartup = _launchAtStartup.Checked;
        _settings.General.RefreshOnPanelOpen = _refreshOnOpen.Checked;
        _settings.General.ShowCachedOnFailure = _showCachedOnFailure.Checked;
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
        _settings.Display.RememberDetailHeight =
            _rememberDetailHeight.Checked;
        _settings.Display.RememberSettingsHeight =
            _rememberSettingsHeight.Checked;
        _settings.Language.Mode = _locale.SelectedIndex == 0
            ? "auto"
            : "manual";
        if (_locale.SelectedIndex != 0)
        {
            _settings.Language.Locale = _locale.SelectedIndex == 1
                ? "ja-JP"
                : "en-US";
        }
        _settings.History.RetentionDays = _retention.SelectedIndex switch
        {
            0 => 365,
            2 => 1825,
            3 => null,
            _ => 1095,
        };
        _settings.Connection.CodexExecutablePath =
            string.IsNullOrWhiteSpace(_codexPath.Text) ? null : _codexPath.Text.Trim();
        _settings.Connection.CodexPathMode =
            _codexPathMode.SelectedItem?.ToString() ?? "auto";
        if (_settings.Connection.CodexPathMode == "auto")
        {
            _settings.Connection.CodexExecutablePath = null;
        }
        _settings.Notifications.Remaining30 =
            _remaining30Notification.Checked;
        _settings.Notifications.Remaining10 =
            _remaining10Notification.Checked;
        _settings.Notifications.ScheduledReset =
            _scheduledResetNotification.Checked;
        _settings.Notifications.UnexpectedResetCandidate =
            _unexpectedResetNotification.Checked;
        _settings.Notifications.ResetCreditExpiring =
            _creditExpiryNotification.Checked;
        _settings.Notifications.PersistentConnectionFailure =
            _connectionFailureNotification.Checked;
        _settings.ResetDetection.StoreQuotaState = _storeQuotaState.Checked;
        _settings.ResetDetection.DetectUnexpectedRecovery =
            _detectRecovery.Checked;
        _settings.ResetDetection.ConfirmRecovery = _confirmRecovery.Checked;
        _settings.ResetDetection.RecentHistoryCount =
            (int)_recentHistoryCount.Value;
        _settings.UsageAnalytics.Enabled = _usageEnabled.Checked;
        _settings.UsageAnalytics.RefreshIntervalMinutes =
            _usageRefreshInterval.SelectedIndex switch
            {
                0 => 1,
                2 => 15,
                3 => 30,
                4 => 0,
                _ => 5,
            };
        _settings.UsageAnalytics.RefreshWhenOpened =
            _usageRefreshWhenOpened.Checked;
        _settings.UsageAnalytics.IncludeArchivedSessions =
            _includeArchives.Checked;
        _settings.UsageAnalytics.CollectModel = _collectModel.Checked;
        _settings.UsageAnalytics.CollectReasoningEffort =
            _collectReasoning.Checked;
        _settings.UsageAnalytics.CollectServiceTier = _collectTier.Checked;
        _settings.UsageAnalytics.CollectTokens = _collectTokens.Checked;
        _settings.UsageAnalytics.CollectElapsedTime = _collectElapsed.Checked;
        _settings.UsageAnalytics.CollectTurnCount = _collectTurns.Checked;
        _settings.UsageAnalytics.CollectToolUsage = _collectTools.Checked;
        _settings.UsageAnalytics.CollectSkillUsage = _collectSkills.Checked;
        _settings.UsageAnalytics.DefaultPeriod =
            _usagePeriod.SelectedItem?.ToString() ?? "current-window";
        _settings.UsageAnalytics.DefaultMetric =
            _usageMetric.SelectedItem?.ToString() ?? "total-tokens";
        _settings.UsageAnalytics.ChartStyle =
            _chartStyle.SelectedItem?.ToString() ?? "horizontal-bar";
        _settings.UsageAnalytics.SortOrder =
            _sortOrder.SelectedItem?.ToString() ?? "descending";
        _settings.UsageAnalytics.NumberFormat =
            _numberFormat.SelectedItem?.ToString() ?? "grouped";
        _settings.UsageAnalytics.MaxIndividualModels =
            (int)_maximumModels.Value;
        _settings.UsageAnalytics.ShowElapsedTime = _showElapsed.Checked;
        _settings.UsageAnalytics.ShowTurnCount = _showTurns.Checked;
        _settings.UsageAnalytics.ShowReasoningBreakdown =
            _showReasoning.Checked;
        _settings.UsageAnalytics.ShowServiceTierBreakdown = _showTier.Checked;
        _settings.UsageAnalytics.GroupOtherModels =
            _groupOtherModels.Checked;
        _settings.UsageAnalytics.ShowEstimatedConsumption =
            _usageEstimate.Enabled && _usageEstimate.Checked;
        _settings.UsageAnalytics.ShowAccountUsage =
            _showAccountUsage.Checked;
        _settings.UsageAnalytics.ShowToolAndSkillBreakdown =
            _showActivityBreakdown.Checked;
        _settings.Diagnostics.LogRetentionDays =
            (int?)_logRetention.SelectedItem ?? 14;
    }

    private void ResetAllSettings()
    {
        var defaults = new AppSettings();
        _settings.CopyFrom(defaults);
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

    protected override void OnFormClosed(FormClosedEventArgs eventArgs)
    {
        if (!_saved)
        {
            _settings.CopyFrom(_originalSettings);
            SettingsPreviewChanged?.Invoke(
                this,
                new SettingsPreviewChangedEventArgs(
                    SettingsPreviewKind.Appearance));
        }
        base.OnFormClosed(eventArgs);
    }

    private async Task ExportSettingsAsync()
    {
        ApplyControls();
        using var dialog = new SaveFileDialog
        {
            Title = _localizer.Text("Settings.ExportSettings"),
            Filter = "JSON (*.json)|*.json",
            FileName = "QuantaTray-settings.json",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        var json = JsonSerializer.Serialize(
            _settings,
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(dialog.FileName, json);
    }

    private async Task ImportSettingsAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = _localizer.Text("Settings.ImportSettings"),
            Filter = "JSON (*.json)|*.json",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            var json = await File.ReadAllTextAsync(dialog.FileName);
            var imported = JsonSerializer.Deserialize<AppSettings>(json);
            if (imported is null)
            {
                throw new JsonException("The settings file is empty.");
            }
            _settings.CopyFrom(SettingsMigration.Upgrade(imported));
            LoadControls(_settings);
            NotifyPreviewChanged(SettingsPreviewKind.Appearance);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            JsonException)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "QuantaTray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static void SelectOrFirst(ComboBox combo, object value)
    {
        combo.SelectedItem = value;
        if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _help.Dispose();
        }
        base.Dispose(disposing);
    }

}
