using System.Diagnostics;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.App;

internal sealed class QuantaTrainContext : ApplicationContext
{
    private static readonly string ProductVersion =
        typeof(QuantaTrainContext).Assembly.GetName().Version?.ToString(3) ?? "0.1.3";

    private static readonly TimeSpan[] RestartBackoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(15),
    ];

    private readonly SingleInstanceCoordinator _singleInstance;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly SemaphoreSlim _settingsPreviewGate = new(1, 1);
    private readonly Control _dispatcher = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _activationTimer;
    private readonly DataPaths _paths;
    private readonly JsonSettingsStore _settingsStore;
    private readonly JsonlHistoryStore _historyStore;
    private readonly RedactedLogger _logger;
    private readonly LocalizationService _localizer;
    private readonly AppSettings _settings;
    private PanelPositionSettings _panelPosition;
    private MiniForm? _miniForm;
    private CompactForm? _compactForm;
    private DetailForm? _detailForm;
    private JsonRpcConnection? _connection;
    private CodexAccountClient? _accountClient;
    private PollingCoordinator? _polling;
    private CodexInstallation? _codex;
    private WeeklyQuotaState? _previousState;
    private IReadOnlyList<string> _historyItems = [];
    private bool _confirmationPending;
    private SettingsForm? _settingsForm;
    private int _restartAttempt;
    private bool _exiting;

    public QuantaTrainContext(SingleInstanceCoordinator singleInstance)
    {
        _singleInstance = singleInstance;
        _ = _dispatcher.Handle;
        _paths = DataPathResolver.Resolve(AppContext.BaseDirectory);
        _settingsStore = new JsonSettingsStore(_paths.SettingsFile);
        _settings = _settingsStore.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        _panelPosition = _settings.Display.RememberPosition
            ? PanelPlacement.Clone(_settings.Display.PanelPosition)
            : new PanelPositionSettings();
        _historyStore = new JsonlHistoryStore(_paths.HistoryDirectory);
        _historyStore.Prune(_settings.History.RetentionDays);
        _logger = new RedactedLogger(_paths.LogsDirectory);
        _localizer = new LocalizationService(Path.Combine(AppContext.BaseDirectory, "locales"));
        _localizer.Load(_settings.Language);
        Theme.Configure(_settings.Display.Theme, _settings.Display.Accent);

        _notifyIcon = new NotifyIcon
        {
            Icon = IconFactory.Create(null),
            Text = "QuantaTray",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _notifyIcon.MouseClick += HandleTrayClick;
        _notifyIcon.MouseDoubleClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                ShowDetail();
            }
        };

        _activationTimer = new System.Windows.Forms.Timer { Interval = 500, Enabled = true };
        _activationTimer.Tick += (_, _) =>
        {
            if (_singleInstance.ConsumeShutdownRequest())
            {
                ExitThread();
                return;
            }
            if (_singleInstance.ConsumeActivationRequest())
            {
                ShowCompact();
            }
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged +=
            HandleDisplaySettingsChanged;
        _ = InitializeAsync();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(
            _localizer.Text("Menu.ShowMini"),
            null,
            (_, _) => ShowMini());
        menu.Items.Add(
            _localizer.Text("Menu.ShowCompact"),
            null,
            (_, _) => ShowCompact());
        menu.Items.Add(
            _localizer.Text("Menu.ShowDetail"),
            null,
            (_, _) => ShowDetail());
        menu.Items.Add(
            _localizer.Text("Common.Refresh"),
            null,
            async (_, _) => await RefreshAsync());
        menu.Items.Add(new ToolStripSeparator());

        var topMost = new ToolStripMenuItem(_localizer.Text("Menu.AlwaysOnTop"))
        {
            CheckOnClick = true,
            Checked = _settings.Display.AlwaysOnTop,
        };
        topMost.CheckedChanged += async (_, _) =>
        {
            _settings.Display.AlwaysOnTop = topMost.Checked;
            ApplyDisplaySettings();
            await SaveSettingsAsync();
        };
        menu.Items.Add(topMost);

        var lockPosition = new ToolStripMenuItem(_localizer.Text("Menu.LockPosition"))
        {
            CheckOnClick = true,
            Checked = _settings.Display.LockPosition,
        };
        lockPosition.CheckedChanged += async (_, _) =>
        {
            _settings.Display.LockPosition = lockPosition.Checked;
            await SaveSettingsAsync();
        };
        menu.Items.Add(lockPosition);

        var miniClickThrough = new ToolStripMenuItem(
            _localizer.Text("Settings.MiniClickThrough"))
        {
            CheckOnClick = true,
            Checked = _settings.Display.MiniClickThrough,
        };
        miniClickThrough.CheckedChanged += async (_, _) =>
        {
            _settings.Display.MiniClickThrough = miniClickThrough.Checked;
            ApplyDisplaySettings();
            await SaveSettingsAsync();
        };
        menu.Items.Add(miniClickThrough);
        menu.Items.Add(
            _localizer.Text("Menu.ResetPosition"),
            null,
            async (_, _) => await ResetPanelPositionAsync());
        menu.Items.Add(
            _localizer.Text("Common.Settings"),
            null,
            (_, _) => QueueShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(
            _localizer.Text("Menu.OpenCodex"),
            null,
            (_, _) => OpenUrl("https://chatgpt.com/codex"));
        menu.Items.Add(
            _localizer.Text("Menu.OpenChatGPT"),
            null,
            (_, _) => OpenUrl("https://chatgpt.com/"));
        menu.Items.Add(
            _localizer.Text("Settings.About"),
            null,
            (_, _) =>
            {
                using var form = new AboutForm(_localizer, ProductVersion);
                form.ShowDialog();
            });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(
            _localizer.Text("Common.Exit"),
            null,
            (_, _) => ExitThread());
        return menu;
    }

    private async Task InitializeAsync()
    {
        try
        {
            _historyItems = await _historyStore.ReadRecentAsync(5, _lifetime.Token);
            var background = Environment.GetCommandLineArgs()
                .Any(argument => string.Equals(
                    argument,
                    "--background",
                    StringComparison.OrdinalIgnoreCase));
            if (!background)
            {
                switch (_settings.General.StartupMode)
                {
                    case "mini":
                        ShowMini();
                        break;
                    case "compact":
                        ShowCompact();
                        break;
                    case "detail":
                        ShowDetail();
                        break;
                }
            }
            UpdateViews(null, true, null);
            await ConnectAsync(_lifetime.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await _logger.WarningAsync(exception.Message, CancellationToken.None);
            UpdateViews(null, false, exception.Message);
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not null && _accountClient is not null)
            {
                var existingAccount = await _accountClient.ReadAccountAsync(cancellationToken);
                SetSignedIn(existingAccount.IsSignedIn);
                if (existingAccount.IsSignedIn)
                {
                    await StartPollingAsync(cancellationToken);
                }
                else
                {
                    UpdateViews(null, false, null);
                }
                return;
            }

            _codex = await CodexLocator.LocateAsync(
                _settings.Connection.CodexExecutablePath,
                cancellationToken);
            if (_codex is null)
            {
                SetSignedIn(false);
                UpdateViews(null, false, _localizer.Text("Connection.CodexNotFound"));
                _notifyIcon.ShowBalloonTip(
                    5000,
                    "QuantaTray",
                    _localizer.Text("Connection.CodexNotFound"),
                    ToolTipIcon.Warning);
                return;
            }

            _connection = await JsonRpcConnection.StartAsync(
                _codex.ExecutablePath,
                ProductVersion,
                cancellationToken);
            _connection.Exited += HandleConnectionExited;
            _accountClient = new CodexAccountClient(_connection, _codex.Version);
            _accountClient.LoginCompleted += HandleLoginCompleted;
            _accountClient.AccountUpdated += HandleAccountUpdated;

            var account = await _accountClient.ReadAccountAsync(cancellationToken);
            SetSignedIn(account.IsSignedIn);
            if (account.IsSignedIn)
            {
                await StartPollingAsync(cancellationToken);
            }
            else
            {
                UpdateViews(null, false, null);
            }
            _restartAttempt = 0;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private async Task StartPollingAsync(CancellationToken cancellationToken)
    {
        if (_accountClient is null || _polling is not null)
        {
            return;
        }

        _polling = new PollingCoordinator(
            _accountClient,
            TimeSpan.FromSeconds(_settings.General.RefreshIntervalSeconds));
        _polling.StateChanged += HandlePollingStateChanged;
        _polling.Start();
        await _polling.RefreshAsync(cancellationToken);
    }

    private void HandlePollingStateChanged(
        object? sender,
        PollingStateChangedEventArgs eventArgs)
    {
        PostToUi(async () =>
        {
            UpdateViews(eventArgs.State, eventArgs.IsUpdating, eventArgs.Error);
            if (!eventArgs.IsUpdating &&
                eventArgs.Error is null &&
                eventArgs.State is not null)
            {
                var before = _previousState;
                _previousState = eventArgs.State;
                if (before is not null &&
                    ResetClassifier.Classify(before, eventArgs.State, confirmed: false) is not null &&
                    !_confirmationPending)
                {
                    await ConfirmResetAsync(before);
                }
            }
        });
    }

    private async Task ConfirmResetAsync(WeeklyQuotaState before)
    {
        if (_polling is null)
        {
            return;
        }

        _confirmationPending = true;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), _lifetime.Token);
            var confirmedState = await _polling.RefreshAsync(_lifetime.Token);
            if (confirmedState is null)
            {
                return;
            }

            var resetEvent = ResetClassifier.Classify(before, confirmedState, confirmed: true);
            if (resetEvent is null)
            {
                return;
            }

            await _historyStore.AppendAsync(resetEvent, _lifetime.Token);
            _historyItems = await _historyStore.ReadRecentAsync(5, _lifetime.Token);
            UpdateViews(confirmedState, false, null);
            ShowResetNotification(resetEvent);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            _confirmationPending = false;
        }
    }

    private void ShowResetNotification(ResetEvent resetEvent)
    {
        var enabled = resetEvent.Classification switch
        {
            ResetClassification.ScheduledReset => _settings.Notifications.ScheduledReset,
            ResetClassification.UnexpectedResetCandidate =>
                _settings.Notifications.UnexpectedResetCandidate,
            _ => false,
        };
        if (enabled)
        {
            _notifyIcon.ShowBalloonTip(
                5000,
                "QuantaTray",
                resetEvent.Classification.ToString(),
                ToolTipIcon.Info);
        }
    }

    private void HandleLoginCompleted(object? sender, bool success)
    {
        if (success)
        {
            PostToUi(async () =>
            {
                SetSignedIn(true);
                await StartPollingAsync(_lifetime.Token);
            });
        }
    }

    private void HandleAccountUpdated(object? sender, EventArgs eventArgs)
    {
        PostToUi(async () =>
        {
            if (_accountClient is null)
            {
                return;
            }

            var status = await _accountClient.ReadAccountAsync(_lifetime.Token);
            SetSignedIn(status.IsSignedIn);
            if (status.IsSignedIn)
            {
                await StartPollingAsync(_lifetime.Token);
            }
        });
    }

    private void HandleConnectionExited(object? sender, EventArgs eventArgs)
    {
        PostToUi(async () =>
        {
            UpdateViews(_polling?.Current, false, "Codex App Server exited.");
            if (_restartAttempt >= RestartBackoff.Length || _exiting)
            {
                return;
            }

            var delay = RestartBackoff[_restartAttempt++];
            await DisposeConnectionAsync();
            try
            {
                await Task.Delay(delay, _lifetime.Token);
                await ConnectAsync(_lifetime.Token);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                await _logger.WarningAsync(exception.Message, CancellationToken.None);
            }
        });
    }

    private void HandleTrayClick(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button == MouseButtons.Left)
        {
            ShowCompact();
            if (_settings.General.RefreshOnPanelOpen)
            {
                _ = RefreshAsync();
            }
        }
    }

    private CompactForm GetCompactForm()
    {
        if (_compactForm is null || _compactForm.IsDisposed)
        {
            _compactForm = new CompactForm(
                _localizer,
                () => !_settings.Display.LockPosition);
            _compactForm.RefreshRequested += async (_, _) => await RefreshAsync();
            _compactForm.MiniRequested += (_, _) => ShowMini();
            _compactForm.DetailRequested += (_, _) => ShowDetail();
            _compactForm.SettingsRequested += (_, _) => QueueShowSettings();
            _compactForm.SignInRequested += async (_, _) => await StartLoginAsync();
            _compactForm.MoveCompleted += async (_, _) =>
                await HandlePanelMoveCompletedAsync(_compactForm);
            ApplyDisplaySettings();
        }
        return _compactForm;
    }

    private MiniForm GetMiniForm()
    {
        if (_miniForm is null || _miniForm.IsDisposed)
        {
            _miniForm = new MiniForm(
                _localizer,
                () => !_settings.Display.LockPosition);
            _miniForm.CompactRequested += (_, _) => ShowCompact();
            _miniForm.DetailRequested += (_, _) => ShowDetail();
            _miniForm.RefreshRequested += async (_, _) => await RefreshAsync();
            _miniForm.SettingsRequested += (_, _) => QueueShowSettings();
            _miniForm.ClickThroughRequested += async (_, eventArgs) =>
            {
                _settings.Display.MiniClickThrough = eventArgs.Enabled;
                ApplyDisplaySettings();
                RebuildTrayMenu();
                await SaveSettingsAsync();
            };
            _miniForm.MoveCompleted += async (_, _) =>
                await HandlePanelMoveCompletedAsync(_miniForm);
            ApplyDisplaySettings();
        }
        return _miniForm;
    }

    private DetailForm GetDetailForm()
    {
        if (_detailForm is null || _detailForm.IsDisposed)
        {
            _detailForm = new DetailForm(
                _localizer,
                () => !_settings.Display.LockPosition);
            _detailForm.RefreshRequested += async (_, _) => await RefreshAsync();
            _detailForm.CompactRequested += (_, _) => ShowCompact();
            _detailForm.SettingsRequested += (_, _) => QueueShowSettings();
            _detailForm.MoveCompleted += async (_, _) =>
                await HandlePanelMoveCompletedAsync(_detailForm);
            ApplyDisplaySettings();
        }
        return _detailForm;
    }

    private void ShowCompact(bool activate = true)
    {
        CaptureVisiblePanelPosition();
        _miniForm?.Hide();
        _detailForm?.Hide();
        var form = GetCompactForm();
        PositionPanel(form);
        form.Show();
        if (activate)
        {
            form.Activate();
        }
    }

    private void ShowDetail(bool activate = true)
    {
        CaptureVisiblePanelPosition();
        _miniForm?.Hide();
        _compactForm?.Hide();
        var form = GetDetailForm();
        PositionPanel(form);
        form.Show();
        if (activate)
        {
            form.Activate();
        }
        if (_settings.General.RefreshOnPanelOpen)
        {
            _ = RefreshAsync();
        }
    }

    private void ShowMini(bool activate = true)
    {
        CaptureVisiblePanelPosition();
        _compactForm?.Hide();
        _detailForm?.Hide();
        var form = GetMiniForm();
        PositionPanel(form);
        form.Show();
        if (_settings.Display.MiniClickThrough)
        {
            if (activate || _settingsForm is null)
            {
                form.EnsureVisibleWithoutActivation(
                    _settings.Display.AlwaysOnTop);
            }
        }
        else if (activate)
        {
            form.Activate();
        }
        if (_settings.General.RefreshOnPanelOpen)
        {
            _ = RefreshAsync();
        }
    }

    private void ShowSettings()
    {
        if (_settingsForm is not null && !_settingsForm.IsDisposed)
        {
            _settingsForm.Show();
            _settingsForm.Activate();
            return;
        }

        var displayMode = _miniForm?.Visible == true
            ? "mini"
            : _detailForm?.Visible == true
                ? "detail"
                : "compact";
        var form = new SettingsForm(
            _settings,
            _localizer,
            initialPage: 1,
            initialDisplayMode: displayMode);
        _settingsForm = form;
        form.FormClosed += (_, _) =>
        {
            var restoreClickThroughMini =
                _miniForm?.Visible == true && _settings.Display.MiniClickThrough;
            if (ReferenceEquals(_settingsForm, form))
            {
                _settingsForm = null;
            }
            if (restoreClickThroughMini)
            {
                _miniForm?.EnsureVisibleWithoutActivation(
                    _settings.Display.AlwaysOnTop);
            }
        };
        form.PositionResetRequested += async (_, _) =>
            await ResetPanelPositionAsync();
        form.AllSettingsResetRequested += async (_, _) =>
        {
            _panelPosition = new PanelPositionSettings();
            await ResetPanelPositionAsync();
        };
        form.SettingsPreviewChanged += async (_, eventArgs) =>
        {
            try
            {
                if (eventArgs.Kind == SettingsPreviewKind.DisplayMode)
                {
                    if (form.DisplayMode == "mini")
                    {
                        ShowMini(activate: false);
                    }
                    else if (form.DisplayMode == "detail")
                    {
                        ShowDetail(activate: false);
                    }
                    else
                    {
                        ShowCompact(activate: false);
                    }
                    return;
                }
                await ApplySettingsPreviewAsync(eventArgs.Kind);
            }
            catch (Exception exception)
            {
                await _logger.WarningAsync(
                    exception.Message,
                    CancellationToken.None);
            }
        };
        form.SettingsSaved += async (_, _) =>
        {
            try
            {
                await _settingsPreviewGate.WaitAsync(_lifetime.Token);
                try
                {
                    Theme.Configure(
                        _settings.Display.Theme,
                        _settings.Display.Accent);
                    _localizer.Load(_settings.Language);
                    ApplyDisplaySettings();
                    ApplyPanelBehavior();
                    await SaveSettingsAsync();
                }
                finally
                {
                    _settingsPreviewGate.Release();
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                await _logger.WarningAsync(
                    exception.Message,
                    CancellationToken.None);
            }
        };
        form.Show();
        form.Activate();
    }

    private async Task ApplySettingsPreviewAsync(SettingsPreviewKind kind)
    {
        await _settingsPreviewGate.WaitAsync(_lifetime.Token);
        try
        {
            if (kind is SettingsPreviewKind.Opacity
                or SettingsPreviewKind.DisplayBehavior)
            {
                ApplyDisplaySettings();
                ApplyPanelBehavior();
                if (kind == SettingsPreviewKind.DisplayBehavior)
                {
                    RebuildTrayMenu();
                }
                await SaveSettingsAsync();
                return;
            }

            Theme.Configure(_settings.Display.Theme, _settings.Display.Accent);
            _localizer.Load(_settings.Language);
            var oldMenu = _notifyIcon.ContextMenuStrip;
            _notifyIcon.ContextMenuStrip = BuildMenu();
            oldMenu?.Dispose();
            RebuildViews();
            ApplyDisplaySettings();
            UpdateViews(_polling?.Current, false, null);
            await SaveSettingsAsync();
        }
        finally
        {
            _settingsPreviewGate.Release();
        }
    }

    private void QueueShowSettings()
    {
        PostToUi(() =>
        {
            ShowSettings();
            return Task.CompletedTask;
        });
    }

    private void RebuildTrayMenu()
    {
        var oldMenu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = BuildMenu();
        oldMenu?.Dispose();
    }

    private async Task StartLoginAsync()
    {
        if (_accountClient is null)
        {
            OpenUrl("https://github.com/openai/codex");
            return;
        }

        try
        {
            var login = await _accountClient.StartChatGptLoginAsync(_lifetime.Token);
            OpenUrl(login.AuthorizationUri.AbsoluteUri);
        }
        catch (Exception exception)
        {
            await _logger.WarningAsync(exception.Message, CancellationToken.None);
            MessageBox.Show(
                Redaction.Redact(exception.Message),
                "QuantaTray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            if (_polling is null)
            {
                await ConnectAsync(_lifetime.Token);
                return;
            }

            await _polling.RefreshAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await _logger.WarningAsync(exception.Message, CancellationToken.None);
            UpdateViews(
                _polling?.Current,
                false,
                Redaction.Redact(exception.Message));
        }
    }

    private void SetSignedIn(bool signedIn)
    {
        _compactForm?.SetSignedIn(signedIn);
    }

    private void UpdateViews(WeeklyQuotaState? state, bool updating, string? error)
    {
        _miniForm?.UpdateState(state);
        _compactForm?.UpdateState(state, updating, error);
        _detailForm?.UpdateState(state, updating, error, _historyItems);

        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = IconFactory.Create(state?.RemainingPercent);
        oldIcon?.Dispose();
        var remainingText = state is null
            ? _localizer.Text("Quota.Unavailable")
            : _localizer.Text(
                "Common.Remaining",
                QuotaDisplay.Number(state.RemainingPercent));
        _notifyIcon.Text = remainingText.Length <= 63
            ? remainingText
            : remainingText[..63];
    }

    private void ApplyDisplaySettings()
    {
        foreach (var form in new Form?[] { _miniForm, _compactForm, _detailForm })
        {
            if (form is null || form.IsDisposed)
            {
                continue;
            }
            form.TopMost = _settings.Display.AlwaysOnTop;
            form.Opacity = _settings.Display.OpacityPercent / 100d;
        }
        if (_miniForm is not null && !_miniForm.IsDisposed)
        {
            _miniForm.SetClickThrough(_settings.Display.MiniClickThrough);
        }
    }

    private void ApplyPanelBehavior()
    {
        var form = VisiblePanel();
        if (form is null || form.IsDisposed)
        {
            return;
        }
        if (_settings.Display.SnapToEdge)
        {
            PanelPlacement.SnapToEdge(form);
        }
        PanelPlacement.Capture(form, _panelPosition);
        if (_settings.Display.RememberPosition)
        {
            _settings.Display.PanelPosition =
                PanelPlacement.Clone(_panelPosition);
        }
    }

    private void PositionPanel(Form form)
    {
        var previousMonitor = _panelPosition.MonitorDeviceName;
        if (PanelPlacement.TryRestore(form, _panelPosition))
        {
            PanelPlacement.Capture(form, _panelPosition);
            PersistPanelPosition();
            if (_settings.Display.RememberPosition
                && !string.Equals(
                    previousMonitor,
                    _panelPosition.MonitorDeviceName,
                    StringComparison.OrdinalIgnoreCase))
            {
                _ = SaveSettingsAsync();
            }
            return;
        }

        if (form is MiniForm mini)
        {
            mini.PositionNearTray();
        }
        else if (form is CompactForm compact)
        {
            compact.PositionNearTray();
        }
        else if (form is DetailForm detail)
        {
            detail.PositionNearTray();
        }
        PanelPlacement.Capture(form, _panelPosition);
        PersistPanelPosition();
    }

    private async Task HandlePanelMoveCompletedAsync(Form? form)
    {
        if (form is null || form.IsDisposed)
        {
            return;
        }
        if (_settings.Display.SnapToEdge)
        {
            PanelPlacement.SnapToEdge(form);
        }
        PanelPlacement.Capture(form, _panelPosition);
        if (_settings.Display.RememberPosition)
        {
            _settings.Display.PanelPosition =
                PanelPlacement.Clone(_panelPosition);
            await SaveSettingsAsync();
        }
    }

    private void RebuildViews()
    {
        var displayMode = _miniForm?.Visible == true
            ? "mini"
            : _compactForm?.Visible == true
                ? "compact"
                : _detailForm?.Visible == true
                    ? "detail"
                    : null;
        CaptureVisiblePanelPosition();

        _miniForm?.Hide();
        _compactForm?.Hide();
        _detailForm?.Hide();
        _miniForm?.Dispose();
        _compactForm?.Dispose();
        _detailForm?.Dispose();
        _miniForm = null;
        _compactForm = null;
        _detailForm = null;

        switch (displayMode)
        {
            case "mini":
                ShowMini(activate: false);
                break;
            case "compact":
                ShowCompact(activate: false);
                break;
            case "detail":
                ShowDetail(activate: false);
                break;
        }
    }

    private Form? VisiblePanel() =>
        _miniForm?.Visible == true
            ? _miniForm
            : _compactForm?.Visible == true
                ? _compactForm
                : _detailForm?.Visible == true
                    ? _detailForm
                    : null;

    private void CaptureVisiblePanelPosition()
    {
        var form = VisiblePanel();
        if (form is null || form.IsDisposed)
        {
            return;
        }

        PanelPlacement.Capture(form, _panelPosition);
        PersistPanelPosition();
    }

    private void PersistPanelPosition()
    {
        if (_settings.Display.RememberPosition)
        {
            _settings.Display.PanelPosition =
                PanelPlacement.Clone(_panelPosition);
        }
    }

    private async Task ResetPanelPositionAsync()
    {
        _panelPosition = new PanelPositionSettings();
        _settings.Display.PanelPosition = new PanelPositionSettings();
        _miniForm?.Hide();
        _detailForm?.Hide();
        var compact = GetCompactForm();
        PanelPlacement.CenterOnPrimary(compact);
        PanelPlacement.Capture(compact, _panelPosition);
        PersistPanelPosition();
        compact.Show();
        compact.Activate();
        await SaveSettingsAsync();
    }

    private void HandleDisplaySettingsChanged(object? sender, EventArgs eventArgs)
    {
        PostToUi(EnsureVisiblePanelReachableAsync);
    }

    private async Task EnsureVisiblePanelReachableAsync()
    {
        var form = VisiblePanel();
        if (form is null || form.IsDisposed || PanelPlacement.IsReachable(form))
        {
            return;
        }

        PanelPlacement.CenterOnPrimary(form);
        PanelPlacement.Capture(form, _panelPosition);
        PersistPanelPosition();
        await SaveSettingsAsync();
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsStore.SaveAsync(_settings, _lifetime.Token);
            StartupRegistration.SetEnabled(
                _settings.General.LaunchAtStartup,
                Environment.ProcessPath
                    ?? Path.Combine(AppContext.BaseDirectory, "QuantaTray.exe"));
        }
        catch (Exception exception)
        {
            await _logger.WarningAsync(exception.Message, CancellationToken.None);
        }
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void PostToUi(Func<Task> action)
    {
        if (_dispatcher.IsDisposed || !_dispatcher.IsHandleCreated || _exiting)
        {
            return;
        }

        _dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                await _logger.WarningAsync(
                    exception.Message,
                    CancellationToken.None);
            }
        });
    }

    private async Task DisposeConnectionAsync()
    {
        if (_polling is not null)
        {
            _polling.StateChanged -= HandlePollingStateChanged;
            await _polling.DisposeAsync().ConfigureAwait(false);
            _polling = null;
        }
        if (_accountClient is not null)
        {
            _accountClient.LoginCompleted -= HandleLoginCompleted;
            _accountClient.AccountUpdated -= HandleAccountUpdated;
            _accountClient = null;
        }
        if (_connection is not null)
        {
            _connection.Exited -= HandleConnectionExited;
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }

    protected override void ExitThreadCore()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -=
            HandleDisplaySettingsChanged;
        _lifetime.Cancel();
        _activationTimer.Stop();
        _notifyIcon.Visible = false;
        DisposeConnectionAsync().GetAwaiter().GetResult();
        _settingsForm?.Dispose();
        _miniForm?.Dispose();
        _compactForm?.Dispose();
        _detailForm?.Dispose();
        _notifyIcon.Dispose();
        _activationTimer.Dispose();
        _dispatcher.Dispose();
        _lifetime.Dispose();
        base.ExitThreadCore();
    }
}
