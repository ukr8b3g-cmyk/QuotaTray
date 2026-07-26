using System.ComponentModel;
using System.Drawing.Drawing2D;
using QuantaTrain.Core;

namespace QuantaTrain.App;

internal sealed class DetailForm : FixedWidthResizableForm
{
    private readonly LocalizationService _localizer;
    private readonly Func<bool> _canDrag;
    private readonly Func<int> _refreshIntervalSeconds;
    private readonly Panel _overviewPage;
    private readonly Panel _usagePage;
    private readonly Button _overviewTab;
    private readonly Button _usageTab;
    private readonly Panel _tabUnderline;
    private readonly QuotaRingControl _quotaRing = new();
    private readonly QuotaSplitProgressBar _quotaSplitBar = new();
    private readonly Label _usedShare = ValueLabel();
    private readonly Label _remainingShare = ValueLabel();
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
    private readonly Panel _tokenDetails = DetailsPanel();
    private readonly Panel _timeDetails = DetailsPanel();
    private readonly UsageDonutControl _reasoningDonut = new();
    private readonly Panel _reasoningLegend = DetailsPanel();
    private readonly ComboBox _period = new();
    private readonly ComboBox _metric = new();
    private bool _syncingUsageFilters;

    public DetailForm(
        LocalizationService localizer,
        Func<bool>? canDrag = null,
        Func<int>? refreshIntervalSeconds = null)
    {
        _localizer = localizer;
        _canDrag = canDrag ?? (() => true);
        _refreshIntervalSeconds = refreshIntervalSeconds ?? (() => 60);
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
        title.Size = new Size(278, 30);
        title.TextAlign = ContentAlignment.MiddleLeft;
        var mini = HeaderButton(
            _localizer.Text("Menu.ShowMini"),
            334,
            78);
        mini.Click += (_, _) => MiniRequested?.Invoke(this, EventArgs.Empty);
        var compact = HeaderButton(
            _localizer.Text("Menu.ShowCompact"),
            414,
            116);
        compact.Click += (_, _) =>
            CompactRequested?.Invoke(this, EventArgs.Empty);
        var refresh = HeaderButton(
            _localizer.Text("Common.Refresh"),
            532,
            102,
            FluentSymbol.Refresh);
        refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        var settings = HeaderButton(
            _localizer.Text("Common.Settings"),
            636,
            100,
            FluentSymbol.Settings);
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        var close = HeaderButton("×", 748, 34);
        close.Click += (_, _) => Hide();
        header.Controls.AddRange(
        [
            brand, title, mini, compact, refresh, settings, close,
        ]);
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
        _usageTab.Click += (_, _) =>
        {
            SelectTab(usage: true);
            UsageViewRequested?.Invoke(this, EventArgs.Empty);
        };
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
    public event EventHandler? UsageViewRequested;
    public event EventHandler? MiniRequested;
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
        var remainingPercent = state?.RemainingPercent ?? 0d;
        _quotaSplitBar.RemainingPercent = state?.RemainingPercent;
        _usedShare.Text = state is null
            ? "—"
            : _localizer.Text(
                "Quota.UsedShare",
                Math.Round(100d - remainingPercent));
        _remainingShare.Text = state is null
            ? "—"
            : _localizer.Text(
                "Quota.RemainingShare",
                Math.Round(remainingPercent));
        _usedShare.ForeColor = state is null ? Theme.Muted : Theme.UsedQuota;
        _remainingShare.ForeColor = state is null
            ? Theme.Muted
            : QuotaRingControl.RemainingColor(remainingPercent);
        _quotaRing.Caption = _localizer.Text("Common.Remaining", string.Empty)
            .Trim();
        _quotaRing.Subcaption = state?.ResetsAtUtc is null
            ? string.Empty
            : _localizer.Text(
                "Quota.ApproximateRemaining",
                FormatCountdown(state.ResetsAtUtc.Value));
        _resetAt.Text = state?.ResetsAtUtc?.ToLocalTime()
            .ToString("yyyy/MM/dd (ddd) HH:mm") ?? "—";
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
        var connectionText = error is not null
            ? _localizer.Text("Status.Failed")
            : updating
                ? _localizer.Text("Status.Updating")
                : state is null
                    ? _localizer.Text("Status.Stale")
                    : _localizer.Text("Status.Healthy");
        _connection.Text = $"● {connectionText}";
        _connection.ForeColor = error is not null
            ? Theme.Red
            : updating
                ? Theme.Yellow
                : state is null
                    ? Theme.Muted
                    : Theme.Green;
        _lastUpdated.Text = state?.ObservedAtUtc.ToLocalTime().ToString("G") ?? "—";
        _nextRefresh.Text = updating
            ? _localizer.Text("Status.Updating")
            : _localizer.Text(
                "Detail.RefreshSeconds",
                _refreshIntervalSeconds());
        _version.Text =
            typeof(DetailForm).Assembly.GetName().Version?.ToString(3) ?? "—";
        _status.Text = string.Join(
            "  |  ",
            [
                $"{_localizer.Text("Detail.DataSource")}: Codex App Server",
                $"{_localizer.Text("Detail.LastUpdated")}: {_lastUpdated.Text}",
                $"{_localizer.Text("Detail.AutoRefresh")}: {_nextRefresh.Text}",
                $"{_localizer.Text("Detail.ConnectionLabel")}: {_connection.Text}",
            ]);
        PopulateCreditCards(state);
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
            PopulatePlaceholder(
                _tokenDetails,
                _localizer.Text("Usage.EnableInSettings"));
            PopulatePlaceholder(_timeDetails, "—");
            PopulatePlaceholder(_reasoningLegend, "—");
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
        PopulateTokenDetails(tokens);
        var elapsed = summaries.Sum(summary => summary.ElapsedMilliseconds);
        var turns = summaries.Sum(summary => summary.TurnCount);
        PopulateTimeDetails(elapsed, turns, tokens);
        var reasoning = summaries
            .SelectMany(summary => summary.ReasoningTokens)
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Value));
        _reasoningDonut.CenterCaption = _localizer.Text("Common.Total");
        _reasoningDonut.SetValues(reasoning);
        PopulateReasoningLegend(reasoning, total);
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
        var quota = Card(new Rectangle(16, 10, 362, 300), "Quota.Weekly");
        _quotaRing.Bounds = new Rectangle(18, 55, 174, 174);
        quota.Controls.Add(_quotaRing);
        AddKeyValue(quota, "Quota.NextResetAt", _resetAt, 212, 64);
        _resetAt.Font = Theme.Ui(11.5F, FontStyle.Bold);
        _resetAt.Height = 34;
        AddKeyValue(quota, "Quota.WindowStartedAt", _windowStart, 212, 142);
        _usedShare.Bounds = new Rectangle(18, 244, 154, 19);
        _usedShare.AutoSize = false;
        _usedShare.Font = Theme.Ui(7.7F, FontStyle.Bold);
        _remainingShare.Bounds = new Rectangle(172, 244, 172, 19);
        _remainingShare.AutoSize = false;
        _remainingShare.Font = Theme.Ui(7.7F, FontStyle.Bold);
        _remainingShare.TextAlign = ContentAlignment.MiddleRight;
        _quotaSplitBar.Bounds = new Rectangle(18, 270, 326, 12);
        _quotaSplitBar.Anchor =
            AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        quota.Controls.AddRange(
        [
            _usedShare, _remainingShare, _quotaSplitBar,
        ]);

        var info = Card(
            new Rectangle(16, 320, 362, 134),
            "Detail.PlanConnection");
        AddInlineKeyValue(info, "Detail.PlanLabel", _plan, 18, 50, 116);
        AddInlineKeyValue(
            info,
            "Detail.ConnectionLabel",
            _connection,
            18,
            82,
            116);
        AddInlineKeyValue(info, "Detail.AppVersion", _version, 18, 108, 116);

        var credits = Card(
            new Rectangle(390, 10, 376, 164),
            "Credits.Title");
        _credits.Bounds = new Rectangle(18, 45, 340, 104);
        _credits.FlowDirection = FlowDirection.LeftToRight;
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
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
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
        token.Controls.Add(_tokenDetails);

        var time = Card(
            new Rectangle(268, 314, 228, 160),
            "Usage.TimeTurnSummary");
        _timeDetails.Bounds = new Rectangle(16, 42, 196, 108);
        time.Controls.Add(_timeDetails);

        var reasoning = Card(
            new Rectangle(506, 314, 260, 160),
            "Usage.ReasoningBreakdown");
        _reasoningDonut.Bounds = new Rectangle(16, 45, 112, 112);
        _reasoningLegend.Bounds = new Rectangle(138, 46, 106, 100);
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
        for (var index = 0; index < summaries.Count; index++)
        {
            var summary = summaries[index];
            var modelColor = UsageVisuals.ModelColor(
                index,
                string.Equals(
                    summary.Model,
                    "other",
                    StringComparison.OrdinalIgnoreCase));
            var row = new Panel
            {
                Width = 718,
                Height = 22,
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
                BackColor = modelColor,
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
                Height = 25,
                Margin = new Padding(0, 3, 0, 0),
                BackColor = Theme.SurfaceRaised,
            };
            var divider = new Panel
            {
                Bounds = new Rectangle(0, 0, 718, 1),
                BackColor = Theme.Border,
            };
            var totalLabel = Caption(_localizer.Text("Common.Total"), 0, 4);
            totalLabel.Font = Theme.Ui(8.2F, FontStyle.Bold);
            totalLabel.Size = new Size(260, 19);
            var share = Caption("100%", 270, 4);
            share.Size = new Size(52, 19);
            var tokens = Caption(
                $"{summaries.Sum(item => item.Tokens.EffectiveTotalTokens):N0}",
                326,
                4);
            tokens.Size = new Size(96, 19);
            var elapsed = Caption(
                FormatDuration(
                    summaries.Sum(item => item.ElapsedMilliseconds)),
                428,
                4);
            elapsed.Size = new Size(72, 19);
            var turns = Caption(
                $"{summaries.Sum(item => item.TurnCount):N0}",
                504,
                4);
            turns.Size = new Size(42, 19);
            totalRow.Controls.AddRange(
            [
                divider, totalLabel, share, tokens, elapsed, turns,
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

    private void AddInlineKeyValue(
        Control parent,
        string key,
        Label value,
        int x,
        int y,
        int valueX)
    {
        var caption = Caption(_localizer.Text(key), x, y);
        caption.ForeColor = Theme.Muted;
        caption.AutoSize = false;
        caption.Size = new Size(valueX - x - 8, 23);
        value.Location = new Point(valueX, y);
        value.Size = new Size(parent.Width - valueX - 16, 23);
        value.AutoSize = false;
        value.TextAlign = ContentAlignment.MiddleLeft;
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

    private static Panel DetailsPanel() =>
        new()
        {
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

    private Button HeaderButton(
        string text,
        int x,
        int width,
        string? symbol = null)
    {
        var button = symbol is null
            ? ActionButton(text)
            : new IconTextButton(symbol, text);
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
            var (symbol, color) = HistoryVisual(value);
            var row = new Panel
            {
                BackColor = Theme.Surface,
                Margin = new Padding(0, 0, 0, 4),
                Size = new Size(panel.ClientSize.Width - 2, 39),
            };
            var icon = new Label
            {
                Text = symbol,
                Font = new Font("Segoe Fluent Icons", 8.5F),
                ForeColor = color,
                BackColor = Theme.SurfaceRaised,
                Bounds = new Rectangle(7, 6, 25, 25),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            var text = new Label
            {
                Text = value,
                Font = Theme.Ui(8.4F),
                ForeColor = Theme.Text,
                BackColor = Theme.Surface,
                Bounds = new Rectangle(
                    39,
                    3,
                    Math.Max(0, panel.ClientSize.Width - 48),
                    33),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
            };
            row.Controls.AddRange([icon, text]);
            panel.Controls.Add(row);
        }
        panel.ResumeLayout();
    }

    private static (string Symbol, Color Color) HistoryVisual(string value)
    {
        if (value.Contains(
                "unexpected-reset-candidate",
                StringComparison.OrdinalIgnoreCase))
        {
            return (FluentSymbol.Warning, Theme.Yellow);
        }

        if (value.Contains(
                "reset-credit-likely",
                StringComparison.OrdinalIgnoreCase))
        {
            return (FluentSymbol.Refresh, Theme.Blue);
        }

        if (value.Contains(
                "scheduled-reset",
                StringComparison.OrdinalIgnoreCase))
        {
            return (FluentSymbol.CheckMark, Theme.Green);
        }

        return (FluentSymbol.History, Theme.Muted);
    }

    private void PopulateTokenDetails(UsageTokenTotals tokens)
    {
        _tokenDetails.SuspendLayout();
        _tokenDetails.Controls.Clear();
        var rows = new (string Label, long Value, Color Color)[]
        {
            (_localizer.Text("Usage.InputTokens"), tokens.InputTokens, UsageVisuals.TokenColor(0)),
            (_localizer.Text("Usage.CachedInput"), tokens.CachedInputTokens, UsageVisuals.TokenColor(1)),
            (_localizer.Text("Usage.CacheWrite"), tokens.CacheWriteInputTokens, UsageVisuals.TokenColor(2)),
            (_localizer.Text("Usage.OutputTokens"), tokens.OutputTokens, UsageVisuals.TokenColor(3)),
            (_localizer.Text("Usage.ReasoningTokens"), tokens.ReasoningOutputTokens, UsageVisuals.TokenColor(4)),
        };
        for (var index = 0; index < rows.Length; index++)
        {
            AddLegendValueRow(
                _tokenDetails,
                rows[index].Label,
                $"{rows[index].Value:N0}",
                rows[index].Color,
                index * 17,
                210);
        }

        _tokenDetails.Controls.Add(new Panel
        {
            Bounds = new Rectangle(0, 86, 210, 1),
            BackColor = Theme.Border,
        });
        AddValueRow(
            _tokenDetails,
            _localizer.Text("Common.Total"),
            $"{tokens.EffectiveTotalTokens:N0}",
            90,
            210,
            bold: true);
        _tokenDetails.ResumeLayout();
    }

    private void PopulateTimeDetails(
        long elapsed,
        long turns,
        UsageTokenTotals tokens)
    {
        _timeDetails.SuspendLayout();
        _timeDetails.Controls.Clear();
        var rows = new (string Symbol, string Label, string Value, Color Color)[]
        {
            (FluentSymbol.Clock, _localizer.Text("Usage.TotalTime"), FormatDuration(elapsed), Theme.Blue),
            (FluentSymbol.Timer, _localizer.Text("Usage.AverageTurnTime"), FormatDuration(turns == 0 ? 0 : elapsed / turns), Theme.Blue),
            (FluentSymbol.Turn, _localizer.Text("Usage.TurnCount"), $"{turns:N0}", Color.FromArgb(77, 169, 224)),
            (FluentSymbol.Compact, _localizer.Text("Usage.AverageInput"), $"{(turns == 0 ? 0 : tokens.InputTokens / turns):N0}", Color.FromArgb(171, 94, 230)),
            (FluentSymbol.Compact, _localizer.Text("Usage.AverageOutput"), $"{(turns == 0 ? 0 : tokens.OutputTokens / turns):N0}", Color.FromArgb(171, 94, 230)),
        };
        for (var index = 0; index < rows.Length; index++)
        {
            AddIconValueRow(
                _timeDetails,
                rows[index].Symbol,
                rows[index].Label,
                rows[index].Value,
                rows[index].Color,
                index * 21,
                196);
        }
        _timeDetails.ResumeLayout();
    }

    private void PopulateReasoningLegend(
        IReadOnlyDictionary<string, long> reasoning,
        long total)
    {
        _reasoningLegend.SuspendLayout();
        _reasoningLegend.Controls.Clear();
        var rows = reasoning
            .OrderByDescending(item => item.Value)
            .Take(5)
            .ToArray();
        for (var index = 0; index < rows.Length; index++)
        {
            AddLegendValueRow(
                _reasoningLegend,
                DisplayEffort(rows[index].Key),
                $"{Percent(rows[index].Value, total):0}%",
                UsageVisuals.ReasoningColor(index),
                index * 19,
                106,
                labelWidth: 54);
        }
        if (rows.Length == 0)
        {
            PopulatePlaceholder(_reasoningLegend, "—");
        }
        _reasoningLegend.ResumeLayout();
    }

    private static void PopulatePlaceholder(Control panel, string text)
    {
        panel.Controls.Clear();
        panel.Controls.Add(new Label
        {
            Text = text,
            Bounds = panel.ClientRectangle,
            AutoSize = false,
            ForeColor = Theme.Muted,
            BackColor = Theme.Surface,
            Font = Theme.Ui(8F),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        });
    }

    private static void AddLegendValueRow(
        Control parent,
        string label,
        string value,
        Color color,
        int y,
        int width,
        int labelWidth = 116)
    {
        parent.Controls.Add(new Panel
        {
            Bounds = new Rectangle(0, y + 5, 6, 8),
            BackColor = color,
        });
        AddValueRow(
            parent,
            label,
            value,
            y,
            width,
            labelX: 13,
            labelWidth: labelWidth);
    }

    private static void AddIconValueRow(
        Control parent,
        string symbol,
        string label,
        string value,
        Color color,
        int y,
        int width)
    {
        parent.Controls.Add(new Label
        {
            Text = symbol,
            Bounds = new Rectangle(0, y, 15, 18),
            AutoSize = false,
            Font = new Font("Segoe Fluent Icons", 8.3F),
            ForeColor = color,
            BackColor = Theme.Surface,
            TextAlign = ContentAlignment.MiddleCenter,
        });
        AddValueRow(
            parent,
            label,
            value,
            y,
            width,
            labelX: 19,
            labelWidth: 116);
    }

    private static void AddValueRow(
        Control parent,
        string label,
        string value,
        int y,
        int width,
        bool bold = false,
        int labelX = 0,
        int labelWidth = 124)
    {
        var style = bold ? FontStyle.Bold : FontStyle.Regular;
        parent.Controls.Add(new Label
        {
            Text = label,
            Bounds = new Rectangle(labelX, y, labelWidth, 18),
            AutoSize = false,
            Font = Theme.Ui(7.7F, style),
            ForeColor = Theme.Text,
            BackColor = Theme.Surface,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        });
        parent.Controls.Add(new Label
        {
            Text = value,
            Bounds = new Rectangle(
                labelX + labelWidth,
                y,
                Math.Max(0, width - labelX - labelWidth),
                18),
            AutoSize = false,
            Font = Theme.Ui(7.7F, style),
            ForeColor = Theme.Text,
            BackColor = Theme.Surface,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true,
        });
    }

    private void PopulateCreditCards(WeeklyQuotaState? state)
    {
        _credits.SuspendLayout();
        _credits.Controls.Clear();
        var details = state?.ResetCredits?.Take(3).ToArray() ?? [];
        if (details.Length > 0)
        {
            foreach (var credit in details)
            {
                _credits.Controls.Add(
                    CreditCard(
                        1,
                        credit.ExpiresAtUtc?.ToLocalTime()));
            }
        }
        else if (state?.ResetCreditCount is > 0)
        {
            _credits.Controls.Add(
                CreditCard(state.ResetCreditCount.Value, null));
        }
        else
        {
            _credits.Controls.Add(new Label
            {
                Text = _localizer.Text("Credits.None"),
                Font = Theme.Ui(8.4F),
                ForeColor = Theme.Muted,
                BackColor = Theme.Surface,
                Margin = Padding.Empty,
                Padding = new Padding(8, 9, 8, 0),
                Size = new Size(338, 38),
                AutoEllipsis = true,
            });
        }
        _credits.ResumeLayout();
    }

    private RoundedPanel CreditCard(
        long count,
        DateTimeOffset? expiresAt)
    {
        var card = new RoundedPanel
        {
            Size = new Size(106, 96),
            Margin = new Padding(0, 0, 8, 0),
            BackColor = Theme.SurfaceRaised,
        };
        var available = UiFactory.Label(
            _localizer.Text("Credits.Available", count),
            new Point(9, 8),
            8.4F,
            FontStyle.Bold,
            Theme.Yellow);
        available.AutoSize = false;
        available.Size = new Size(88, 21);
        var expires = UiFactory.Label(
            expiresAt is null
                ? _localizer.Text("Credits.DetailsUnavailable")
                : expiresAt.Value.ToString("yyyy/MM/dd"),
            new Point(9, 34),
            7.8F,
            FontStyle.Regular,
            Theme.Text);
        expires.AutoSize = false;
        expires.Size = new Size(88, 22);
        expires.TextAlign = ContentAlignment.MiddleLeft;
        var active = UiFactory.Label(
            _localizer.Text("Credits.Active"),
            new Point(9, 65),
            7.7F,
            FontStyle.Bold,
            Theme.Green);
        active.AutoSize = false;
        active.Size = new Size(88, 20);
        card.Controls.AddRange([available, expires, active]);
        return card;
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

    private string FormatCountdown(DateTimeOffset resetAtUtc)
    {
        var remaining = resetAtUtc - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return "0m";
        }

        if (_localizer.CurrentLocale == "ja-JP")
        {
            return remaining.TotalDays >= 1
                ? $"{(int)remaining.TotalDays}日 {remaining.Hours}時間"
                : $"{(int)remaining.TotalHours}時間 {remaining.Minutes}分";
        }

        return remaining.TotalDays >= 1
            ? $"{(int)remaining.TotalDays}d {remaining.Hours}h"
            : $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
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

internal static class UsageVisuals
{
    private static readonly Color[] ModelPalette =
    [
        Color.FromArgb(55, 132, 232),
        Color.FromArgb(118, 67, 190),
        Color.FromArgb(239, 143, 52),
        Color.FromArgb(63, 177, 207),
        Color.FromArgb(78, 190, 101),
        Color.FromArgb(211, 92, 139),
        Color.FromArgb(222, 187, 53),
        Color.FromArgb(103, 126, 231),
    ];

    private static readonly Color[] TokenPalette =
    [
        Color.FromArgb(55, 132, 232),
        Color.FromArgb(71, 183, 193),
        Color.FromArgb(76, 180, 82),
        Color.FromArgb(239, 143, 52),
        Color.FromArgb(221, 98, 95),
    ];

    private static readonly Color[] ReasoningPalette =
    [
        Theme.Green,
        Theme.Blue,
        Color.FromArgb(235, 139, 53),
        Color.FromArgb(165, 88, 213),
        Color.FromArgb(211, 92, 139),
        Color.FromArgb(71, 183, 193),
    ];

    public static Color ModelColor(int index, bool isOther = false) =>
        isOther
            ? Theme.Subtle
            : ModelPalette[Math.Abs(index) % ModelPalette.Length];

    public static Color TokenColor(int index) =>
        TokenPalette[Math.Abs(index) % TokenPalette.Length];

    public static Color ReasoningColor(int index) =>
        ReasoningPalette[Math.Abs(index) % ReasoningPalette.Length];
}

internal sealed class QuotaRingControl : Control
{
    private double? _remainingPercent;
    private readonly Label _valueLabel;
    private readonly Label _subcaptionLabel;

    public QuotaRingControl()
    {
        DoubleBuffered = true;
        BackColor = Theme.Surface;
        _valueLabel = new Label
        {
            AutoSize = false,
            Font = Theme.Ui(20F, FontStyle.Bold),
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Text = "—",
            TextAlign = ContentAlignment.MiddleCenter,
        };
        _subcaptionLabel = new Label
        {
            AutoSize = false,
            Font = Theme.Ui(7.7F),
            ForeColor = Theme.Muted,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.AddRange([_valueLabel, _subcaptionLabel]);
        UpdateLabelBounds();
    }

    public event EventHandler? ValueChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption { get; set; } = string.Empty;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Subcaption
    {
        get => _subcaptionLabel.Text;
        set => _subcaptionLabel.Text = value;
    }

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

    protected override void OnSizeChanged(EventArgs eventArgs)
    {
        base.OnSizeChanged(eventArgs);
        UpdateLabelBounds();
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
            var remainingSweep = (float)(359.9 * Math.Clamp(
                RemainingPercent.Value,
                0,
                100) / 100d);
            using var remaining = new Pen(
                RemainingColor(RemainingPercent.Value),
                12)
            {
                StartCap = LineCap.Flat,
                EndCap = LineCap.Flat,
            };
            eventArgs.Graphics.DrawArc(
                remaining,
                bounds,
                -90,
                remainingSweep);
            using var used = new Pen(Theme.UsedQuota, 12)
            {
                StartCap = LineCap.Flat,
                EndCap = LineCap.Flat,
            };
            eventArgs.Graphics.DrawArc(
                used,
                bounds,
                -90 + remainingSweep,
                359.9F - remainingSweep);
        }
        var captionBounds = new Rectangle(0, Height / 2 - 52, Width, 18);
        using var captionFont = Theme.Ui(7.5F);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            Caption,
            captionFont,
            captionBounds,
            Theme.Muted,
            TextFormatFlags.HorizontalCenter);
    }

    private void UpdateLabelBounds()
    {
        _valueLabel.Bounds = new Rectangle(12, Height / 2 - 25, Width - 24, 42);
        _subcaptionLabel.Bounds =
            new Rectangle(12, Height / 2 + 17, Width - 24, 24);
    }

    public static Color RemainingColor(double remainingPercent) =>
        remainingPercent <= 10d
            ? Theme.Red
            : remainingPercent <= 30d
                ? Theme.Orange
                : Theme.Accent;
}

internal sealed class UsageDonutControl : Control
{
    private IReadOnlyList<long> _values = [];

    public UsageDonutControl()
    {
        DoubleBuffered = true;
        BackColor = Theme.Surface;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string CenterCaption { get; set; } = string.Empty;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long Total => _values.Sum();

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
            using var pen = new Pen(
                UsageVisuals.ReasoningColor(index),
                14);
            eventArgs.Graphics.DrawArc(pen, bounds, start, sweep);
            start += sweep;
        }

        using var captionFont = Theme.Ui(6.7F);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            CenterCaption,
            captionFont,
            new Rectangle(18, Height / 2 - 17, Width - 36, 16),
            Theme.Muted,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding);
        using var totalFont = Theme.Ui(7.3F, FontStyle.Bold);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            total.ToString("N0"),
            totalFont,
            new Rectangle(12, Height / 2 - 1, Width - 24, 20),
            Theme.Text,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding);
    }
}
