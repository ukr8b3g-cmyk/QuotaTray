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
            using var backgroundPen = new Pen(Color.FromArgb(70, 80, 82), 7);
            using var valuePen = new Pen(Theme.QuotaColor(remaining), 7)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
            };
            graphics.DrawEllipse(backgroundPen, 7, 7, 50, 50);
            if (remaining is not null)
            {
                graphics.DrawArc(valuePen, 7, 7, 50, 50, -90, (float)(remaining.Value * 3.6));
            }

            using var font = new Font("Segoe UI", 19, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color.White);
            var text = remaining is null ? "Q" : Math.Round(remaining.Value).ToString("0");
            var measured = graphics.MeasureString(text, font);
            graphics.DrawString(
                text,
                font,
                brush,
                (size - measured.Width) / 2,
                (size - measured.Height) / 2);
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
