using System.ComponentModel;
using System.Drawing.Drawing2D;
using QuantaTrain.Core;

namespace QuantaTrain.App;

internal sealed class DetailForm : FixedWidthResizableForm
{
    private readonly LocalizationService _localizer;
    private readonly Func<bool> _canDrag;
    private readonly Panel _overviewPage;
    private readonly Panel _usagePage;
    private readonly Button _overviewTab;
    private readonly Button _usageTab;
    private readonly Panel _tabUnderline;
    private readonly QuotaRingControl _quotaRing = new();
    private readonly Label _resetAt = ValueLabel();
    private readonly Label _windowStart = ValueLabel();
    private readonly Label _plan = ValueLabel();
    private readonly Label _connection = ValueLabel();
    private readonly Label _lastUpdated = ValueLabel();
    private readonly Label _nextRefresh = ValueLabel();
    private readonly Label _version = ValueLabel();
    private readonly FlowLayoutPanel _credits = RowsPanel();
    private readonly FlowLayoutPanel _history = RowsPanel();
    private readonly Label _status = ValueLabel();
    private readonly Label _usageStatus = ValueLabel();
    private readonly FlowLayoutPanel _modelRows = RowsPanel();
    private readonly Label _tokenDetails = ValueLabel();
    private readonly Label _timeDetails = ValueLabel();
    private readonly UsageDonutControl _reasoningDonut = new();
    private readonly Label _reasoningLegend = ValueLabel();
    private readonly ComboBox _period = new();
    private readonly ComboBox _metric = new();
    private bool _syncingUsageFilters;

    public DetailForm(LocalizationService localizer, Func<bool>? canDrag = null)
    {
        _localizer = localizer;
        _canDrag = canDrag ?? (() => true);
        Text = $"{_localizer.Text("Menu.ShowDetail")} — QuantaTray";
        ConfigureFixedLogicalWidth(800, 600, 520);
        StartPosition = FormStartPosition.Manual;
        AccessibleName = "QuantaTray detailed quota and usage dashboard";

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Theme.Window,
        };
        var brand = UiFactory.BrandIcon(new Point(18, 14), 24);
        var title = UiFactory.Label(
            $"QuantaTray  {_localizer.Text("Menu.ShowDetail")}",
            new Point(52, 12),
            11F,
            FontStyle.Bold);
        title.AutoSize = false;
        title.Size = new Size(290, 30);
        title.TextAlign = ContentAlignment.MiddleLeft;
        var refresh = HeaderButton(
            _localizer.Text("Common.Refresh"),
            558,
            88);
        refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        var settings = HeaderButton(
            _localizer.Text("Common.Settings"),
            650,
            88);
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        var close = HeaderButton("×", 748, 34);
        close.Click += (_, _) => Hide();
        header.Controls.AddRange([brand, title, refresh, settings, close]);
        MakeDraggable(header);
        MakeDraggable(brand);
        MakeDraggable(title);
        title.DoubleClick += (_, _) => CompactRequested?.Invoke(this, EventArgs.Empty);

