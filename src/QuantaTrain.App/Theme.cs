namespace QuantaTrain.App;

internal static class Theme
{
    public static readonly Color Window = Color.FromArgb(19, 24, 29);
    public static readonly Color Surface = Color.FromArgb(22, 28, 33);
    public static readonly Color SurfaceRaised = Color.FromArgb(34, 41, 48);
    public static readonly Color Border = Color.FromArgb(45, 54, 61);
    public static readonly Color Text = Color.FromArgb(242, 245, 247);
    public static readonly Color Muted = Color.FromArgb(178, 187, 193);
    public static readonly Color Subtle = Color.FromArgb(116, 129, 137);
    public static readonly Color Green = Color.FromArgb(91, 196, 91);
    public static readonly Color Blue = Color.FromArgb(69, 153, 232);
    public static readonly Color Yellow = Color.FromArgb(248, 185, 27);
    public static readonly Color Red = Color.FromArgb(239, 83, 80);

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
            _ => Green,
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
