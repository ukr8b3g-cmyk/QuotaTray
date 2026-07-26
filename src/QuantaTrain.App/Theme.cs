namespace QuantaTrain.App;

internal static class Theme
{
    private static bool _light;
    private static Color _accent = Color.FromArgb(91, 196, 91);
    private static bool _useGreenUsedQuota;

    public static Color Window => _light
        ? Color.FromArgb(246, 248, 250)
        : Color.FromArgb(19, 24, 29);
    public static Color Surface => _light
        ? Color.FromArgb(255, 255, 255)
        : Color.FromArgb(22, 28, 33);
    public static Color SurfaceRaised => _light
        ? Color.FromArgb(232, 237, 242)
        : Color.FromArgb(34, 41, 48);
    public static Color Border => _light
        ? Color.FromArgb(186, 196, 204)
        : Color.FromArgb(45, 54, 61);
    public static Color Text => _light
        ? Color.FromArgb(28, 35, 40)
        : Color.FromArgb(242, 245, 247);
    public static Color Muted => _light
        ? Color.FromArgb(77, 91, 101)
        : Color.FromArgb(178, 187, 193);
    public static Color Subtle => _light
        ? Color.FromArgb(101, 116, 126)
        : Color.FromArgb(116, 129, 137);
    public static Color Green => Color.FromArgb(91, 196, 91);
    public static Color Blue => Color.FromArgb(69, 153, 232);
    public static Color Orange => Color.FromArgb(239, 143, 52);
    public static Color Yellow => Color.FromArgb(248, 185, 27);
    public static Color Red => Color.FromArgb(239, 83, 80);
    public static Color Accent => _accent;
    public static Color UsedQuota => _useGreenUsedQuota ? Green : Blue;

    public static void Configure(string theme, string accent)
    {
        _light = theme.Equals("light", StringComparison.OrdinalIgnoreCase)
            || (theme.Equals("system", StringComparison.OrdinalIgnoreCase)
                && UsesLightSystemTheme());
        var normalizedAccent = accent.ToLowerInvariant();
        _accent = normalizedAccent switch
        {
            "blue" => Blue,
            "cyan" => Color.FromArgb(73, 179, 214),
            "purple" => Color.FromArgb(171, 94, 230),
            "yellow" => Yellow,
            "gray" => Subtle,
            _ => Green,
        };
        _useGreenUsedQuota =
            normalizedAccent is "blue" or "cyan";
    }

    private static bool UsesLightSystemTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch
        {
            return true;
        }
    }

    // Compatibility names used by the tray icon and existing infrastructure.
    public static Color DarkBackground => Window;
    public static Color DarkSurface => SurfaceRaised;
    public static Color DarkBorder => Border;
    public static Color LightText => Text;
    public static Color MutedText => Muted;

    public static Color QuotaColor(double? remaining) =>
        remaining switch
        {
            null => Subtle,
            <= 10 => Red,
            <= 30 => Orange,
            _ => Accent,
        };

    public static Font Ui(float size = 9F, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI", size, style, GraphicsUnit.Point);

    public static void StyleCombo(ComboBox combo)
    {
        combo.BackColor = SurfaceRaised;
        combo.ForeColor = Text;
        combo.FlatStyle = FlatStyle.Standard;
        combo.Font = Ui(9F);
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.ItemHeight = 22;
        combo.DrawItem -= DrawComboItem;
        combo.DrawItem += DrawComboItem;
    }

    private static void DrawComboItem(object? sender, DrawItemEventArgs eventArgs)
    {
        if (sender is not ComboBox combo)
        {
            return;
        }

        var isEdit = (eventArgs.State & DrawItemState.ComboBoxEdit) != 0;
        var isSelected =
            (eventArgs.State & DrawItemState.Selected) != 0 && !isEdit;
        using var background = new SolidBrush(
            isSelected ? Surface : SurfaceRaised);
        eventArgs.Graphics.FillRectangle(background, eventArgs.Bounds);

        var item = eventArgs.Index >= 0 && eventArgs.Index < combo.Items.Count
            ? combo.Items[eventArgs.Index]
            : combo.SelectedItem;
        var text = item?.ToString() ?? string.Empty;
        var textBounds = Rectangle.Inflate(eventArgs.Bounds, -5, 0);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            text,
            combo.Font,
            textBounds,
            Text,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
        eventArgs.DrawFocusRectangle();
    }
}
