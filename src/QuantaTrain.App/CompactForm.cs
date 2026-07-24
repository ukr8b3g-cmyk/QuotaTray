using QuantaTrain.Core;

namespace QuantaTrain.App;

internal sealed class CompactForm : FramelessForm
{
    private readonly LocalizationService _localizer;
    private readonly RoundedPanel _quotaCard = new();
    private readonly Label _remainingPrefix = new();
    private readonly Label _remainingPercent = new();
    private readonly QuotaProgressBar _progress = new();
    private readonly Label _reset = new();
    private readonly Label _countdown = new();
    private readonly Label _status = new();
    private readonly Button _signIn;

    public CompactForm(LocalizationService localizer)
    {
        _localizer = localizer;
        Text = "QuantaTrain";
        ClientSize = new Size(322, 214);
        StartPosition = FormStartPosition.Manual;
        AccessibleName = "QuantaTrain compact quota panel";

        var brand = UiFactory.BrandIcon(new Point(16, 16), 25);
        var title = UiFactory.Label(
            "QuantaTrain",
            new Point(49, 16),
            11F,
            FontStyle.Bold);
        title.Size = new Size(158, 27);
        title.AutoSize = false;
        title.TextAlign = ContentAlignment.MiddleLeft;

        var more = new IconButton(FluentSymbol.More)
        {
            AccessibleName = _localizer.Text("Common.Settings"),
            Bounds = new Rectangle(247, 12, 31, 32),
        };
        var close = new IconButton(FluentSymbol.Close)
        {
            AccessibleName = _localizer.Text("Common.Close"),
            Bounds = new Rectangle(282, 12, 30, 32),
        };
        close.Click += (_, _) => Hide();

        var menu = BuildCompactMenu();
        more.Click += (_, _) => menu.Show(more, new Point(-100, more.Height + 2));

        _quotaCard.Bounds = new Rectangle(12, 55, 298, 147);
        var weekly = UiFactory.Label(
            _localizer.Text("Quota.Weekly"),
            new Point(13, 14),
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

        _progress.Bounds = new Rectangle(13, 57, 272, 12);
        _reset = UiFactory.Label(string.Empty, new Point(13, 86), 8.7F);
        _countdown = UiFactory.Label(string.Empty, new Point(109, 111), 8.7F);
        _status = UiFactory.Label(
            string.Empty,
            new Point(13, 120),
            8F,
            FontStyle.Regular,
            Theme.Muted);

        _signIn = UiFactory.TextButton(
            _localizer.Text("Auth.SignIn"),
            new Rectangle(135, 105, 150, 29),
            primary: true);
        _signIn.Visible = false;
        _signIn.Click += (_, _) => SignInRequested?.Invoke(this, EventArgs.Empty);

        _quotaCard.Controls.AddRange(
        [
            weekly, _remainingPrefix, _remainingPercent, _progress,
            _reset, _countdown, _status, _signIn,
        ]);
        Controls.AddRange([brand, title, more, close, _quotaCard]);

        MakeDraggable(this);
        MakeDraggable(brand);
        MakeDraggable(title);
        title.DoubleClick += (_, _) => DetailRequested?.Invoke(this, EventArgs.Empty);
        brand.DoubleClick += (_, _) => DetailRequested?.Invoke(this, EventArgs.Empty);
        AlignRemaining();
    }

    public event EventHandler? RefreshRequested;
    public event EventHandler? DetailRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? SignInRequested;

    public void SetSignedIn(bool signedIn)
    {
        _signIn.Visible = !signedIn;
        _countdown.Visible = signedIn;
    }

    public void UpdateState(WeeklyQuotaState? state, bool updating, string? error)
    {
        if (state is null)
        {
            _remainingPrefix.Text = string.Empty;
            _remainingPercent.Text = "—";
            _remainingPercent.ForeColor = Theme.Subtle;
            _progress.Value = 0;
            _reset.Text = _localizer.Text("Quota.Unavailable");
            _countdown.Text = string.Empty;
        }
        else
        {
            var remaining = (int)Math.Round(state.RemainingPercent);
            _remainingPrefix.Text = RemainingPrefix(remaining);
            _remainingPercent.Text = $"{remaining}%";
            _remainingPercent.ForeColor = Theme.QuotaColor(remaining);
            _progress.ValueColor = Theme.QuotaColor(remaining);
            _progress.Value = Math.Clamp(remaining, 0, 100);
            _reset.Text = state.ResetsAtUtc is null
                ? _localizer.Text("Quota.NextReset", "—")
                : _localizer.Text(
                    "Quota.NextReset",
                    state.ResetsAtUtc.Value.ToLocalTime().ToString("g"));
            _countdown.Text = state.ResetsAtUtc is null
                ? string.Empty
                : _localizer.Text(
                    "Quota.Countdown",
                    FormatCountdown(state.ResetsAtUtc.Value));
        }

        _status.Text = updating
            ? _localizer.Text("Status.Updating")
            : error is not null
                ? _localizer.Text("Status.Failed")
                : string.Empty;
        _status.Location = _countdown.Text.Length == 0
            ? new Point(13, 112)
            : new Point(13, 126);
        AlignRemaining();
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
            10);
        _remainingPrefix.Location = new Point(
            _remainingPercent.Left - _remainingPrefix.PreferredWidth - 4,
            19);
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
