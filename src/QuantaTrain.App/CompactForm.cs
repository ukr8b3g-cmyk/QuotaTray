using QuantaTrain.Core;

namespace QuantaTrain.App;

internal sealed class CompactForm : FramelessForm
{
    private const int CompactWidth = 286;
    private const int CompactHeight = 384;
    private const int CardWidth = 266;
    private const int ContentWidth = 246;

    private readonly LocalizationService _localizer;
    private readonly Func<bool> _canDrag;
    private readonly ToolTip _help = UiHelp.Create();
    private readonly RoundedPanel _quotaCard = new();
    private readonly Label _remainingPrefix = new();
    private readonly Label _remainingPercent = new();
    private readonly QuotaProgressBar _progress = new();
    private readonly Label _reset = new();
    private readonly Label _countdown = new();
    private readonly Label _status = new();
    private readonly RoundedPanel _creditsCard = new();
    private readonly Label _creditCount = new();
    private readonly Label _creditExpiry = new();
    private readonly RoundedPanel _historyCard = new();
    private readonly Label _historyValue = new();
    private readonly Button _signIn;

    public CompactForm(LocalizationService localizer, Func<bool>? canDrag = null)
    {
        _localizer = localizer;
        _canDrag = canDrag ?? (() => true);
        Text = "QuantaTray";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        ClientSize = new Size(CompactWidth, CompactHeight);
        MinimumSize = new Size(CompactWidth, CompactHeight);
        StartPosition = FormStartPosition.Manual;
        AccessibleName = "QuantaTray compact quota panel";

        var brand = UiFactory.BrandIcon(new Point(13, 12), 22);
        var title = UiFactory.Label(
            "QuantaTray",
            new Point(42, 11),
            10F,
            FontStyle.Bold);
        title.Size = new Size(126, 25);
        title.AutoSize = false;
        title.TextAlign = ContentAlignment.MiddleLeft;

        var refresh = new IconButton(FluentSymbol.Refresh)
        {
            AccessibleName = _localizer.Text("Common.Refresh"),
            Bounds = new Rectangle(174, 8, 26, 30),
        };
        refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        var mini = new IconButton(FluentSymbol.Compact)
        {
            AccessibleName = _localizer.Text("Menu.ShowMini"),
            Bounds = new Rectangle(200, 8, 26, 30),
        };
        mini.Click += (_, _) => MiniRequested?.Invoke(this, EventArgs.Empty);
        var settings = new IconButton(FluentSymbol.Settings)
        {
            AccessibleName = _localizer.Text("Common.Settings"),
            Bounds = new Rectangle(226, 8, 26, 30),
        };
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        var close = new IconButton(FluentSymbol.Close)
        {
            AccessibleName = _localizer.Text("Common.Close"),
            Bounds = new Rectangle(252, 8, 26, 30),
        };
        close.Click += (_, _) => Hide();
        _help.SetToolTip(refresh, _localizer.Text("Help.Refresh"));
        _help.SetToolTip(mini, _localizer.Text("Help.ShowMini"));
        _help.SetToolTip(settings, _localizer.Text("Help.Settings"));
        _help.SetToolTip(close, _localizer.Text("Help.Close"));

        var menu = BuildCompactMenu();
        ContextMenuStrip = menu;

        _quotaCard.Bounds = new Rectangle(10, 44, CardWidth, 132);
        var weekly = UiFactory.Label(
            _localizer.Text("Quota.Weekly"),
            new Point(10, 4),
            8.7F,
            FontStyle.Bold);
        weekly.AutoSize = false;
        weekly.Size = new Size(ContentWidth, 19);
        weekly.AutoEllipsis = true;

        _remainingPrefix.AutoSize = true;
        _remainingPrefix.Font = Theme.Ui(7.8F);
        _remainingPrefix.ForeColor = Theme.Text;
        _remainingPrefix.BackColor = Color.Transparent;

        _remainingPercent.AutoSize = true;
        _remainingPercent.Font = Theme.Ui(14F, FontStyle.Bold);
        _remainingPercent.ForeColor = Theme.Subtle;
        _remainingPercent.BackColor = Color.Transparent;
        _remainingPercent.Text = "—";

        _progress.Bounds = new Rectangle(10, 58, ContentWidth, 9);
        _reset = UiFactory.Label(string.Empty, new Point(10, 74), 7.7F);
        _reset.AutoSize = false;
        _reset.Size = new Size(ContentWidth, 34);
        _reset.AutoEllipsis = false;
        _reset.TextAlign = ContentAlignment.TopLeft;
        _countdown = UiFactory.Label(string.Empty, new Point(10, 104), 7.1F);
        _countdown.AutoSize = false;
        _countdown.Size = new Size(ContentWidth, 15);
        _countdown.TextAlign = ContentAlignment.TopCenter;
        _status = UiFactory.Label(
            string.Empty,
            new Point(10, 110),
            7.2F,
            FontStyle.Regular,
            Theme.Muted);
        _status.AutoSize = false;
        _status.Size = new Size(ContentWidth, 18);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.AutoEllipsis = true;

        _signIn = UiFactory.TextButton(
            _localizer.Text("Auth.SignIn"),
            new Rectangle(10, 76, ContentWidth, 31),
            primary: true);
        _signIn.Visible = false;
        _signIn.Click += (_, _) => SignInRequested?.Invoke(this, EventArgs.Empty);

        _quotaCard.Controls.AddRange(
        [
            weekly, _remainingPrefix, _remainingPercent, _progress,
            _reset, _countdown, _status, _signIn,
        ]);

        BuildCreditsCard();
        BuildHistoryCard();
        Controls.AddRange(
        [
            brand, title, refresh, mini, settings, close,
            _quotaCard, _creditsCard, _historyCard,
        ]);

        MakeDraggable(this);
        MakeDraggable(brand);
        MakeDraggable(title);
        title.DoubleClick += (_, _) => DetailRequested?.Invoke(this, EventArgs.Empty);
        brand.DoubleClick += (_, _) => DetailRequested?.Invoke(this, EventArgs.Empty);
        AlignRemaining();
    }

