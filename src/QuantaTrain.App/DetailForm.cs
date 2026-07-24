using QuantaTrain.Core;

namespace QuantaTrain.App;

internal sealed class DetailForm : FramelessForm
{
    private readonly LocalizationService _localizer;
    private readonly RoundedPanel _quotaCard = new();
    private readonly Label _remainingPrefix = new();
    private readonly Label _remainingPercent = new();
    private readonly QuotaProgressBar _progress = new();
    private readonly Label _reset;
    private readonly Label _countdown;
    private readonly Label _credits;
    private readonly FlowLayoutPanel _creditExpirations = new();
    private readonly FlowLayoutPanel _history = new();
    private readonly Label _plan;
    private readonly Label _connection;

    public DetailForm(LocalizationService localizer)
    {
        _localizer = localizer;
        Text = $"{_localizer.Text("Menu.ShowDetail")} — QuantaTrain";
        ClientSize = new Size(354, 636);
        StartPosition = FormStartPosition.Manual;
        AccessibleName = "QuantaTrain detailed quota panel";

        var brand = UiFactory.BrandIcon(new Point(14, 15), 25);
        var title = UiFactory.Label(
            "QuantaTrain",
            new Point(47, 15),
            11F,
            FontStyle.Bold);
        title.Size = new Size(176, 28);
        title.AutoSize = false;
        title.TextAlign = ContentAlignment.MiddleLeft;

        var refresh = HeaderButton(
            FluentSymbol.Refresh,
            _localizer.Text("Common.Refresh"),
            248);
        refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        var settings = HeaderButton(
            FluentSymbol.Settings,
            _localizer.Text("Common.Settings"),
            281);
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        var close = HeaderButton(
            FluentSymbol.Close,
            _localizer.Text("Common.Close"),
            314);
        close.Click += (_, _) => Hide();

        _quotaCard.Bounds = new Rectangle(12, 56, 330, 133);
        var quotaHeading = UiFactory.Label(
            _localizer.Text("Quota.Weekly"),
            new Point(12, 13),
            10F,
            FontStyle.Bold);
        _remainingPrefix.AutoSize = true;
        _remainingPrefix.Font = Theme.Ui(8.5F);
        _remainingPrefix.ForeColor = Theme.Text;
        _remainingPrefix.BackColor = Color.Transparent;
        _remainingPercent.AutoSize = true;
        _remainingPercent.Font = Theme.Ui(16F, FontStyle.Bold);
        _remainingPercent.ForeColor = Theme.Subtle;
        _remainingPercent.BackColor = Color.Transparent;
        _remainingPercent.Text = "—";
        _progress.Bounds = new Rectangle(12, 55, 306, 12);
        _reset = UiFactory.Label(string.Empty, new Point(12, 82), 8.7F);
        _countdown = UiFactory.Label(string.Empty, new Point(86, 105), 8.7F);
        _quotaCard.Controls.AddRange(
        [
            quotaHeading, _remainingPrefix, _remainingPercent,
            _progress, _reset, _countdown,
        ]);

        var creditsCard = new RoundedPanel { Bounds = new Rectangle(12, 198, 330, 113) };
        var creditsHeading = UiFactory.Label(
            _localizer.Text("Credits.Title"),
            new Point(12, 12),
            10F,
            FontStyle.Bold);
        _credits = UiFactory.Label(string.Empty, new Point(222, 13), 9F);
        _credits.Size = new Size(96, 22);
        _credits.AutoSize = false;
        _credits.TextAlign = ContentAlignment.TopRight;
        _creditExpirations.Bounds = new Rectangle(12, 42, 306, 60);
        _creditExpirations.FlowDirection = FlowDirection.TopDown;
        _creditExpirations.WrapContents = false;
        _creditExpirations.AutoScroll = false;
        _creditExpirations.BackColor = Theme.Surface;
        _creditExpirations.Margin = Padding.Empty;
        creditsCard.Controls.AddRange([creditsHeading, _credits, _creditExpirations]);

        var historyCard = new RoundedPanel { Bounds = new Rectangle(12, 320, 330, 194) };
        var historyHeading = UiFactory.Label(
            _localizer.Text("History.Title"),
            new Point(12, 12),
            10F,
            FontStyle.Bold);
        var historyLink = UiFactory.Label(
            _localizer.Text("History.ShowAll"),
            new Point(244, 13),
            8.5F,
            FontStyle.Regular,
            Theme.Blue);
        historyLink.Size = new Size(74, 20);
        historyLink.AutoSize = false;
        historyLink.TextAlign = ContentAlignment.TopRight;
        _history.Bounds = new Rectangle(12, 41, 306, 141);
        _history.FlowDirection = FlowDirection.TopDown;
        _history.WrapContents = false;
        _history.AutoScroll = true;
        _history.BackColor = Theme.Surface;
        historyCard.Controls.AddRange([historyHeading, historyLink, _history]);

        var infoCard = new RoundedPanel { Bounds = new Rectangle(12, 523, 330, 101) };
        var infoHeading = UiFactory.Label(
            _localizer.Text("Detail.OtherInformation"),
            new Point(12, 11),
            10F,
            FontStyle.Bold);
        _plan = UiFactory.Label(string.Empty, new Point(12, 43), 8.7F);
        _connection = UiFactory.Label(
            string.Empty,
            new Point(12, 69),
            8.4F,
            FontStyle.Regular,
            Theme.Muted);
        infoCard.Controls.AddRange([infoHeading, _plan, _connection]);

        Controls.AddRange(
        [
            brand, title, refresh, settings, close,
            _quotaCard, creditsCard, historyCard, infoCard,
        ]);
        MakeDraggable(this);
        MakeDraggable(brand);
        MakeDraggable(title);
        title.DoubleClick += (_, _) => CompactRequested?.Invoke(this, EventArgs.Empty);
        AlignRemaining();
        ShowEmptyRows();
    }

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
            var remaining = (int)Math.Round(state.RemainingPercent);
            _remainingPrefix.Text = RemainingPrefix(remaining);
            _remainingPercent.Text = $"{remaining}%";
            _remainingPercent.ForeColor = Theme.QuotaColor(remaining);
            _progress.ValueColor = Theme.QuotaColor(remaining);
            _progress.Value = Math.Clamp(remaining, 0, 100);
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
            rowHeight: 39);

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
            Bounds = new Rectangle(x, 12, 30, 32),
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
            rowHeight: 39);
        _plan.Text = _localizer.Text("Detail.Plan", "—");
        _connection.Text = _localizer.Text("Status.Stale");
    }

    private static void PopulateRows(
        FlowLayoutPanel panel,
        IEnumerable<string> values,
        Color color,
        int rowHeight = 24)
    {
        panel.SuspendLayout();
        panel.Controls.Clear();
        foreach (var value in values)
        {
            var label = new Label
            {
                Text = value,
                Font = Theme.Ui(8.5F),
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

    private void AlignRemaining()
    {
        _remainingPercent.Location = new Point(
            _quotaCard.ClientSize.Width - _remainingPercent.PreferredWidth - 12,
            9);
        _remainingPrefix.Location = new Point(
            _remainingPercent.Left - _remainingPrefix.PreferredWidth - 4,
            18);
    }

    private string RemainingPrefix(int value)
    {
        var formatted = _localizer.Text("Common.Remaining", value);
        var token = value.ToString();
        var index = formatted.IndexOf(token, StringComparison.Ordinal);
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
