using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace QuantaTrain.App;

internal static class FluentSymbol
{
    public const string More = "\uE712";
    public const string Refresh = "\uE72C";
    public const string Compact = "\uE73F";
    public const string Settings = "\uE713";
    public const string Close = "\uE8BB";
    public const string General = "\uE713";
    public const string Display = "\uE7F4";
    public const string Language = "\uE774";
    public const string Notification = "\uEA8F";
    public const string History = "\uE81C";
    public const string Account = "\uE77B";
    public const string Info = "\uE946";
}

internal static class UiGeometry
{
    public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            0,
            90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal class FramelessForm : Form
{
    private const int CsDropShadow = 0x00020000;
    private const int WmNclButtonDown = 0x00A1;
    private const int WmExitSizeMove = 0x0232;
    private const int HtCaption = 2;

    public FramelessForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = Theme.Ui();
        DoubleBuffered = true;
        ShowInTaskbar = Environment.GetCommandLineArgs()
            .Contains("--qa-window", StringComparer.OrdinalIgnoreCase);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ClassStyle |= CsDropShadow;
            return parameters;
        }
    }

    public event EventHandler? MoveCompleted;

    protected virtual bool CanDrag => true;

    protected override void OnSizeChanged(EventArgs eventArgs)
    {
        base.OnSizeChanged(eventArgs);
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = UiGeometry.RoundedRectangle(
            new Rectangle(0, 0, Width, Height),
            9);
        Region?.Dispose();
        Region = new Region(path);
    }

    protected void MakeDraggable(Control control)
    {
        control.MouseDown += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Left || !CanDrag)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
        };
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg == WmExitSizeMove)
        {
            MoveCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint handle, int message, int wParam, int lParam);
}

internal sealed class RoundedPanel : Panel
{
    private Color _borderColor = Theme.Border;
    private int _radius = 8;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        BackColor = Theme.Surface;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius
    {
        get => _radius;
        set
        {
            _radius = value;
            UpdateRegion();
            Invalidate();
        }
    }

    protected override void OnSizeChanged(EventArgs eventArgs)
    {
        base.OnSizeChanged(eventArgs);
        UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiGeometry.RoundedRectangle(
            new Rectangle(0, 0, Width - 1, Height - 1),
            Radius);
        using var pen = new Pen(BorderColor);
        eventArgs.Graphics.DrawPath(pen, path);
        base.OnPaint(eventArgs);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = UiGeometry.RoundedRectangle(
            new Rectangle(0, 0, Width, Height),
            Radius);
        Region?.Dispose();
        Region = new Region(path);
    }
}

internal sealed class IconButton : Button
{
    public IconButton(string symbol)
    {
        Text = symbol;
        Font = new Font("Segoe Fluent Icons", 10F, FontStyle.Regular);
        ForeColor = Theme.Text;
        BackColor = Theme.Window;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Theme.SurfaceRaised;
        FlatAppearance.MouseDownBackColor = Theme.Border;
        Cursor = Cursors.Hand;
        TabStop = true;
        Margin = Padding.Empty;
        UseVisualStyleBackColor = false;
    }
}

internal sealed class QuotaProgressBar : Control
{
    private int _value;

    public QuotaProgressBar()
    {
        DoubleBuffered = true;
        Size = new Size(100, 12);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 100);
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ValueColor { get; set; } = Theme.Green;

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var track = UiGeometry.RoundedRectangle(
            new Rectangle(0, 0, Width - 1, Height - 1),
            Height / 2);
        using var trackBrush = new SolidBrush(Theme.SurfaceRaised);
        eventArgs.Graphics.FillPath(trackBrush, track);

        var fillWidth = (int)Math.Round((Width - 1) * Value / 100d);
        if (fillWidth < 2)
        {
            return;
        }

        using var fill = UiGeometry.RoundedRectangle(
            new Rectangle(0, 0, fillWidth, Height - 1),
            Height / 2);
        using var fillBrush = new SolidBrush(ValueColor);
        eventArgs.Graphics.FillPath(fillBrush, fill);
    }
}

internal sealed class ToggleSwitch : Control
{
    private bool _checked;

    public ToggleSwitch()
    {
        Size = new Size(40, 22);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        AccessibleRole = AccessibleRole.CheckButton;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            AccessibleDefaultActionDescription = value ? "On" : "Off";
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CheckedChanged;

    protected override void OnClick(EventArgs eventArgs)
    {
        Checked = !Checked;
        base.OnClick(eventArgs);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var track = UiGeometry.RoundedRectangle(
            new Rectangle(0, 2, Width - 1, Height - 5),
            (Height - 4) / 2);
        using var trackBrush = new SolidBrush(Checked ? Theme.Accent : Theme.SurfaceRaised);
        eventArgs.Graphics.FillPath(trackBrush, track);

        var knobSize = Height - 8;
        var knobX = Checked ? Width - knobSize - 4 : 4;
        using var knobBrush = new SolidBrush(Color.FromArgb(246, 248, 249));
        eventArgs.Graphics.FillEllipse(knobBrush, knobX, 4, knobSize, knobSize);
    }
}

internal sealed class ValueSlider : Control
{
    private int _value = 100;

