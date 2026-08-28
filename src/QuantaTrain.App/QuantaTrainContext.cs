using System.Diagnostics;
using System.Text.Json;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.App;

internal sealed class QuantaTrainContext : ApplicationContext
{
    private static readonly string ProductVersion =
        typeof(QuantaTrainContext).Assembly.GetName().Version?.ToString(3) ?? "0.2.0";

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
    private readonly System.Windows.Forms.Timer _usageRefreshTimer;
    private readonly DataPaths _paths;
    private readonly JsonSettingsStore _settingsStore;
    private readonly JsonlHistoryStore _historyStore;
    private readonly JsonQuotaStateStore _quotaStateStore;
    private readonly RetentionMaintenance _retentionMaintenance;
    private readonly CodexSessionScanner _sessionScanner;
    private readonly UsageAggregateStore _usageStore;
    private readonly RedactedLogger _logger;
    private readonly LocalizationService _localizer;
    private readonly AppSettings _settings;
    private PanelPositionSettings _miniPanelPosition;
    private PanelPositionSettings _compactPanelPosition;
    private PanelPositionSettings _detailPanelPosition;
    private MiniForm? _miniForm;
    private CompactForm? _compactForm;
    private DetailForm? _detailForm;
    private JsonRpcConnection? _connection;
    private CodexAccountClient? _accountClient;
    private PollingCoordinator? _polling;
    private CodexInstallation? _codex;
    private WeeklyQuotaState? _previousState;
    private WeeklyQuotaState? _displayState;
    private IReadOnlyList<string> _historyItems = [];
    private UsageAnalysisSnapshot? _usageSnapshot;
    private AccountUsageSnapshot? _accountUsageSnapshot;
    private bool _viewUpdating;
    private string? _viewError;
    private bool _signedIn;
    private bool _confirmationPending;
    private bool _usageScanPending;
    private DateTimeOffset? _lastUsageRefreshUtc;
    private int _connectionFailureCount;
    private readonly HashSet<string> _notifiedCreditExpiries =
        new(StringComparer.Ordinal);
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
        _miniPanelPosition = _settings.Display.RememberPosition
            ? PanelPlacement.Clone(_settings.Display.MiniPanelPosition)
            : new PanelPositionSettings();
        _compactPanelPosition = _settings.Display.RememberPosition
            ? PanelPlacement.Clone(_settings.Display.CompactPanelPosition)
            : new PanelPositionSettings();
        _detailPanelPosition = _settings.Display.RememberPosition
            ? PanelPlacement.Clone(_settings.Display.DetailPanelPosition)
            : new PanelPositionSettings();
        _historyStore = new JsonlHistoryStore(_paths.HistoryDirectory);
        _quotaStateStore = new JsonQuotaStateStore(_paths.StateFile);
        _retentionMaintenance = new RetentionMaintenance(
            _paths.HistoryDirectory,
            _paths.UsageDirectory,
            _paths.LogsDirectory);
        _usageStore = new UsageAggregateStore(_paths.UsageDirectory);
        _sessionScanner = new CodexSessionScanner(
            Path.Combine(_paths.CacheDirectory, "session-scan-index.json"),
            _usageStore);
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
        _usageRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 60_000,
            Enabled = true,
        };
        _usageRefreshTimer.Tick += async (_, _) =>
        {
            try
            {
                var interval =
                    _settings.UsageAnalytics.RefreshIntervalMinutes;
                if (!_settings.UsageAnalytics.Enabled ||
                    interval <= 0 ||
                    _usageScanPending ||
                    _lastUsageRefreshUtc is not null &&
                    DateTimeOffset.UtcNow - _lastUsageRefreshUtc.Value <
                    TimeSpan.FromMinutes(interval))
                {
                    return;
                }
                await RefreshUsageAsync();
            }
            catch (OperationCanceledException) when (
                _lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                await _logger.WarningAsync(
                    exception.Message,
                    CancellationToken.None);
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
            if (_settings.ResetDetection.StoreQuotaState)
            {
                var persistedState = await _quotaStateStore.ReadAsync(
                    _lifetime.Token);
                _previousState = persistedState?.State;
                _displayState = _previousState;
            }
            await _retentionMaintenance.RunIfDueAsync(
                _settings.History.RetentionDays,
                _settings.Diagnostics.LogRetentionDays,
                DateTimeOffset.UtcNow,
                _lifetime.Token);
            _historyItems = await _historyStore.ReadRecentAsync(
                _settings.ResetDetection.RecentHistoryCount,
                _lifetime.Token);
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
            await RefreshUsageAsync();
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

    private async Task RestartPollingAsync()
    {
        if (_polling is null || _accountClient is null)
        {
            return;
        }
        _polling.StateChanged -= HandlePollingStateChanged;
        await _polling.DisposeAsync();
        _polling = null;
        await StartPollingAsync(_lifetime.Token);
    }

    private void HandlePollingStateChanged(
        object? sender,
        PollingStateChangedEventArgs eventArgs)
    {
        PostToUi(async () =>
        {
            var visibleState = eventArgs.Error is not null &&
                               !_settings.General.ShowCachedOnFailure
                ? null
                : eventArgs.State;
            UpdateViews(
                visibleState,
                eventArgs.IsUpdating,
                eventArgs.Error);
            if (!eventArgs.IsUpdating && eventArgs.Error is not null)
            {
                _connectionFailureCount++;
                if (_connectionFailureCount == 3 &&
                    _settings.Notifications.PersistentConnectionFailure)
                {
                    _notifyIcon.ShowBalloonTip(
                        5000,
                        "QuantaTray",
                        _localizer.Text("Notification.ConnectionFailure"),
                        ToolTipIcon.Warning);
                }
            }
            if (!eventArgs.IsUpdating &&
                eventArgs.Error is null &&
                eventArgs.State is not null)
            {
                await RefreshAccountUsageAsync();
                _connectionFailureCount = 0;
                if (_confirmationPending)
                {
                    return;
                }
                var before = _previousState;
                HandleQuotaNotifications(before, eventArgs.State);
                var recovery = before is not null &&
                    _settings.ResetDetection.DetectUnexpectedRecovery &&
                    ResetClassifier.Classify(
                        before,
                        eventArgs.State,
                        confirmed: false) is not null;
                if (recovery && _settings.ResetDetection.ConfirmRecovery)
                {
                    await ConfirmResetAsync(before!);
                    return;
                }
                if (recovery)
                {
                    var resetEvent = ResetClassifier.Classify(
                        before!,
                        eventArgs.State,
                        confirmed: true);
                    if (resetEvent is not null)
                    {
                        await RecordResetAsync(resetEvent);
                    }
                }
                await SetQuotaBaselineAsync(eventArgs.State);
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
            await Task.Delay(
                TimeSpan.FromSeconds(
                    _settings.ResetDetection.ConfirmationSeconds),
                _lifetime.Token);
            var confirmedState = await _polling.RefreshAsync(_lifetime.Token);
            if (confirmedState is null)
            {
                return;
            }

            var resetEvent = ResetClassifier.Classify(before, confirmedState, confirmed: true);
            if (resetEvent is not null)
            {
                await RecordResetAsync(resetEvent);
            }
            await SetQuotaBaselineAsync(confirmedState);
            UpdateViews(confirmedState, false, null);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            _confirmationPending = false;
        }
    }

    private async Task RecordResetAsync(ResetEvent resetEvent)
    {
        await _historyStore.AppendAsync(resetEvent, _lifetime.Token);
        _historyItems = await _historyStore.ReadRecentAsync(
            _settings.ResetDetection.RecentHistoryCount,
            _lifetime.Token);
        ShowResetNotification(resetEvent);
        await RefreshUsageAsync();
    }

    private async Task SetQuotaBaselineAsync(WeeklyQuotaState state)
    {
        _previousState = state;
        if (_settings.ResetDetection.StoreQuotaState)
        {
            await _quotaStateStore.WriteAsync(state, _lifetime.Token);
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
            _compactForm.SetSignedIn(_signedIn);
            _compactForm.UpdateState(
                _displayState,
                _viewUpdating,
                _viewError,
                _historyItems);
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
            _miniForm.UpdateState(_displayState);
        }
        return _miniForm;
    }

    private DetailForm GetDetailForm()
    {
        if (_detailForm is null || _detailForm.IsDisposed)
        {
            _detailForm = new DetailForm(
                _localizer,
                () => !_settings.Display.LockPosition,
                () => _settings.General.RefreshIntervalSeconds);
            _detailForm.Height = Math.Clamp(
                (int)Math.Round(
                    _settings.Display.DetailWindowHeightLogical *
                    _detailForm.DeviceDpi / 96d),
                _detailForm.MinimumSize.Height,
                _detailForm.MaximumSize.Height);
            _detailForm.RefreshRequested += async (_, _) => await RefreshAsync();
            _detailForm.UsageRefreshRequested += async (_, _) =>
                await RefreshUsageAsync();
            _detailForm.UsageViewRequested += async (_, _) =>
            {
                if (_settings.UsageAnalytics.Enabled &&
                    _settings.UsageAnalytics.RefreshWhenOpened)
                {
                    await RefreshUsageAsync();
                }
            };
            _detailForm.UsageFilterChanged += async (_, eventArgs) =>
            {
                _settings.UsageAnalytics.DefaultPeriod = eventArgs.Period;
                _settings.UsageAnalytics.DefaultMetric = eventArgs.Metric;
                await SaveSettingsAsync();
                await RefreshUsageAsync();
            };
            _detailForm.MiniRequested += (_, _) => ShowMini();
            _detailForm.CompactRequested += (_, _) => ShowCompact();
            _detailForm.SettingsRequested += (_, _) => QueueShowSettings();
            _detailForm.HistoryRequested += async (_, _) =>
                await ResetHistoryDialog.ShowAsync(
                    _detailForm,
                    _historyStore,
                    _localizer,
                    _lifetime.Token);
            _detailForm.MoveCompleted += async (_, _) =>
            {
                if (_settings.Display.RememberDetailHeight)
                {
                    _settings.Display.DetailWindowHeightLogical = Math.Clamp(
                        (int)Math.Round(
                            _detailForm.Height * 96d / _detailForm.DeviceDpi),
                        520,
                        2160);
                }
                await HandlePanelMoveCompletedAsync(_detailForm);
            };
            ApplyDisplaySettings();
            _detailForm.UpdateState(
                _displayState,
                _viewUpdating,
                _viewError,
                _historyItems);
            _detailForm.UpdateUsage(
                _usageSnapshot,
                _accountUsageSnapshot,
                _settings.UsageAnalytics,
                _usageScanPending);
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
        RenderPanel(form);
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
        RenderPanel(form);
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
        RenderPanel(form);
        if (_settings.Display.MiniClickThrough)
        {
            form.EnsureVisibleWithoutActivation(
                _settings.Display.AlwaysOnTop);
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
        if (TryCloseVisibleSettings(_settingsForm))
        {
            return;
        }
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
        form.Height = Math.Clamp(
            (int)Math.Round(
                _settings.Display.SettingsWindowHeightLogical *
                form.DeviceDpi / 96d),
            form.MinimumSize.Height,
            form.MaximumSize.Height);
        form.SetConnectionStatus(
            _codex is null
                ? _localizer.Text("Status.Stale")
                : $"{_localizer.Text("Status.Latest")}  Codex {_codex.Version}");
        if (_usageSnapshot is not null)
        {
            form.SetUsageScanStatus(
                _localizer.Text(
                    "Usage.FileResult",
                    _usageSnapshot.ScannedFileCount,
                    _usageSnapshot.SkippedFileCount,
                    _usageSnapshot.ErrorFileCount));
        }
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
        form.UsageRescanRequested += async (_, _) =>
            await RefreshUsageAsync();
        form.UsageCacheRebuildRequested += async (_, _) =>
        {
            await _sessionScanner.ResetCacheAsync(_lifetime.Token);
            await RefreshUsageAsync();
        };
        form.ConnectionDiagnosticRequested += async (_, _) =>
        {
            await RefreshAsync();
            form.SetConnectionStatus(
                _polling?.Current is null
                    ? _localizer.Text("Status.Failed")
                    : $"{_localizer.Text("Status.Latest")}  Codex {_codex?.Version ?? "—"}");
        };
        form.OpenHistoryRequested += (_, _) =>
            OpenDirectory(_paths.HistoryDirectory);
        form.HistoryExportRequested += async (_, _) =>
            await ExportHistoryAsync();
        form.UsageExportRequested += async (_, eventArgs) =>
            await ExportUsageAsync(eventArgs.Format);
        form.OpenLogsRequested += (_, _) =>
            OpenDirectory(_paths.LogsDirectory);
        form.ClearCacheRequested += async (_, _) =>
        {
            await _sessionScanner.ResetCacheAsync(_lifetime.Token);
            form.SetUsageScanStatus(_localizer.Text("Settings.UsageNotScanned"));
        };
        form.ReconnectRequested += async (_, _) =>
        {
            await DisposeConnectionAsync();
            await ConnectAsync(_lifetime.Token);
            form.SetConnectionStatus(
                _codex is null
                    ? _localizer.Text("Status.Failed")
                    : $"{_localizer.Text("Status.Latest")}  Codex {_codex.Version}");
        };
        form.OpenStorageRequested += (_, _) =>
            OpenDirectory(_paths.Root);
        form.OpenDocumentRequested += (_, eventArgs) =>
            OpenLocalDocument(eventArgs.FileName);
        form.MoveCompleted += async (_, _) =>
        {
            if (_settings.Display.RememberSettingsHeight)
            {
                _settings.Display.SettingsWindowHeightLogical = Math.Clamp(
                    (int)Math.Round(form.Height * 96d / form.DeviceDpi),
                    520,
                    2160);
                await SaveSettingsAsync();
            }
        };
        form.AllSettingsResetRequested += async (_, _) =>
        {
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
                    await _retentionMaintenance.RunIfDueAsync(
                        _settings.History.RetentionDays,
                        _settings.Diagnostics.LogRetentionDays,
                        DateTimeOffset.UtcNow,
                        _lifetime.Token,
                        force: true);
                    await RestartPollingAsync();
                    await RefreshUsageAsync();
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

    internal static bool TryCloseVisibleSettings(Form? form)
    {
        if (form is null || form.IsDisposed || !form.Visible)
        {
            return false;
        }
        form.Close();
        return true;
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

    private void HandleQuotaNotifications(
        WeeklyQuotaState? before,
        WeeklyQuotaState current)
    {
        if (_settings.Notifications.Remaining30 &&
            before is not null &&
            before.RemainingPercent > 30 &&
            current.RemainingPercent <= 30)
        {
            ShowQuotaNotification(30);
        }
        if (_settings.Notifications.Remaining10 &&
            before is not null &&
            before.RemainingPercent > 10 &&
            current.RemainingPercent <= 10)
        {
            ShowQuotaNotification(10);
        }

        if (!_settings.Notifications.ResetCreditExpiring)
        {
            return;
        }
        var limit = DateTimeOffset.UtcNow.AddHours(24);
        foreach (var credit in current.ResetCredits ?? [])
        {
            if (credit.ExpiresAtUtc is null ||
                credit.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
                credit.ExpiresAtUtc > limit)
            {
                continue;
            }
            var key = credit.ExpiresAtUtc.Value.ToUniversalTime().ToString("O");
            if (_notifiedCreditExpiries.Add(key))
            {
                _notifyIcon.ShowBalloonTip(
                    5000,
                    "QuantaTray",
                    _localizer.Text(
                        "Notification.CreditExpiring",
                        credit.ExpiresAtUtc.Value.ToLocalTime().ToString("g")),
                    ToolTipIcon.Warning);
            }
        }
    }

    private void ShowQuotaNotification(int threshold)
    {
        _notifyIcon.ShowBalloonTip(
            5000,
            "QuantaTray",
            _localizer.Text("Notification.Remaining", threshold),
            ToolTipIcon.Warning);
    }

    private async Task RefreshUsageAsync()
    {
        if (_usageScanPending)
        {
            return;
        }

        var resetEvents = await _historyStore.ReadRecentEventsAsync(
            32,
            _lifetime.Token);
        var period = UsagePeriodResolver.Resolve(
            _settings.UsageAnalytics.DefaultPeriod,
            DateTimeOffset.UtcNow,
            _polling?.Current ?? _previousState,
            resetEvents);
        await RefreshAccountUsageAsync();
        if (!_settings.UsageAnalytics.Enabled)
        {
            _usageSnapshot = UsageAnalysisSnapshot.Empty(
                period.FromUtc,
                period.ToUtc);
            _detailForm?.UpdateUsage(
                _usageSnapshot,
                _accountUsageSnapshot,
                _settings.UsageAnalytics,
                scanning: false);
            _settingsForm?.SetUsageScanStatus(
                _localizer.Text("Usage.Disabled"));
            return;
        }

        _usageScanPending = true;
        _detailForm?.UpdateUsage(
            _usageSnapshot,
            _accountUsageSnapshot,
            _settings.UsageAnalytics,
            scanning: true);
        _settingsForm?.SetUsageScanStatus(
            _localizer.Text("Usage.Scanning"));
        try
        {
            var result = await _sessionScanner.ScanAsync(
                _settings.UsageAnalytics,
                null,
                _lifetime.Token);
            var fromDate = DateOnly.FromDateTime(
                period.FromUtc.LocalDateTime.Date);
            var toDate = DateOnly.FromDateTime(
                period.ToUtc.LocalDateTime.Date);
            var rows = result.Rows
                .Where(row =>
                    row.Key.LocalDate >= fromDate &&
                    row.Key.LocalDate <= toDate)
                .ToArray();
            _usageSnapshot = new UsageAnalysisSnapshot(
                period.FromUtc,
                period.ToUtc,
                period.IsStartEstimated,
                rows,
                DateTimeOffset.UtcNow,
                result.ScannedFileCount,
                result.SkippedFileCount,
                result.ErrorFileCount,
                (result.Activities ?? [])
                    .Where(row =>
                        row.LocalDate >= fromDate && row.LocalDate <= toDate)
                    .ToArray());
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            FormatException or System.Text.Json.JsonException or
            InvalidOperationException)
        {
            await _logger.WarningAsync(
                exception.Message,
                CancellationToken.None);
            _usageSnapshot = new UsageAnalysisSnapshot(
                period.FromUtc,
                period.ToUtc,
                period.IsStartEstimated,
                [],
                DateTimeOffset.UtcNow,
                0,
                0,
                1);
        }
        finally
        {
            _usageScanPending = false;
            _lastUsageRefreshUtc = DateTimeOffset.UtcNow;
            _detailForm?.UpdateUsage(
                _usageSnapshot,
                _accountUsageSnapshot,
                _settings.UsageAnalytics,
                scanning: false);
            if (_usageSnapshot is not null)
            {
                _settingsForm?.SetUsageScanStatus(
                    _localizer.Text(
                        "Usage.FileResult",
                        _usageSnapshot.ScannedFileCount,
                        _usageSnapshot.SkippedFileCount,
                        _usageSnapshot.ErrorFileCount));
            }
        }
    }

    private async Task RefreshAccountUsageAsync()
    {
        if (!_settings.UsageAnalytics.ShowAccountUsage || _accountClient is null)
        {
            return;
        }
        try
        {
            _accountUsageSnapshot = await _accountClient.ReadUsageAsync(
                _lifetime.Token);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException or
            JsonException or FormatException)
        {
            await _logger.WarningAsync(
                exception.Message,
                CancellationToken.None);
        }
    }

    private void SetSignedIn(bool signedIn)
    {
        _signedIn = signedIn;
        _compactForm?.SetSignedIn(signedIn);
    }

    private void UpdateViews(WeeklyQuotaState? state, bool updating, string? error)
    {
        _viewUpdating = updating;
        _viewError = error;
        if (state is not null)
        {
            _displayState = state;
        }
        else if (
            !_settings.General.ShowCachedOnFailure ||
            !updating && error is null)
        {
            _displayState = null;
        }
        var displayedState = state ?? _displayState;
        _miniForm?.UpdateState(displayedState);
        _compactForm?.UpdateState(
            displayedState,
            updating,
            error,
            _historyItems);
        _detailForm?.UpdateState(
            displayedState,
            updating,
            error,
            _historyItems);
        _detailForm?.UpdateUsage(
            _usageSnapshot,
            _accountUsageSnapshot,
            _settings.UsageAnalytics,
            _usageScanPending);

        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = IconFactory.Create(displayedState?.RemainingPercent);
        oldIcon?.Dispose();
        var remainingText = displayedState is null
            ? _localizer.Text("Quota.Unavailable")
            : _localizer.Text(
                "Common.Remaining",
                QuotaDisplay.Number(displayedState.RemainingPercent));
        _notifyIcon.Text = remainingText.Length <= 63
            ? remainingText
            : remainingText[..63];
    }

    private static void RenderPanel(Form form)
    {
        form.PerformLayout();
        form.Invalidate(true);
        form.Update();
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
        var position = PanelPositionFor(form);
        PanelPlacement.Capture(form, position);
        PersistPanelPosition(form, position);
    }

    private void PositionPanel(Form form)
    {
        var position = PanelPositionFor(form);
        var previousMonitor = position.MonitorDeviceName;
        if (PanelPlacement.TryRestore(form, position))
        {
            PanelPlacement.Capture(form, position);
            PersistPanelPosition(form, position);
            if (_settings.Display.RememberPosition
                && !string.Equals(
                    previousMonitor,
                    position.MonitorDeviceName,
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
        PanelPlacement.Capture(form, position);
        PersistPanelPosition(form, position);
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
        var position = PanelPositionFor(form);
        PanelPlacement.Capture(form, position);
        PersistPanelPosition(form, position);
        if (_settings.Display.RememberPosition)
        {
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

        var position = PanelPositionFor(form);
        PanelPlacement.Capture(form, position);
        PersistPanelPosition(form, position);
    }

    private PanelPositionSettings PanelPositionFor(Form form) => form switch
    {
        MiniForm => _miniPanelPosition,
        CompactForm => _compactPanelPosition,
        DetailForm => _detailPanelPosition,
        _ => throw new ArgumentException("Unsupported panel form.", nameof(form)),
    };

    private void PersistPanelPosition(
        Form form,
        PanelPositionSettings position)
    {
        if (!_settings.Display.RememberPosition)
        {
            return;
        }

        var copy = PanelPlacement.Clone(position);
        switch (form)
        {
            case MiniForm:
                _settings.Display.MiniPanelPosition = copy;
                break;
            case CompactForm:
                _settings.Display.CompactPanelPosition = copy;
                break;
            case DetailForm:
                _settings.Display.DetailPanelPosition = copy;
                break;
            default:
                throw new ArgumentException(
                    "Unsupported panel form.",
                    nameof(form));
        }

        // Preserve downgrade compatibility without using this shared value in
        // the current runtime.
        _settings.Display.PanelPosition = PanelPlacement.Clone(position);
    }

    private async Task ResetPanelPositionAsync()
    {
        _miniPanelPosition = new PanelPositionSettings();
        _compactPanelPosition = new PanelPositionSettings();
        _detailPanelPosition = new PanelPositionSettings();
        _settings.Display.PanelPosition = new PanelPositionSettings();
        _settings.Display.MiniPanelPosition = new PanelPositionSettings();
        _settings.Display.CompactPanelPosition = new PanelPositionSettings();
        _settings.Display.DetailPanelPosition = new PanelPositionSettings();
        _miniForm?.Hide();
        _detailForm?.Hide();
        var compact = GetCompactForm();
        PanelPlacement.CenterOnPrimary(compact);
        PanelPlacement.Capture(compact, _compactPanelPosition);
        PersistPanelPosition(compact, _compactPanelPosition);
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
        var position = PanelPositionFor(form);
        PanelPlacement.Capture(form, position);
        PersistPanelPosition(form, position);
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

    private async Task ExportHistoryAsync()
    {
        using var dialog = new SaveFileDialog
        {
            Title = _localizer.Text("Settings.ExportHistoryJson"),
            Filter = "JSON (*.json)|*.json",
            FileName = $"QuantaTray-history-{DateTime.Now:yyyyMMdd}.json",
            AddExtension = true,
        };
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var temporary = $"{dialog.FileName}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous);
            using var writer = new Utf8JsonWriter(
                stream,
                new JsonWriterOptions { Indented = true });
            writer.WriteStartArray();
            foreach (var path in Directory.EnumerateFiles(
                         _paths.HistoryDirectory,
                         "*.jsonl").OrderBy(item => item, StringComparer.Ordinal))
            {
                await foreach (var line in File.ReadLinesAsync(
                                   path,
                                   _lifetime.Token))
                {
                    try
                    {
                        using var document = JsonDocument.Parse(line);
                        document.RootElement.WriteTo(writer);
                    }
                    catch (JsonException)
                    {
                        // Damaged rows are omitted from an otherwise valid export.
                    }
                }
            }
            writer.WriteEndArray();
            await writer.FlushAsync(_lifetime.Token);
            await stream.FlushAsync(_lifetime.Token);
            File.Move(temporary, dialog.FileName, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private async Task ExportUsageAsync(string format)
    {
        var isCsv = string.Equals(
            format,
            "csv",
            StringComparison.OrdinalIgnoreCase);
        using var dialog = new SaveFileDialog
        {
            Title = isCsv
                ? _localizer.Text("Settings.ExportUsageCsv")
                : _localizer.Text("Settings.ExportUsageJson"),
            Filter = isCsv ? "CSV (*.csv)|*.csv" : "JSON (*.json)|*.json",
            FileName =
                $"QuantaTray-usage-{DateTime.Now:yyyyMMdd}." +
                (isCsv ? "csv" : "json"),
            AddExtension = true,
        };
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var rows = await _usageStore.ReadAsync(
            DateTimeOffset.UtcNow.AddYears(-100),
            DateTimeOffset.UtcNow,
            _lifetime.Token);
        var exporter = new UsageExportService();
        if (isCsv)
        {
            await exporter.ExportCsvAsync(
                dialog.FileName,
                rows,
                _lifetime.Token);
        }
        else
        {
            await exporter.ExportJsonAsync(
                dialog.FileName,
                rows,
                _lifetime.Token);
        }
    }

    private static void OpenDirectory(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"")
        {
            UseShellExecute = true,
        });
    }

    private static void OpenLocalDocument(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
            });
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
        _usageRefreshTimer.Stop();
        _notifyIcon.Visible = false;
        DisposeConnectionAsync().GetAwaiter().GetResult();
        _settingsForm?.Dispose();
        _miniForm?.Dispose();
        _compactForm?.Dispose();
        _detailForm?.Dispose();
        _notifyIcon.Dispose();
        _activationTimer.Dispose();
        _usageRefreshTimer.Dispose();
        _dispatcher.Dispose();
        _lifetime.Dispose();
        base.ExitThreadCore();
    }
}