        var tabs = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Theme.Window,
        };
        _overviewTab = TabButton(
            _localizer.Text("Detail.OverviewTab"),
            18,
            130);
        _usageTab = TabButton(
            _localizer.Text("Detail.UsageTab"),
            150,
            130);
        _overviewTab.Click += (_, _) => SelectTab(usage: false);
        _usageTab.Click += (_, _) => SelectTab(usage: true);
        _tabUnderline = new Panel
        {
            Bounds = new Rectangle(18, 40, 130, 3),
            BackColor = Theme.Blue,
        };
        tabs.Controls.AddRange([_overviewTab, _usageTab, _tabUnderline]);

        var pages = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Window,
        };
        _overviewPage = BuildOverviewPage();
        _usagePage = BuildUsagePage();
        pages.Controls.AddRange([_usagePage, _overviewPage]);
        Controls.AddRange([pages, tabs, header]);
        SelectTab(usage: false);
        UpdateState(null, false, null, []);
        UpdateUsage(null, new UsageAnalyticsSettings(), false);
    }

    protected override bool CanDrag => _canDrag();

    public event EventHandler? RefreshRequested;
    public event EventHandler? UsageRefreshRequested;
    public event EventHandler? CompactRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler<UsageFilterChangedEventArgs>? UsageFilterChanged;

    public void UpdateState(
        WeeklyQuotaState? state,
        bool updating,
        string? error,
        IReadOnlyList<string> history)
    {
        _quotaRing.RemainingPercent = state?.RemainingPercent;
        _quotaRing.Caption = _localizer.Text("Common.Remaining", string.Empty)
            .Trim();
        _resetAt.Text = state?.ResetsAtUtc?.ToLocalTime().ToString("g") ?? "—";
        _windowStart.Text = state?.ResetsAtUtc is not null &&
                            state.WindowDurationMinutes is > 0
            ? state.ResetsAtUtc.Value
                .AddMinutes(-state.WindowDurationMinutes.Value)
                .ToLocalTime()
                .ToString("g")
            : "—";
        _plan.Text = string.IsNullOrWhiteSpace(state?.PlanType)
            ? "—"
            : state.PlanType;
        _connection.Text = error is not null
            ? _localizer.Text("Status.Failed")
            : updating
                ? _localizer.Text("Status.Updating")
                : state is null
                    ? _localizer.Text("Status.Stale")
                    : _localizer.Text("Status.Latest");
        _lastUpdated.Text = state?.ObservedAtUtc.ToLocalTime().ToString("G") ?? "—";
        _nextRefresh.Text = updating
            ? _localizer.Text("Status.Updating")
            : _localizer.Text("Detail.AutomaticRefresh");
        _version.Text =
            typeof(DetailForm).Assembly.GetName().Version?.ToString(3) ?? "—";
        _status.Text = _connection.Text;
        PopulateRows(
            _credits,
            state?.ResetCredits?
                .Take(3)
                .Select((credit, index) =>
                    $"{_localizer.Text("Credits.RemainingShort")} {index + 1}    " +
                    $"{credit.ExpiresAtUtc?.ToLocalTime():yyyy/MM/dd}")
                .ToArray() is { Length: > 0 } creditRows
                ? creditRows
                : [_localizer.Text("Credits.None")]);
        PopulateRows(
            _history,
            history.Count == 0
                ? [_localizer.Text("History.Empty")]
                : history.Take(3));
    }

    public void UpdateUsage(
        UsageAnalysisSnapshot? snapshot,
        UsageAnalyticsSettings settings,
        bool scanning)
    {
        _syncingUsageFilters = true;
        _period.SelectedIndex = settings.DefaultPeriod switch
        {
            "7-days" => 1,
            "30-days" => 2,
            "90-days" => 3,
            _ => 0,
        };
        _metric.SelectedIndex = settings.DefaultMetric switch
        {
            "elapsed-time" => 1,
            "turn-count" => 2,
            _ => 0,
        };
        _syncingUsageFilters = false;
        if (!settings.Enabled)
        {
            _usageStatus.Text = _localizer.Text("Usage.Disabled");
            _modelRows.Controls.Clear();
            _tokenDetails.Text = _localizer.Text("Usage.EnableInSettings");
            _timeDetails.Text = "—";
            _reasoningLegend.Text = "—";
            _reasoningDonut.SetValues(
                new Dictionary<string, long>(StringComparer.Ordinal));
            return;
        }

        if (scanning)
        {
            _usageStatus.Text = _localizer.Text("Usage.Scanning");
            return;
        }
        if (snapshot is null)
        {
            _usageStatus.Text = _localizer.Text("Status.Stale");
            return;
        }

        var summaries = UsageAnalyticsAggregator.SummarizeModels(
            snapshot.Rows,
            settings.MaxIndividualModels,
            settings.GroupOtherModels);
        var total = summaries.Sum(
            summary => summary.Tokens.EffectiveTotalTokens);
        PopulateModelRows(summaries, settings);
        var tokens = summaries.Aggregate(
            UsageTokenTotals.Empty,
            (current, summary) => current + summary.Tokens);
        _tokenDetails.Text = string.Join(
            Environment.NewLine,
            [
                $"{_localizer.Text("Usage.InputTokens")}    {tokens.InputTokens:N0}",
                $"{_localizer.Text("Usage.CachedInput")}    {tokens.CachedInputTokens:N0}",
                $"{_localizer.Text("Usage.CacheWrite")}    {tokens.CacheWriteInputTokens:N0}",
                $"{_localizer.Text("Usage.OutputTokens")}    {tokens.OutputTokens:N0}",
                $"{_localizer.Text("Usage.ReasoningTokens")}    {tokens.ReasoningOutputTokens:N0}",
                $"{_localizer.Text("Common.Total")}    {tokens.EffectiveTotalTokens:N0}",
            ]);
        var elapsed = summaries.Sum(summary => summary.ElapsedMilliseconds);
        var turns = summaries.Sum(summary => summary.TurnCount);
        _timeDetails.Text = string.Join(
            Environment.NewLine,
            [
                $"{_localizer.Text("Usage.TotalTime")}    {FormatDuration(elapsed)}",
                $"{_localizer.Text("Usage.AverageTurnTime")}    " +
                $"{FormatDuration(turns == 0 ? 0 : elapsed / turns)}",
                $"{_localizer.Text("Usage.TurnCount")}    {turns:N0}",
                $"{_localizer.Text("Usage.AverageInput")}    " +
                $"{(turns == 0 ? 0 : tokens.InputTokens / turns):N0}",
                $"{_localizer.Text("Usage.AverageOutput")}    " +
                $"{(turns == 0 ? 0 : tokens.OutputTokens / turns):N0}",
            ]);
        var reasoning = summaries
            .SelectMany(summary => summary.ReasoningTokens)
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Value));
        _reasoningDonut.SetValues(reasoning);
        _reasoningLegend.Text = string.Join(
            Environment.NewLine,
            reasoning.OrderByDescending(item => item.Value)
                .Select(item =>
                    $"{DisplayEffort(item.Key)}  {item.Value:N0}  " +
                    $"{Percent(item.Value, total):0}%"));
        _usageStatus.Text =
            $"{snapshot.RefreshedAtUtc.ToLocalTime():yyyy/MM/dd HH:mm:ss}   " +
            $"{_localizer.Text("Usage.FileResult", snapshot.ScannedFileCount, snapshot.SkippedFileCount, snapshot.ErrorFileCount)}";
    }

    public void PositionNearTray()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ??
            Screen.GetWorkingArea(Cursor.Position);
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
    }

    private Panel BuildOverviewPage()
    {
        var page = Page();
        var quota = Card(new Rectangle(16, 10, 362, 210), "Quota.Weekly");
        _quotaRing.Bounds = new Rectangle(18, 45, 132, 132);
        quota.Controls.Add(_quotaRing);
        AddKeyValue(quota, "Quota.NextResetAt", _resetAt, 174, 52);
        AddKeyValue(quota, "Quota.WindowStartedAt", _windowStart, 174, 104);
        var progress = new QuotaProgressBar
        {
            Bounds = new Rectangle(18, 184, 326, 10),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };
        _quotaRing.ValueChanged += (_, _) =>
        {
            progress.Value = (int)Math.Round(
                Math.Clamp(100 - (_quotaRing.RemainingPercent ?? 100), 0, 100));
            progress.ValueColor = Theme.Blue;
        };
        quota.Controls.Add(progress);

        var info = Card(
            new Rectangle(16, 230, 362, 224),
            "Detail.PlanConnection");
        AddKeyValue(info, "Detail.PlanLabel", _plan, 18, 48);
        AddKeyValue(info, "Detail.ConnectionLabel", _connection, 18, 87);
        AddKeyValue(info, "Detail.LastUpdated", _lastUpdated, 18, 126);
        AddKeyValue(info, "Detail.NextAutomaticRefresh", _nextRefresh, 18, 165);
        AddKeyValue(info, "Detail.AppVersion", _version, 18, 204);

        var credits = Card(
            new Rectangle(390, 10, 376, 164),
            "Credits.Title");
        _credits.Bounds = new Rectangle(18, 45, 340, 104);
        credits.Controls.Add(_credits);

        var history = Card(
            new Rectangle(390, 184, 376, 270),
            "History.Recent");
        _history.Bounds = new Rectangle(18, 45, 340, 174);
        history.Controls.Add(_history);
        var allHistory = LinkButton(_localizer.Text("History.ShowAll"));
        allHistory.Bounds = new Rectangle(198, 229, 160, 28);
        history.Controls.Add(allHistory);

        var footer = new Panel
        {
            Bounds = new Rectangle(16, 462, 750, 28),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = Theme.Window,
        };
        _status.Bounds = new Rectangle(0, 3, 726, 22);
        _status.ForeColor = Theme.Muted;
        footer.Controls.Add(_status);
        page.Controls.AddRange([quota, info, credits, history, footer]);
        return page;
    }

    private Panel BuildUsagePage()
    {
        var page = Page();
        var filters = new Panel
        {
            Bounds = new Rectangle(16, 8, 750, 44),
            BackColor = Theme.Window,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
        };
        var periodLabel = Caption(_localizer.Text("Usage.Period"), 0, 13);
        _period.Bounds = new Rectangle(90, 7, 215, 31);
        _period.Items.AddRange(
        [
            _localizer.Text("Usage.CurrentWindow"),
            _localizer.Text("Usage.Last7Days"),
            _localizer.Text("Usage.Last30Days"),
            _localizer.Text("Usage.Last90Days"),
        ]);
        _period.SelectedIndex = 0;
        Theme.StyleCombo(_period);
        var metricLabel = Caption(_localizer.Text("Usage.Metric"), 326, 13);
        _metric.Bounds = new Rectangle(405, 7, 165, 31);
        _metric.Items.AddRange(
        [
            _localizer.Text("Usage.TotalTokens"),
            _localizer.Text("Usage.ElapsedTime"),
            _localizer.Text("Usage.TurnCount"),
        ]);
        _metric.SelectedIndex = 0;
        Theme.StyleCombo(_metric);
        _period.SelectedIndexChanged += (_, _) => RaiseUsageFilterChanged();
        _metric.SelectedIndexChanged += (_, _) => RaiseUsageFilterChanged();
        var refresh = ActionButton(_localizer.Text("Usage.Rescan"));
        refresh.Bounds = new Rectangle(580, 7, 170, 31);
        refresh.Click += (_, _) => UsageRefreshRequested?.Invoke(this, EventArgs.Empty);
        filters.Controls.AddRange(
        [
            periodLabel, _period, metricLabel, _metric, refresh,
        ]);

        var models = Card(
            new Rectangle(16, 58, 750, 246),
            "Usage.ModelStatus");
        var modelHeader = new Panel
        {
            Bounds = new Rectangle(16, 38, 718, 24),
            BackColor = Theme.Surface,
        };
        AddHeader(modelHeader, _localizer.Text("Usage.Model"), 0, 115);
        AddHeader(modelHeader, _localizer.Text("Usage.Share"), 270, 52);
        AddHeader(modelHeader, _localizer.Text("Usage.TotalTokens"), 326, 96);
        AddHeader(modelHeader, _localizer.Text("Usage.ElapsedTime"), 428, 72);
        AddHeader(modelHeader, _localizer.Text("Usage.TurnCount"), 504, 42);
        AddHeader(
            modelHeader,
            _localizer.Text("Usage.ReasoningBreakdown"),
            550,
            132);
        AddHeader(modelHeader, _localizer.Text("Usage.FastRate"), 686, 32);
        _modelRows.Bounds = new Rectangle(16, 63, 718, 167);
        models.Controls.AddRange([modelHeader, _modelRows]);

        var token = Card(
            new Rectangle(16, 314, 242, 160),
            "Usage.TokenBreakdown");
        _tokenDetails.Bounds = new Rectangle(16, 42, 210, 108);
        _tokenDetails.AutoSize = false;
        token.Controls.Add(_tokenDetails);

        var time = Card(
            new Rectangle(268, 314, 228, 160),
            "Usage.TimeTurnSummary");
        _timeDetails.Bounds = new Rectangle(16, 42, 196, 108);
        _timeDetails.AutoSize = false;
        time.Controls.Add(_timeDetails);

        var reasoning = Card(
            new Rectangle(506, 314, 260, 160),
            "Usage.ReasoningBreakdown");
        _reasoningDonut.Bounds = new Rectangle(16, 45, 112, 112);
        _reasoningLegend.Bounds = new Rectangle(138, 46, 106, 100);
        _reasoningLegend.AutoSize = false;
        reasoning.Controls.AddRange([_reasoningDonut, _reasoningLegend]);
        _usageStatus.Bounds = new Rectangle(16, 478, 750, 20);
        _usageStatus.ForeColor = Theme.Muted;
        page.Controls.AddRange(
        [
            filters, models, token, time, reasoning, _usageStatus,
        ]);
        return page;
    }

    private void PopulateModelRows(
        IReadOnlyList<UsageModelSummary> summaries,
        UsageAnalyticsSettings settings)
    {
        _modelRows.SuspendLayout();
        _modelRows.Controls.Clear();
        var maximum = Math.Max(
            1,
            summaries.Select(item => MetricValue(item, settings.DefaultMetric))
                .DefaultIfEmpty(1)
                .Max());
        var metricTotal = summaries.Sum(
            item => MetricValue(item, settings.DefaultMetric));
        foreach (var summary in summaries)
        {
            var row = new Panel
            {
                Width = 718,
                Height = 23,
                Margin = Padding.Empty,
                BackColor = Theme.Surface,
            };
            var model = Caption(summary.Model, 0, 2);
            model.Size = new Size(115, 19);
            var barTrack = new Panel
            {
                Bounds = new Rectangle(118, 6, 142, 10),
                BackColor = Theme.SurfaceRaised,
            };
            var fill = new Panel
            {
                Bounds = new Rectangle(
                    0,
                    0,
                    (int)Math.Round(
                        142d *
                        MetricValue(summary, settings.DefaultMetric) /
                        maximum),
                    10),
                BackColor = Theme.Blue,
            };
            barTrack.Controls.Add(fill);
            var share = Caption(
                $"{Percent(
                    MetricValue(summary, settings.DefaultMetric),
                    metricTotal):0}%",
                270,
                2);
            share.Size = new Size(52, 19);
            var tokens = Caption(
                $"{summary.Tokens.EffectiveTotalTokens:N0}",
                326,
                2);
            tokens.Size = new Size(96, 19);
            var elapsed = Caption(
                FormatDuration(summary.ElapsedMilliseconds),
                428,
                2);
            elapsed.Size = new Size(72, 19);
            var turns = Caption($"{summary.TurnCount:N0}", 504, 2);
            turns.Size = new Size(42, 19);
            var reasoning = Caption(
                settings.ShowReasoningBreakdown
                    ? string.Join(
                        " / ",
                        summary.ReasoningTokens
                            .OrderByDescending(item => item.Value)
                            .Take(2)
                            .Select(item =>
                                $"{DisplayEffort(item.Key)} " +
                                $"{Percent(item.Value, summary.Tokens.EffectiveTotalTokens):0}%"))
                    : "—",
                550,
                2);
            reasoning.Size = new Size(132, 19);
            var fast = summary.ServiceTierTokens
                .Where(item => item.Key == "fast")
                .Sum(item => item.Value);
            var tier = Caption(
                settings.ShowServiceTierBreakdown
                    ? $"{Percent(fast, summary.Tokens.EffectiveTotalTokens):0}%"
                    : "—",
                686,
                2);
            tier.Size = new Size(32, 19);
            row.Controls.AddRange(
            [
                model, barTrack, share, tokens, elapsed, turns, reasoning, tier,
            ]);
            _modelRows.Controls.Add(row);
        }
        if (summaries.Count > 0)
        {
            var totalRow = new Panel
            {
                Width = 718,
                Height = 23,
                Margin = Padding.Empty,
                BackColor = Theme.SurfaceRaised,
            };
            var totalLabel = Caption(_localizer.Text("Common.Total"), 0, 2);
            totalLabel.Font = Theme.Ui(8.2F, FontStyle.Bold);
            totalLabel.Size = new Size(260, 19);
            var share = Caption("100%", 270, 2);
            share.Size = new Size(52, 19);
            var tokens = Caption(
                $"{summaries.Sum(item => item.Tokens.EffectiveTotalTokens):N0}",
                326,
                2);
            tokens.Size = new Size(96, 19);
            var elapsed = Caption(
                FormatDuration(
                    summaries.Sum(item => item.ElapsedMilliseconds)),
                428,
                2);
            elapsed.Size = new Size(72, 19);
            var turns = Caption(
                $"{summaries.Sum(item => item.TurnCount):N0}",
                504,
                2);
            turns.Size = new Size(42, 19);
            totalRow.Controls.AddRange(
            [
                totalLabel, share, tokens, elapsed, turns,
            ]);
            _modelRows.Controls.Add(totalRow);
        }
        if (summaries.Count == 0)
        {
            _modelRows.Controls.Add(new Label
            {
                Text = _localizer.Text("Usage.NoData"),
                ForeColor = Theme.Muted,
                BackColor = Theme.Surface,
                Font = Theme.Ui(9F),
                Size = new Size(718, 32),
                TextAlign = ContentAlignment.MiddleCenter,
            });
        }
        _modelRows.ResumeLayout();
    }

    private void SelectTab(bool usage)
    {
        _overviewPage.Visible = !usage;
        _usagePage.Visible = usage;
        _overviewPage.BringToFront();
        if (usage)
        {
            _usagePage.BringToFront();
        }
        _overviewTab.ForeColor = usage ? Theme.Muted : Theme.Text;
        _usageTab.ForeColor = usage ? Theme.Text : Theme.Muted;
        _tabUnderline.Left = usage ? _usageTab.Left : _overviewTab.Left;
        _tabUnderline.Width = usage ? _usageTab.Width : _overviewTab.Width;
    }

    private void RaiseUsageFilterChanged()
    {
        if (_syncingUsageFilters ||
            _period.SelectedIndex < 0 ||
            _metric.SelectedIndex < 0)
        {
            return;
        }
        UsageFilterChanged?.Invoke(
            this,
            new UsageFilterChangedEventArgs(
                _period.SelectedIndex switch
                {
                    1 => "7-days",
                    2 => "30-days",
                    3 => "90-days",
                    _ => "current-window",
                },
                _metric.SelectedIndex switch
                {
                    1 => "elapsed-time",
                    2 => "turn-count",
                    _ => "total-tokens",
                }));
    }

    private RoundedPanel Card(Rectangle bounds, string headingKey)
    {
        var card = new RoundedPanel
        {
            Bounds = bounds,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
        };
        var heading = UiFactory.Label(
            _localizer.Text(headingKey),
            new Point(16, 13),
            9.2F,
            FontStyle.Bold);
        heading.AutoSize = false;
        heading.Size = new Size(bounds.Width - 32, 24);
        card.Controls.Add(heading);
        return card;
    }

    private void AddKeyValue(
        Control parent,
        string key,
        Label value,
        int x,
        int y)
    {
        var caption = Caption(_localizer.Text(key), x, y);
        caption.ForeColor = Theme.Muted;
        value.Location = new Point(x, y + 19);
        value.Size = new Size(parent.Width - x - 16, 24);
        value.AutoSize = false;
        parent.Controls.AddRange([caption, value]);
    }

    private static Panel Page() =>
        new()
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Window,
            AutoScroll = true,
        };

    private static FlowLayoutPanel RowsPanel() =>
        new()
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Theme.Surface,
            Margin = Padding.Empty,
        };

    private static Label ValueLabel() =>
        new()
        {
            AutoSize = true,
            Font = Theme.Ui(8.5F),
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
        };

    private static Label Caption(string text, int x, int y) =>
        new()
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            Font = Theme.Ui(8.2F),
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
        };

    private static void AddHeader(
        Control parent,
        string text,
        int x,
        int width)
    {
        var label = Caption(text, x, 2);
        label.ForeColor = Theme.Muted;
        label.AutoSize = false;
        label.Size = new Size(width, 20);
        parent.Controls.Add(label);
    }

    private static Button ActionButton(string text) =>
        new()
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderColor = Theme.Border },
            BackColor = Theme.SurfaceRaised,
            ForeColor = Theme.Text,
            Font = Theme.Ui(8.2F),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };

    private static Button LinkButton(string text)
    {
        var button = ActionButton(text);
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Theme.Surface;
        button.ForeColor = Theme.Blue;
        return button;
    }

    private Button HeaderButton(string text, int x, int width)
    {
        var button = ActionButton(text);
        button.Bounds = new Rectangle(x, 10, width, 32);
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Theme.Window;
        button.AccessibleName = text;
        return button;
    }

    private static Button TabButton(string text, int x, int width) =>
        new()
        {
            Text = text,
            Bounds = new Rectangle(x, 2, width, 38),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Theme.Window,
            ForeColor = Theme.Text,
            Font = Theme.Ui(9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };

    private static void PopulateRows(
        FlowLayoutPanel panel,
        IEnumerable<string> values)
    {
        panel.SuspendLayout();
        panel.Controls.Clear();
        foreach (var value in values)
        {
            panel.Controls.Add(new Label
            {
                Text = value,
                Font = Theme.Ui(8.4F),
                ForeColor = Theme.Text,
                BackColor = Theme.Surface,
                Margin = new Padding(0, 0, 0, 4),
                Padding = new Padding(8, 6, 8, 0),
                Size = new Size(panel.ClientSize.Width - 2, 29),
                AutoEllipsis = true,
            });
        }
        panel.ResumeLayout();
    }

    private static string FormatDuration(long milliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes:00}m"
            : duration.TotalMinutes >= 1
                ? $"{(int)duration.TotalMinutes}m {duration.Seconds:00}s"
                : $"{duration.TotalSeconds:0}s";
    }

    private string DisplayEffort(string value) =>
        _localizer.Text($"Usage.Effort.{value}");

    private static double Percent(long value, long total) =>
        total <= 0 ? 0 : Math.Clamp(value * 100d / total, 0, 100);

    private static long MetricValue(
        UsageModelSummary summary,
        string metric) =>
        metric switch
        {
            "elapsed-time" => summary.ElapsedMilliseconds,
            "turn-count" => summary.TurnCount,
            _ => summary.Tokens.EffectiveTotalTokens,
        };
}

