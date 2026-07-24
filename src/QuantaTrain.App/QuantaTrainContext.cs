using System.Diagnostics;
using QuantaTrain.Core;
using QuantaTrain.Infrastructure;

namespace QuantaTrain.App;

internal sealed class QuantaTrainContext : ApplicationContext
{
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
    private readonly Control _dispatcher = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _activationTimer;
    private readonly DataPaths _paths;
    private readonly JsonSettingsStore _settingsStore;
    private readonly JsonlHistoryStore _historyStore;
    private readonly RedactedLogger _logger;
    private readonly LocalizationService _localizer;
    private readonly AppSettings _settings;
    private CompactForm? _compactForm;
    private DetailForm? _detailForm;
    private JsonRpcConnection? _connection;
    private CodexAccountClient? _accountClient;
    private PollingCoordinator? _polling;
    private CodexInstallation? _codex;
    private WeeklyQuotaState? _previousState;
    private IReadOnlyList<string> _historyItems = [];
    private bool _confirmationPending;
    private int _restartAttempt;
    private bool _exiting;

    public QuantaTrainContext(SingleInstanceCoordinator singleInstance)
    {
        _singleInstance = singleInstance;
        _dispatcher.CreateControl();
        _paths = DataPathResolver.Resolve(AppContext.BaseDirectory);
        _settingsStore = new JsonSettingsStore(_paths.SettingsFile);
        _settings = _settingsStore.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        _historyStore = new JsonlHistoryStore(_paths.HistoryDirectory);
        _historyStore.Prune(_settings.History.RetentionDays);
        _logger = new RedactedLogger(_paths.LogsDirectory);
        _localizer = new LocalizationService(Path.Combine(AppContext.BaseDirectory, "locales"));
        _localizer.Load(_settings.Language);

        _notifyIcon = new NotifyIcon
        {
            Icon = IconFactory.Create(null),
            Text = "QuantaTrain",
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
        _ = InitializeAsync();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
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

        var keepOpen = new ToolStripMenuItem(_localizer.Text("Menu.KeepOpen"))
        {
            CheckOnClick = true,
            Checked = _settings.Display.KeepPanelOpen,
        };
        keepOpen.CheckedChanged += async (_, _) =>
        {
            _settings.Display.KeepPanelOpen = keepOpen.Checked;
            await SaveSettingsAsync();
        };
        menu.Items.Add(keepOpen);

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
        menu.Items.Add(
            _localizer.Text("Common.Settings"),
            null,
            (_, _) => ShowSettings());
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
            (_, _) => MessageBox.Show(
                "QuantaTrain 0.1.0\nUnofficial; not affiliated with OpenAI.",
                "QuantaTrain",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information));
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
        _codex = await CodexLocator.LocateAsync(
            _settings.Connection.CodexExecutablePath,
            cancellationToken);
        if (_codex is null)
        {
            SetSignedIn(false);
            UpdateViews(null, false, _localizer.Text("Connection.CodexNotFound"));
            _notifyIcon.ShowBalloonTip(
                5000,
                "QuantaTrain",
                _localizer.Text("Connection.CodexNotFound"),
                ToolTipIcon.Warning);
            return;
        }

        _connection = await JsonRpcConnection.StartAsync(
            _codex.ExecutablePath,
            "0.1.0",
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
                "QuantaTrain",
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
            _compactForm = new CompactForm(_localizer);
            _compactForm.RefreshRequested += async (_, _) => await RefreshAsync();
            _compactForm.DetailRequested += (_, _) => ShowDetail();
            _compactForm.SettingsRequested += (_, _) => ShowSettings();
            _compactForm.SignInRequested += async (_, _) => await StartLoginAsync();
            _compactForm.Deactivate += (_, _) =>
            {
                if (!_settings.Display.KeepPanelOpen)
                {
                    _compactForm.Hide();
                }
            };
            ApplyDisplaySettings();
        }
        return _compactForm;
    }

    private DetailForm GetDetailForm()
    {
        if (_detailForm is null || _detailForm.IsDisposed)
        {
            _detailForm = new DetailForm(_localizer);
            _detailForm.RefreshRequested += async (_, _) => await RefreshAsync();
            _detailForm.CompactRequested += (_, _) => ShowCompact();
            _detailForm.SettingsRequested += (_, _) => ShowSettings();
            _detailForm.Deactivate += (_, _) =>
            {
                if (!_settings.Display.KeepPanelOpen)
                {
                    _detailForm.Hide();
                }
            };
            ApplyDisplaySettings();
        }
        return _detailForm;
    }

    private void ShowCompact()
    {
        _detailForm?.Hide();
        var form = GetCompactForm();
        form.PositionNearTray();
        form.Show();
        form.Activate();
    }

    private void ShowDetail()
    {
        _compactForm?.Hide();
        var form = GetDetailForm();
        form.PositionNearTray();
        form.Show();
        form.Activate();
        if (_settings.General.RefreshOnPanelOpen)
        {
            _ = RefreshAsync();
        }
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(_settings, _localizer);
        form.SettingsSaved += async (_, _) =>
        {
            ApplyDisplaySettings();
            await SaveSettingsAsync();
        };
        form.ShowDialog();
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
                "QuantaTrain",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private async Task RefreshAsync()
    {
        if (_polling is not null)
        {
            await _polling.RefreshAsync(_lifetime.Token);
        }
    }

    private void SetSignedIn(bool signedIn)
    {
        _compactForm?.SetSignedIn(signedIn);
    }

    private void UpdateViews(WeeklyQuotaState? state, bool updating, string? error)
    {
        _compactForm?.UpdateState(state, updating, error);
        _detailForm?.UpdateState(state, updating, error, _historyItems);

        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = IconFactory.Create(state?.RemainingPercent);
        oldIcon?.Dispose();
        var remainingText = state is null
            ? _localizer.Text("Quota.Unavailable")
            : _localizer.Text("Common.Remaining", Math.Round(state.RemainingPercent));
        _notifyIcon.Text = remainingText.Length <= 63
            ? remainingText
            : remainingText[..63];
    }

    private void ApplyDisplaySettings()
    {
        foreach (var form in new Form?[] { _compactForm, _detailForm })
        {
            if (form is null || form.IsDisposed)
            {
                continue;
            }
            form.TopMost = _settings.Display.AlwaysOnTop;
            form.Opacity = _settings.Display.OpacityPercent / 100d;
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsStore.SaveAsync(_settings, _lifetime.Token);
            StartupRegistration.SetEnabled(
                _settings.General.LaunchAtStartup,
                Environment.ProcessPath
                    ?? Path.Combine(AppContext.BaseDirectory, "QuantaTrain.exe"));
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
        if (_dispatcher.IsDisposed || _exiting)
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
        });
    }

    private async Task DisposeConnectionAsync()
    {
        if (_polling is not null)
        {
            _polling.StateChanged -= HandlePollingStateChanged;
            await _polling.DisposeAsync();
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
            await _connection.DisposeAsync();
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
        _lifetime.Cancel();
        _activationTimer.Stop();
        _notifyIcon.Visible = false;
        DisposeConnectionAsync().GetAwaiter().GetResult();
        _compactForm?.Dispose();
        _detailForm?.Dispose();
        _notifyIcon.Dispose();
        _activationTimer.Dispose();
        _dispatcher.Dispose();
        _lifetime.Dispose();
        base.ExitThreadCore();
    }
}
