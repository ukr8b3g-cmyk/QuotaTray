using System.Runtime.InteropServices;

namespace QuantaTrain.App;

internal static class IconFactory
{
    public static Icon Create(double? remaining)
    {
        const int size = 64;
        using var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var backgroundPen = new Pen(Color.FromArgb(70, 80, 82), 5);
            using var valuePen = new Pen(Theme.QuotaColor(remaining), 5)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
            };
            graphics.DrawEllipse(backgroundPen, 3, 3, 58, 58);
            if (remaining is not null)
            {
                graphics.DrawArc(
                    valuePen,
                    3,
                    3,
                    58,
                    58,
                    -90,
                    (float)(Math.Clamp(remaining.Value, 0, 100) * 3.6));
            }

            var text = remaining is null ? "Q" : Math.Round(remaining.Value).ToString("0");
            var fontSize = text.Length switch
            {
                1 => 30F,
                2 => 27F,
                _ => 23F,
            };
            using var font = new Font(
                "Segoe UI",
                fontSize,
                FontStyle.Bold,
                GraphicsUnit.Pixel);
            TextRenderer.DrawText(
                graphics,
                text,
                font,
                new Rectangle(1, 1, size - 2, size - 2),
                Color.White,
                Color.Transparent,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var source = Icon.FromHandle(handle);
            return (Icon)source.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint handle);
}