internal sealed class UsageFilterChangedEventArgs(
    string period,
    string metric) : EventArgs
{
    public string Period { get; } = period;
    public string Metric { get; } = metric;
}

internal sealed class QuotaRingControl : Control
{
    private double? _remainingPercent;
    private readonly Label _valueLabel;

    public QuotaRingControl()
    {
        DoubleBuffered = true;
        BackColor = Theme.Surface;
        _valueLabel = new Label
        {
            AutoSize = false,
            Bounds = new Rectangle(12, 43, 108, 42),
            Font = Theme.Ui(17F, FontStyle.Bold),
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Text = "—",
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(_valueLabel);
    }

    public event EventHandler? ValueChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption { get; set; } = string.Empty;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double? RemainingPercent
    {
        get => _remainingPercent;
        set
        {
            _remainingPercent = value;
            _valueLabel.Text = value is null
                ? "—"
                : QuotaDisplay.Percent(value.Value);
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(11, 11, Width - 22, Height - 22);
        using var track = new Pen(Theme.SurfaceRaised, 12)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        eventArgs.Graphics.DrawArc(track, bounds, -90, 359.9F);
        if (RemainingPercent is not null)
        {
            using var value = new Pen(
                Theme.QuotaColor(RemainingPercent),
                12)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            eventArgs.Graphics.DrawArc(
                value,
                bounds,
                -90,
                (float)(359.9 * Math.Clamp(
                    RemainingPercent.Value,
                    0,
                    100) / 100d));
        }
        var captionBounds = new Rectangle(0, Height / 2 - 38, Width, 18);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            Caption,
            Theme.Ui(7.5F),
            captionBounds,
            Theme.Muted,
            TextFormatFlags.HorizontalCenter);
    }
}

internal sealed class UsageDonutControl : Control
{
    private static readonly Color[] Palette =
    [
        Theme.Green,
        Theme.Blue,
        Color.FromArgb(235, 139, 53),
        Color.FromArgb(165, 88, 213),
        Theme.Subtle,
    ];
    private IReadOnlyList<long> _values = [];

    public UsageDonutControl()
    {
        DoubleBuffered = true;
        BackColor = Theme.Surface;
    }

    public void SetValues(IReadOnlyDictionary<string, long> values)
    {
        _values = values
            .OrderByDescending(item => item.Value)
            .Select(item => item.Value)
            .Where(value => value > 0)
            .ToArray();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(8, 8, Width - 16, Height - 16);
        using var track = new Pen(Theme.SurfaceRaised, 14);
        eventArgs.Graphics.DrawArc(track, bounds, -90, 359.9F);
        var total = _values.Sum();
        if (total <= 0)
        {
            return;
        }
        var start = -90F;
        for (var index = 0; index < _values.Count; index++)
        {
            var sweep = (float)(_values[index] * 360d / total);
            using var pen = new Pen(Palette[index % Palette.Length], 14);
            eventArgs.Graphics.DrawArc(pen, bounds, start, sweep);
            start += sweep;
        }
    }
}
