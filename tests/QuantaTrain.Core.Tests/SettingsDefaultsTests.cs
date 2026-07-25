using QuantaTrain.Core;

namespace QuantaTrain.Core.Tests;

public sealed class SettingsDefaultsTests
{
    [Fact]
    public void DefaultsMatchPrivacyAndDisplayContract()
    {
        var settings = new AppSettings();

        Assert.Equal("tray-only", settings.General.StartupMode);
        Assert.Equal(60, settings.General.RefreshIntervalSeconds);
        Assert.Equal("dark", settings.Display.Theme);
        Assert.Equal(100, settings.Display.OpacityPercent);
        Assert.False(settings.Display.AlwaysOnTop);
        Assert.False(settings.Display.LockPosition);
        Assert.False(settings.Display.RememberPosition);
        Assert.False(settings.Display.SnapToEdge);
        Assert.False(settings.Display.MiniClickThrough);
        Assert.Null(settings.Display.PanelPosition.X);
        Assert.Null(settings.Display.PanelPosition.Y);
    }
}