    public ValueSlider()
    {
        Size = new Size(112, 22);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum { get; set; } = 60;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum { get; set; } = 100;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, Minimum, Maximum);
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ValueChanged;

    protected override void OnMouseDown(MouseEventArgs eventArgs)
    {
        SetValueFromX(eventArgs.X);
        base.OnMouseDown(eventArgs);
    }

    protected override void OnMouseMove(MouseEventArgs eventArgs)
    {
        if (eventArgs.Button == MouseButtons.Left)
        {
            SetValueFromX(eventArgs.X);
        }
        base.OnMouseMove(eventArgs);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var y = Height / 2;
        using var basePen = new Pen(Theme.SurfaceRaised, 4)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        eventArgs.Graphics.DrawLine(basePen, 4, y, Width - 6, y);

        var ratio = (Value - Minimum) / (double)(Maximum - Minimum);
        var x = 4 + (int)Math.Round((Width - 10) * ratio);
        using var valuePen = new Pen(Theme.Accent, 4)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        eventArgs.Graphics.DrawLine(valuePen, 4, y, x, y);
        using var knob = new SolidBrush(Theme.Accent);
        eventArgs.Graphics.FillEllipse(knob, x - 6, y - 6, 12, 12);
    }

    private void SetValueFromX(int x)
    {
        var ratio = Math.Clamp((x - 4d) / Math.Max(1, Width - 10d), 0d, 1d);
        Value = Minimum + (int)Math.Round((Maximum - Minimum) * ratio);
    }
}

internal sealed class NavButton : UserControl
{
    private readonly Label _icon;
    private readonly Label _label;
    private bool _selected;
    private bool _hovered;

    public NavButton(string symbol, string text)
    {
        Symbol = symbol;
        Text = text;
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        TabStop = true;
        SetStyle(ControlStyles.Selectable, true);

        _icon = new Label
        {
            Text = symbol,
            Font = new Font("Segoe Fluent Icons", 10F),
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Bounds = new Rectangle(11, 0, 23, 34),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _label = new Label
        {
            Text = text,
            Font = Theme.Ui(9F),
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Bounds = new Rectangle(38, 0, 72, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        Controls.AddRange([_icon, _label]);

        foreach (var control in new Control[] { this, _icon, _label })
        {
            control.MouseEnter += (_, _) => SetHovered(true);
            control.MouseLeave += (_, _) => SetHovered(
                ClientRectangle.Contains(PointToClient(Cursor.Position)));
        }
        _icon.Click += (_, eventArgs) => OnClick(eventArgs);
        _label.Click += (_, eventArgs) => OnClick(eventArgs);
    }

    public string Symbol { get; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            var oldFont = _label.Font;
            _label.Font = Theme.Ui(
                9F,
                value ? FontStyle.Bold : FontStyle.Regular);
            oldFont.Dispose();
            UpdateAppearance();
        }
    }

    protected override void OnSizeChanged(EventArgs eventArgs)
    {
        base.OnSizeChanged(eventArgs);
        _icon.Height = Height;
        _label.Size = new Size(Math.Max(0, Width - 40), Height);
        if (Width > 0 && Height > 0)
        {
            using var path = UiGeometry.RoundedRectangle(
                new Rectangle(0, 0, Width, Height),
                6);
            Region?.Dispose();
            Region = new Region(path);
        }
    }

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        if (eventArgs.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            eventArgs.Handled = true;
        }
        base.OnKeyDown(eventArgs);
    }

    private void SetHovered(bool hovered)
    {
        if (_hovered == hovered)
        {
            return;
        }

        _hovered = hovered;
        UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        BackColor = Selected
            ? Theme.SurfaceRaised
            : _hovered
                ? Color.FromArgb(28, 34, 40)
                : Theme.Window;
        Invalidate(true);
    }
}

internal static class UiFactory
{
    public static PictureBox BrandIcon(Point location, int size = 24)
    {
        Image image;
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            image = icon?.ToBitmap() ?? SystemIcons.Application.ToBitmap();
        }
        catch
        {
            image = SystemIcons.Application.ToBitmap();
        }

        return new PictureBox
        {
            Image = image,
            Location = location,
            Size = new Size(size, size),
            SizeMode = PictureBoxSizeMode.Zoom,
        };
    }

    public static Label Label(
        string text,
        Point location,
        float size = 9F,
        FontStyle style = FontStyle.Regular,
        Color? color = null)
    {
        return new Label
        {
            Text = text,
            Location = location,
            AutoSize = true,
            Font = Theme.Ui(size, style),
            ForeColor = color ?? Theme.Text,
            BackColor = Color.Transparent,
        };
    }

    public static Button TextButton(
        string text,
        Rectangle bounds,
        bool primary = false,
        bool danger = false)
    {
        var background = danger
            ? Theme.Red
            : primary
                ? Theme.Accent
                : Theme.SurfaceRaised;
        var button = new Button
        {
            Text = text,
            Bounds = bounds,
            Font = Theme.Ui(
                9F,
                primary || danger ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = Theme.Text,
            BackColor = background,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderColor = danger
            ? Theme.Red
            : primary
                ? Theme.Accent
                : Theme.Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor =
            primary || danger
                ? ControlPaint.Light(background)
                : Theme.SurfaceRaised;
        return button;
    }
}
