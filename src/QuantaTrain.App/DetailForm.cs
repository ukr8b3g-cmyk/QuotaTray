using QuantaTrain.Core;

namespace QuantaTrain.App;

internal sealed class DetailForm : FramelessForm
{
    private const int BaseClientHeight = 504;
    private const int HistoryRowHeight = 24;
    private const int MaxVisibleHistoryRows = 3;

    private readonly LocalizationService _localizer;
    private readonly Func<bool> _canDrag;
    private readonly RoundedPanel _quotaCard = new();
    private readonly Label _remainingPrefix = new();
    private readonly Label _remainingPercent = new();
    private readonly QuotaProgressBar _progress = new();
    private readonly Label _reset;
    private readonly Label _countdown;
    private readonly Label _credits;
    private readonly FlowLayoutPanel _creditExpirations = new();
    private readonly RoundedPanel _historyCard = new();
    private readonly FlowLayoutPanel _history = new();
    private readonly RoundedPanel _infoCard = new();
    private readonly Label _plan;
    private readonly Label _connection;

    public DetailForm(LocalizationService localizer, Func<bool>? canDrag = null)
    {
        _localizer = localizer;
        _canDrag = canDrag ?? (() => true);
        Text = $"{_localizer.Text("Menu.ShowDetail")} — QuantaTray";
        ClientSize = new Size(260, BaseClientHeight);
        StartPosition = FormStartPosition.Manual;
        AccessibleName = "QuantaTray detailed quota panel";

        var brand = UiFactory.BrandIcon(new Point(12, 11), 22);
        var title = UiFactory.Label(
            "QuantaTray",
            new Point(41, 10),
            10F,
            FontStyle.Bold);
        title.Size = new Size(88, 25);
        title.AutoSize = false;
        title.TextAlign = ContentAlignment.MiddleLeft;

        var refresh = HeaderButton(
            FluentSymbol.Refresh,
            _localizer.Text("Common.Refresh"),
            131);
        refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        var compact = HeaderButton(
            FluentSymbol.Compact,
            _localizer.Text("Menu.ShowCompact"),
            160);
        compact.Click += (_, _) => CompactRequested?.Invoke(this, EventArgs.Empty);
        var settings = HeaderButton(
            FluentSymbol.Settings,
            _localizer.Text("Common.Settings"),
            189);
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        var close = HeaderButton(
            FluentSymbol.Close,
            _localizer.Text("Common.Close"),
            220);
        close.Click += (_, _) => Hide();

        _quotaCard.Bounds = new Rectangle(10, 48, 240, 133);
        var quotaHeading = UiFactory.Label(
            _localizer.Text("Quota.Weekly"),
            new Point(10, 11),
            8.7F,
            FontStyle.Bold);
        _remainingPrefix.AutoSize = true;
        _remainingPrefix.Font = Theme.Ui(7.8F);
        _remainingPrefix.ForeColor = Theme.Text;
        _remainingPrefix.BackColor = Color.Transparent;
        _remainingPercent.AutoSize = true;
        _remainingPercent.Font = Theme.Ui(14F, FontStyle.Bold);
        _remainingPercent.ForeColor = Theme.Subtle;
        _remainingPercent.BackColor = Color.Transparent;
        _remainingPercent.Text = "—";
        _progress.Bounds = new Rectangle(10, 51, 220, 10);
        _reset = UiFactory.Label(string.Empty, new Point(10, 76), 7.8F);
        _reset.AutoSize = false;
        _reset.Size = new Size(220, 17);
        _reset.AutoEllipsis = true;
        _countdown = UiFactory.Label(string.Empty, new Point(10, 99), 7.8F);
        _countdown.AutoSize = false;
        _countdown.Size = new Size(220, 17);
        _countdown.TextAlign = ContentAlignment.TopCenter;
        _quotaCard.Controls.AddRange(
        [
            quotaHeading, _remainingPrefix, _remainingPercent,
            _progress, _reset, _countdown,
        ]);

        var creditsCard = new RoundedPanel { Bounds = new Rectangle(10, 189, 240, 110) };
        var creditsHeading = UiFactory.Label(
            _localizer.Text("Credits.Title"),
            new Point(10, 10),
            8.7F,
            FontStyle.Bold);
        _credits = UiFactory.Label(string.Empty, new Point(148, 11), 8F);
        _credits.Size = new Size(82, 20);
        _credits.AutoSize = false;
        _credits.TextAlign = ContentAlignment.TopRight;
        _creditExpirations.Bounds = new Rectangle(10, 37, 220, 62);
        _creditExpirations.FlowDirection = FlowDirection.TopDown;
        _creditExpirations.WrapContents = false;
        _creditExpirations.AutoScroll = false;
        _creditExpirations.BackColor = Theme.Surface;
        _creditExpirations.Margin = Padding.Empty;
        creditsCard.Controls.AddRange([creditsHeading, _credits, _creditExpirations]);

        _historyCard.Bounds = new Rectangle(10, 307, 240, 78);
        var historyHeading = UiFactory.Label(
            _localizer.Text("History.Title"),
            new Point(10, 10),
            8.7F,
            FontStyle.Bold);
        var historyLink = UiFactory.Label(
            _localizer.Text("History.ShowAll"),
            new Point(160, 11),
            7.8F,
            FontStyle.Regular,
            Theme.Blue);
        historyLink.Size = new Size(70, 18);
        historyLink.AutoSize = false;
        historyLink.TextAlign = ContentAlignment.TopRight;
        _history.Bounds = new Rectangle(10, 38, 220, 30);
        _history.FlowDirection = FlowDirection.TopDown;
        _history.WrapContents = false;
        _history.AutoScroll = true;
        _history.BackColor = Theme.Surface;
        _historyCard.Controls.AddRange([historyHeading, historyLink, _history]);

        _infoCard.Bounds = new Rectangle(10, 393, 240, 101);
        var infoHeading = UiFactory.Label(
            _localizer.Text("Detail.OtherInformation"),
            new Point(10, 10),
            8.7F,
            FontStyle.Bold);
        _plan = UiFactory.Label(string.Empty, new Point(10, 39), 7.8F);
        _plan.AutoSize = false;
        _plan.Size = new Size(220, 18);
        _plan.AutoEllipsis = true;
        _connection = UiFactory.Label(
            string.Empty,
            new Point(10, 66),
            7.6F,
            FontStyle.Regular,
            Theme.Muted);
        _connection.AutoSize = false;
        _connection.Size = new Size(220, 18);
        _connection.AutoEllipsis = true;
        _infoCard.Controls.AddRange([infoHeading, _plan, _connection]);

        Controls.AddRange(
        [
            brand, title, refresh, compact, settings, close,
            _quotaCard, creditsCard, _historyCard, _infoCard,
        ]);
        MakeDraggable(this);
        MakeDraggable(brand);
        MakeDraggable(title);
        title.DoubleClick += (_, _) => CompactRequested?.Invoke(this, EventArgs.Empty);
        AlignRemaining();
        ShowEmptyRows();
    }

