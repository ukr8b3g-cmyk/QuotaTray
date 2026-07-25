namespace QuantaTrain.App;

internal static class Theme
{
    private static bool _light;
    private static Color _accent = Color.FromArgb(91, 196, 91);

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
    public static Color Yellow => Color.FromArgb(248, 185, 27);
    public static Color Red => Color.FromArgb(239, 83, 80);
    public static Color Accent => _accent;

    public static void Configure(string theme, string accent)
    {
        _light = theme.Equals("light", StringComparison.OrdinalIgnoreCase)
            || (theme.Equals("system", StringComparison.OrdinalIgnoreCase)
                && UsesLightSystemTheme());
        _accent = accent.ToLowerInvariant() switch
        {
            "blue" => Blue,
            "cyan" => Color.FromArgb(73, 179, 214),
            "purple" => Color.FromArgb(171, 94, 230),
            "yellow" => Yellow,
            "gray" => Subtle,
            _ => Green,
        };
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
            <= 30 => Yellow,
            _ => Accent,
        };

    public static Font Ui(float size = 9F, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI", size, style, GraphicsUnit.Point);

    public static void StyleCombo(ComboBox combo)
    {
        combo.BackColor = SurfaceRaised;
        combo.ForeColor = Text;
        combo.FlatStyle = FlatStyle.Flat;
        combo.Font = Ui(9F);
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
    }
}
