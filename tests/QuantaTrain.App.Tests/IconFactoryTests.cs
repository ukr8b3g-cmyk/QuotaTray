using System.Drawing;
using QuantaTrain.App;

namespace QuantaTrain.App.Tests;

public sealed class IconFactoryTests
{
    [Theory]
    [InlineData(9)]
    [InlineData(42)]
    [InlineData(100)]
    public void TrayArtworkUsesMostOfSlotAndKeepsWhiteDigits(double remaining)
    {
        using var icon = IconFactory.Create(remaining);
        using var bitmap = icon.ToBitmap();
        var opaque = new List<Point>();
        var white = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A > 20)
                {
                    opaque.Add(new Point(x, y));
                }
                if (pixel.A > 100 && pixel.R > 220 && pixel.G > 220 && pixel.B > 220)
                {
                    white++;
                }
            }
        }

        Assert.NotEmpty(opaque);
        Assert.True(opaque.Max(point => point.X) - opaque.Min(point => point.X) >= 27);
        Assert.True(opaque.Max(point => point.Y) - opaque.Min(point => point.Y) >= 27);
        Assert.True(white >= 8);
    }
}