    protected override bool CanDrag => _canDrag();

    public event EventHandler? RefreshRequested;
    public event EventHandler? CompactRequested;
    public event EventHandler? SettingsRequested;

    public void UpdateState(
        WeeklyQuotaState? state,
        bool updating,
        string? error,
        IReadOnlyList<string> history)
    {
        if (state is null)
        {
            _remainingPrefix.Text = string.Empty;
            _remainingPercent.Text = "—";
            _remainingPercent.ForeColor = Theme.Subtle;
            _progress.Value = 0;
            _reset.Text = _localizer.Text("Quota.Unavailable");
            _countdown.Text = string.Empty;
            _credits.Text = _localizer.Text("Credits.Available", 0);
            PopulateRows(
                _creditExpirations,
                [_localizer.Text("Credits.None")],
                Theme.Muted);
            _plan.Text = _localizer.Text("Detail.Plan", "—");
        }
        else
        {
            var remaining = state.RemainingPercent;
            var remainingNumber = QuotaDisplay.Number(remaining);
            _remainingPrefix.Text = RemainingPrefix(remainingNumber);
            _remainingPercent.Text = QuotaDisplay.Percent(remaining);
            _remainingPercent.ForeColor = Theme.QuotaColor(remaining);
            _progress.ValueColor = Theme.QuotaColor(remaining);
            _progress.Value = (int)Math.Round(Math.Clamp(remaining, 0d, 100d));
            _reset.Text = _localizer.Text(
                "Quota.NextReset",
                state.ResetsAtUtc?.ToLocalTime().ToString("g") ?? "—");
            _countdown.Text = state.ResetsAtUtc is null
                ? string.Empty
                : _localizer.Text(
                    "Quota.Countdown",
                    FormatCountdown(state.ResetsAtUtc.Value));
            _credits.Text = _localizer.Text(
                "Credits.Available",
                state.ResetCreditCount ?? 0);

            var expirationRows = state.ResetCredits?
                .Select(credit => _localizer.Text(
                    "Credits.Expires",
                    credit.ExpiresAtUtc?.ToLocalTime().ToString("g") ?? "—"))
                .ToArray() ?? [];
            PopulateRows(
                _creditExpirations,
                expirationRows.Length == 0
                    ? [_localizer.Text("Credits.None")]
                    : expirationRows,
                expirationRows.Length == 0 ? Theme.Muted : Theme.Text);
            _plan.Text = _localizer.Text(
                "Detail.Plan",
                string.IsNullOrWhiteSpace(state.PlanType) ? "—" : state.PlanType);
        }

        PopulateRows(
            _history,
            history.Count == 0 ? [_localizer.Text("History.Empty")] : history,
            history.Count == 0 ? Theme.Muted : Theme.Text,
            rowHeight: HistoryRowHeight);
        AdjustHistoryLayout(history.Count);

        _connection.Text = updating
            ? _localizer.Text("Status.Updating")
            : error is not null
                ? _localizer.Text("Status.Failed")
                : _localizer.Text("Status.Latest");
        AlignRemaining();
    }

