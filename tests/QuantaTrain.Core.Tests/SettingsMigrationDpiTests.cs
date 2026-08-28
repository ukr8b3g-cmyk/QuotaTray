using QuantaTrain.Core;

namespace QuantaTrain.Core.Tests;

public sealed class SettingsMigrationDpiTests
{
    [Fact]
    public void SchemaFourUpgradeResetsPotentiallyDpiScaledWindowHeights()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 4,
            Display = new DisplaySettings
            {
                DetailWindowHeightLogical = 1200,
                SettingsWindowHeightLogical = 1360,
            },
        };

        SettingsMigration.Upgrade(settings);

        Assert.Equal(7, settings.SchemaVersion);
        Assert.Equal(700, settings.Display.DetailWindowHeightLogical);
        Assert.Equal(720, settings.Display.SettingsWindowHeightLogical);
    }

    [Fact]
    public void CurrentSchemaPreservesValidRememberedLogicalHeights()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 5,
            Display = new DisplaySettings
            {
                DetailWindowHeightLogical = 720,
                SettingsWindowHeightLogical = 760,
            },
        };

        SettingsMigration.Upgrade(settings);

        Assert.Equal(720, settings.Display.DetailWindowHeightLogical);
        Assert.Equal(760, settings.Display.SettingsWindowHeightLogical);
    }

    [Fact]
    public void SchemaFiveUpgradeMovesOnlyOldV026Defaults()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 5,
            Display = new DisplaySettings
            {
                DetailWindowHeightLogical = 600,
                SettingsWindowHeightLogical = 680,
            },
            ResetDetection = new ResetDetectionSettings
            {
                RecentHistoryCount = 3,
            },
        };

        SettingsMigration.Upgrade(settings);

        Assert.Equal(7, settings.SchemaVersion);
        Assert.Equal(700, settings.Display.DetailWindowHeightLogical);
        Assert.Equal(720, settings.Display.SettingsWindowHeightLogical);
        Assert.Equal(4, settings.ResetDetection.RecentHistoryCount);
    }
}
