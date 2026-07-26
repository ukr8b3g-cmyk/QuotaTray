using System.Runtime.InteropServices;
using QuantaTrain.Core;

namespace QuantaTrain.App;

internal sealed class MiniForm : FramelessForm
{
    private const int GwlExStyle = -20;
    private const long WsExLayered = 0x00080000L;
    private const long WsExTransparent = 0x00000020L;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private static readonly nint HwndTopMost = new(-1);

    private readonly LocalizationService _localizer;
    private readonly Func<bool> _canDrag;
    private readonly RoundedPanel _quotaCard = new();
    private readonly Label _weekly;
    private readonly Label _remainingPrefix = new();
    private readonly Label _remainingPercent = new();
    private readonly QuotaProgressBar _progress = new();
    private readonly Label _reset;
    private readonly Label _countdown;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _clickThroughItem;
    private bool _clickThrough;

    public MiniForm(LocalizationService localizer, Func<bool>? canDrag = null)
    {
        _localizer = localizer;
        _canDrag = canDrag ?? (() => true);
        Text = $"{_localizer.Text("Menu.ShowMini")} — QuantaTray";
        ClientSize = new Size(220, 95);
        StartPosition = FormStartPosition.Manual;
        AccessibleName = "QuantaTray mini quota panel";

        _quotaCard.Bounds = new Rectangle(6, 6, 208, 83);
        _weekly = UiFactory.Label(
            _localizer.Text("Quota.Weekly"),
            new Point(10, 4),
            8.2F,
            FontStyle.Bold);
        _weekly.AutoSize = false;
        _weekly.Size = new Size(80, 18);
        _weekly.AutoEllipsis = true;

        _remainingPrefix.AutoSize = true;
        _remainingPrefix.Font = Theme.Ui(7.8F);
        _remainingPrefix.ForeColor = Theme.Text;
        _remainingPrefix.BackColor = Color.Transparent;

        _remainingPercent.AutoSize = true;
        _remainingPercent.Font = Theme.Ui(14F, FontStyle.Bold);
        _remainingPercent.ForeColor = Theme.Subtle;
        _remainingPercent.BackColor = Color.Transparent;
        _remainingPercent.Text = "—";

        _progress.Bounds = new Rectangle(10, 39, 188, 9);
        _reset = UiFactory.Label(string.Empty, new Point(10, 57), 7F);
        _reset.AutoSize = false;
        _reset.Size = new Size(68, 16);
        _reset.AutoEllipsis = true;
        _countdown = UiFactory.Label(string.Empty, new Point(82, 57), 7F);
        _countdown.AutoSize = false;
        _countdown.Size = new Size(116, 16);
        _countdown.TextAlign = ContentAlignment.TopLeft;
        _countdown.AutoEllipsis = true;

        _quotaCard.Controls.AddRange(
        [
            _weekly,
            _remainingPrefix,
            _remainingPercent,
            _progress,
            _reset,
            _countdown,
        ]);
        Controls.Add(_quotaCard);

        _menu = new ContextMenuStrip
        {
            BackColor = Theme.SurfaceRaised,
            ForeColor = Theme.Text,
        };
        _menu.Items.Add(
            _localizer.Text("Menu.ShowCompact"),
            null,
            (_, _) => CompactRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add(
            _localizer.Text("Menu.ShowDetail"),
            null,
            (_, _) => DetailRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add(
            _localizer.Text("Common.Refresh"),
            null,
            (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add(new ToolStripSeparator());
        _clickThroughItem = new ToolStripMenuItem(
            _localizer.Text("Settings.MiniClickThrough"))
        {
            CheckOnClick = true,
        };
        _clickThroughItem.Click += (_, _) =>
            ClickThroughRequested?.Invoke(
                this,
                new MiniClickThroughEventArgs(_clickThroughItem.Checked));
        _menu.Items.Add(_clickThroughItem);
        _menu.Items.Add(
            _localizer.Text("Common.Settings"),
            null,
            (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        ContextMenuStrip = _menu;

        foreach (var control in DraggableControls())
        {
            MakeDraggable(control);
            control.DoubleClick += (_, _) =>
                CompactRequested?.Invoke(this, EventArgs.Empty);
        }
        AlignResetLine();
        AlignRemaining();
    }

    public event EventHandler? CompactRequested;
    public event EventHandler? DetailRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler<MiniClickThroughEventArgs>? ClickThroughRequested;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            if (_clickThrough)
            {
                parameters.ExStyle |= (int)(WsExLayered | WsExTransparent);
            }
            return parameters;
        }
    }

    public void SetClickThrough(bool enabled)
    {
        if (_clickThrough == enabled)
        {
            return;
        }

        _clickThrough = enabled;
        _clickThroughItem.Checked = enabled;
        ApplyClickThrough();
    }

    /// <summary>
    /// Makes a click-through mini panel visible without taking keyboard focus.
    /// This is required because a WS_EX_TRANSPARENT window cannot be activated
    /// normally, and Show() alone can leave it behind the active application.
    /// </summary>
    public void EnsureVisibleWithoutActivation(bool alwaysOnTop)
    {
        if (IsDisposed)
        {
            return;
        }

        _ = Handle;
        SetWindowPos(
            Handle,
            alwaysOnTop ? HwndTopMost : nint.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow | SwpFrameChanged);
        Invalidate(true);
        Update();
    }

    protected override bool CanDrag => _canDrag() && !_clickThrough;

    protected override void OnHandleCreated(EventArgs eventArgs)
    {
        base.OnHandleCreated(eventArgs);
        ApplyClickThrough();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }

    public void UpdateState(WeeklyQuotaState? state)
    {
        if (state is null)
        {
            _remainingPrefix.Text = string.Empty;
            _remainingPercent.Text = "—";
            _remainingPercent.ForeColor = Theme.Subtle;
            _progress.Value = 0;
            _reset.Text = ResetCaption();
            _countdown.Text = _localizer.Text("Quota.Unavailable");
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
            _reset.Text = ResetCaption();
            _countdown.Text = state.ResetsAtUtc is null
                ? "—"
                : $"{state.ResetsAtUtc.Value.ToLocalTime():yyyy/MM/dd HH:mm} " +
                  $"({FormatCountdown(state.ResetsAtUtc.Value)})";
        }
        AlignResetLine();
        AlignRemaining();
    }

    public void PositionNearTray()
    {
        var area = Screen.PrimaryScreen?.WorkingArea
            ?? Screen.GetWorkingArea(Cursor.Position);
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
    }

    private IEnumerable<Control> DraggableControls()
    {
        yield return this;
        yield return _quotaCard;
        foreach (Control control in _quotaCard.Controls)
        {
            yield return control;
        }
    }

    private void ApplyClickThrough()
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        var style = GetWindowLongPtr(Handle, GwlExStyle).ToInt64();
        style = _clickThrough
            ? style | WsExLayered | WsExTransparent
            : style & ~WsExTransparent;
        SetWindowLongPtr(Handle, GwlExStyle, new nint(style));
        SetWindowPos(
            Handle,
            nint.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpFrameChanged);
    }

    private void AlignRemaining()
    {
        _remainingPercent.Location = new Point(
            _quotaCard.ClientSize.Width - _remainingPercent.PreferredWidth - 13,
            2);
        _remainingPrefix.Location = new Point(
            _remainingPercent.Left - _remainingPrefix.PreferredWidth - 4,
            10);
        _weekly.Top = _remainingPrefix.Top;
        _weekly.Width = Math.Max(32, _remainingPrefix.Left - _weekly.Left - 6);
    }

    private string RemainingPrefix(string value)
    {
        var formatted = _localizer.Text("Common.Remaining", value);
        var index = formatted.IndexOf(value, StringComparison.Ordinal);
        return index <= 0 ? string.Empty : formatted[..index].Trim();
    }

    private string ResetCaption() =>
        _localizer.Text("Quota.NextReset", string.Empty).Trim();

    private void AlignResetLine()
    {
        var measured = TextRenderer.MeasureText(
            _reset.Text,
            _reset.Font,
            Size.Empty,
            TextFormatFlags.NoPadding).Width + 8;
        var captionWidth = Math.Clamp(measured, 56, 76);
        _reset.Width = captionWidth;
        _countdown.Left = _reset.Right + 4;
        _countdown.Width = Math.Max(
            0,
            _quotaCard.ClientSize.Width - _countdown.Left - 10);
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

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}

internal sealed class MiniClickThroughEventArgs(bool enabled) : EventArgs
{
    public bool Enabled { get; } = enabled;
}