    public void PositionNearTray()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(Cursor.Position);
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
    }

    private IconButton HeaderButton(string symbol, string accessibleName, int x) =>
        new(symbol)
        {
            AccessibleName = accessibleName,
            Bounds = new Rectangle(x, 8, 28, 30),
        };

    private void ShowEmptyRows()
    {
        _credits.Text = _localizer.Text("Credits.Available", 0);
        PopulateRows(
            _creditExpirations,
            [_localizer.Text("Credits.None")],
            Theme.Muted);
        PopulateRows(
            _history,
            [_localizer.Text("History.Empty")],
            Theme.Muted,
            rowHeight: HistoryRowHeight);
        AdjustHistoryLayout(0);
        _plan.Text = _localizer.Text("Detail.Plan", "—");
        _connection.Text = _localizer.Text("Status.Stale");
    }

    private static void PopulateRows(
        FlowLayoutPanel panel,
        IEnumerable<string> values,
        Color color,
        int rowHeight = 22)
    {
        panel.SuspendLayout();
        panel.Controls.Clear();
        foreach (var value in values)
        {
            var label = new Label
            {
                Text = value,
                Font = Theme.Ui(7.7F),
                ForeColor = color,
                BackColor = Theme.Surface,
                Margin = Padding.Empty,
                Padding = new Padding(0, 3, 0, 0),
                Size = new Size(panel.ClientSize.Width - 4, rowHeight),
                AutoEllipsis = true,
            };
            panel.Controls.Add(label);
        }
        panel.ResumeLayout();
    }

    private void AdjustHistoryLayout(int historyCount)
    {
        var visibleRows = Math.Clamp(historyCount, 1, MaxVisibleHistoryRows);
        var extraHeight = (visibleRows - 1) * HistoryRowHeight;
        _historyCard.Height = 78 + extraHeight;
        _history.Height = 30 + extraHeight;
        _infoCard.Top = 393 + extraHeight;
        ClientSize = new Size(260, BaseClientHeight + extraHeight);
    }

    private void AlignRemaining()
    {
        _remainingPercent.Location = new Point(
            _quotaCard.ClientSize.Width - _remainingPercent.PreferredWidth - 12,
            5);
        _remainingPrefix.Location = new Point(
            _remainingPercent.Left - _remainingPrefix.PreferredWidth - 4,
            14);
    }

    private string RemainingPrefix(string value)
    {
        var formatted = _localizer.Text("Common.Remaining", value);
        var index = formatted.IndexOf(value, StringComparison.Ordinal);
        return index <= 0 ? string.Empty : formatted[..index].Trim();
    }

    private static string FormatCountdown(DateTimeOffset resetAtUtc)
    {
        var remaining = resetAtUtc - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return "0m";
        }

        return remaining.TotalDays >= 1
            ? $"{(int)remaining.TotalDays}d {remaining.Hours}h"
            : $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
    }
}