    protected override bool CanDrag => _canDrag();

    public event EventHandler? RefreshRequested;
    public event EventHandler? MiniRequested;
    public event EventHandler? DetailRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? SignInRequested;

    public void SetSignedIn(bool signedIn)
    {
        _signIn.Visible = !signedIn;
        _reset.Visible = signedIn;
        _countdown.Visible = signedIn;
    }

    public void UpdateState(
        WeeklyQuotaState? state,
        bool updating,
        string? error,
        IReadOnlyList<string>? history = null)
    {
        if (state is null)
        {
            _remainingPrefix.Text = string.Empty;
            _remainingPercent.Text = "—";
            _remainingPercent.ForeColor = Theme.Subtle;
            _progress.Value = 0;
            _reset.Text = _localizer.Text("Quota.Unavailable");
            _countdown.Text = string.Empty;
            _creditCount.Text = "—";
            _creditExpiry.Text = _localizer.Text("Credits.DetailsUnavailable");
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
            _reset.Text = state.ResetsAtUtc is null
                ? _localizer.Text("Quota.NextReset", "—")
                : $"{_localizer.Text(
                    "Quota.NextReset",
                    state.ResetsAtUtc.Value.ToLocalTime()
                        .ToString("yyyy/MM/dd HH:mm"))} " +
                  $"({FormatCountdown(state.ResetsAtUtc.Value)})";
            _countdown.Text = string.Empty;
            _creditCount.Text = state.ResetCreditCount is > 0
                ? _localizer.Text(
                    "Credits.Available",
                    state.ResetCreditCount.Value)
                : _localizer.Text("Credits.None");
            var earliestExpiry = state.ResetCredits?
                .Where(credit => credit.ExpiresAtUtc is not null)
                .MinBy(credit => credit.ExpiresAtUtc)
                ?.ExpiresAtUtc;
            _creditExpiry.Text = earliestExpiry is null
                ? _localizer.Text("Credits.DetailsUnavailable")
                : _localizer.Text(
                    "Credits.Expires",
                    earliestExpiry.Value.ToLocalTime()
                        .ToString("yyyy/MM/dd HH:mm"));
        }

        _historyValue.Text = CompactHistory(
            history?.FirstOrDefault() ??
            _localizer.Text("History.Empty"));
        _status.Text = updating
            ? _localizer.Text("Status.Updating")
            : error is not null
                ? _localizer.Text("Status.Failed")
                : string.Empty;
        AlignRemaining();
    }

    private void BuildCreditsCard()
    {
        _creditsCard.Bounds = new Rectangle(10, 184, CardWidth, 86);
        var heading = UiFactory.Label(
            _localizer.Text("Credits.Title"),
            new Point(10, 7),
            8.4F,
            FontStyle.Bold);
        heading.AutoSize = false;
        heading.Size = new Size(110, 22);
        _creditCount.Bounds = new Rectangle(120, 7, 136, 22);
        _creditCount.Font = Theme.Ui(7.8F);
        _creditCount.ForeColor = Theme.Text;
        _creditCount.BackColor = Color.Transparent;
        _creditCount.TextAlign = ContentAlignment.MiddleRight;
        _creditCount.AutoEllipsis = true;
        _creditExpiry.Bounds = new Rectangle(10, 38, ContentWidth, 36);
        _creditExpiry.Font = Theme.Ui(8F);
        _creditExpiry.ForeColor = Theme.Text;
        _creditExpiry.BackColor = Color.Transparent;
        _creditExpiry.AutoEllipsis = false;
        _creditExpiry.TextAlign = ContentAlignment.TopLeft;
        _creditsCard.Controls.AddRange(
        [
            heading, _creditCount, _creditExpiry,
        ]);
    }

    private void BuildHistoryCard()
    {
        _historyCard.Bounds = new Rectangle(10, 278, CardWidth, 96);
        var heading = UiFactory.Label(
            _localizer.Text("History.Title"),
            new Point(10, 7),
            7.8F,
            FontStyle.Regular);
        heading.AutoSize = false;
        heading.Size = new Size(156, 22);
        var showAll = UiFactory.TextButton(
            _localizer.Text("History.ShowAll"),
            new Rectangle(176, 4, 80, 25));
        showAll.FlatAppearance.BorderSize = 0;
        showAll.BackColor = Theme.Surface;
        showAll.ForeColor = Theme.Blue;
        showAll.Font = Theme.Ui(7.1F);
        showAll.Click += (_, _) => DetailRequested?.Invoke(this, EventArgs.Empty);
        _historyValue.Bounds = new Rectangle(10, 35, ContentWidth, 50);
        _historyValue.Font = Theme.Ui(8F);
        _historyValue.ForeColor = Theme.Text;
        _historyValue.BackColor = Color.Transparent;
        _historyValue.AutoEllipsis = false;
        _historyCard.Controls.AddRange(
        [
            heading, showAll, _historyValue,
        ]);
    }

    private static string CompactHistory(string value)
    {
        var separator = value.IndexOf("  ", StringComparison.Ordinal);
        return separator > 0
            ? $"{value[..separator]}{Environment.NewLine}" +
              value[(separator + 2)..].Trim()
            : value;
    }

    public void PositionNearTray()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(Cursor.Position);
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
    }

    private ContextMenuStrip BuildCompactMenu()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = Theme.SurfaceRaised,
            ForeColor = Theme.Text,
            Font = Theme.Ui(9F),
            ShowImageMargin = false,
        };
        menu.Items.Add(
            _localizer.Text("Menu.ShowMini"),
            null,
            (_, _) => MiniRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(
            _localizer.Text("Menu.ShowDetail"),
            null,
            (_, _) => DetailRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(
            _localizer.Text("Common.Refresh"),
            null,
            (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(
            _localizer.Text("Common.Settings"),
            null,
            (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        return menu;
    }

    private void AlignRemaining()
    {
        _remainingPercent.Location = new Point(
            _quotaCard.ClientSize.Width - _remainingPercent.PreferredWidth - 13,
            22);
        _remainingPrefix.Location = new Point(
            _remainingPercent.Left - _remainingPrefix.PreferredWidth - 4,
            30);
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
            return "0M";
        }

        return remaining.TotalDays >= 1
            ? $"{(int)remaining.TotalDays}D{remaining.Hours}H"
            : $"{(int)remaining.TotalHours}H{remaining.Minutes}M";
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
