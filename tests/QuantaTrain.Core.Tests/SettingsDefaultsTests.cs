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
        Assert.True(settings.Display.RememberDetailHeight);
        Assert.Equal(700, settings.Display.DetailWindowHeightLogical);
        Assert.True(settings.Display.RememberSettingsHeight);
        Assert.Equal(720, settings.Display.SettingsWindowHeightLogical);
        Assert.Equal(4, settings.ResetDetection.RecentHistoryCount);
        Assert.Null(settings.Display.PanelPosition.X);
        Assert.Null(settings.Display.PanelPosition.Y);
        Assert.Null(settings.Display.MiniPanelPosition.X);
        Assert.Null(settings.Display.CompactPanelPosition.X);
        Assert.Null(settings.Display.DetailPanelPosition.X);
        Assert.Equal(1095, settings.History.RetentionDays);
        Assert.True(settings.Notifications.Remaining30);
        Assert.True(settings.Notifications.Remaining10);
        Assert.True(settings.Notifications.ScheduledReset);
        Assert.False(settings.UsageAnalytics.Enabled);
        Assert.Equal(5, settings.UsageAnalytics.RefreshIntervalMinutes);
        Assert.True(settings.UsageAnalytics.RefreshWhenOpened);
        Assert.True(settings.UsageAnalytics.IncludeArchivedSessions);
        Assert.False(settings.UsageAnalytics.CollectToolUsage);
        Assert.False(settings.UsageAnalytics.CollectSkillUsage);
        Assert.True(settings.UsageAnalytics.ShowAccountUsage);
        Assert.Equal("current-window", settings.UsageAnalytics.DefaultPeriod);
        Assert.Equal("total-tokens", settings.UsageAnalytics.DefaultMetric);
        Assert.Equal(5, settings.UsageAnalytics.MaxIndividualModels);
        Assert.Equal(14, settings.Diagnostics.LogRetentionDays);
    }

    [Fact]
    public void MigrationPreservesExistingOneYearRetention()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 1,
            Display = new DisplaySettings
            {
                SettingsWindowHeightLogical = 600,
            },
            History = new HistorySettings { RetentionDays = 365 },
        };

        var migrated = SettingsMigration.Upgrade(settings);

        Assert.Equal(7, migrated.SchemaVersion);
        Assert.Equal(720, migrated.Display.SettingsWindowHeightLogical);
        Assert.Equal(365, migrated.History.RetentionDays);
        Assert.False(migrated.UsageAnalytics.Enabled);
    }

    [Fact]
    public void MigrationRaisesPreviouslySaved650SettingsHeight()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 3,
            Display = new DisplaySettings
            {
                SettingsWindowHeightLogical = 650,
            },
        };

        var migrated = SettingsMigration.Upgrade(settings);

        Assert.Equal(7, migrated.SchemaVersion);
        Assert.Equal(720, migrated.Display.SettingsWindowHeightLogical);
    }

    [Fact]
    public void CloneKeepsLiveSettingsIndependent()
    {
        var settings = new AppSettings();
        var copy = settings.Clone();

        copy.Display.Theme = "light";
        copy.Display.PanelPosition.X = 120;
        copy.Display.MiniPanelPosition.X = 220;
        copy.Display.CompactPanelPosition.X = 320;
        copy.Display.DetailPanelPosition.X = 420;
        copy.UsageAnalytics.Enabled = true;

        Assert.Equal("dark", settings.Display.Theme);
        Assert.Null(settings.Display.PanelPosition.X);
        Assert.Null(settings.Display.MiniPanelPosition.X);
        Assert.Null(settings.Display.CompactPanelPosition.X);
        Assert.Null(settings.Display.DetailPanelPosition.X);
        Assert.False(settings.UsageAnalytics.Enabled);
    }

    [Fact]
    public void SchemaSixMigrationSeedsIndependentPanelPositions()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 6,
            Display = new DisplaySettings
            {
                PanelPosition = new PanelPositionSettings
                {
                    MonitorDeviceName = @"\\.\DISPLAY2",
                    HorizontalAnchor = "right",
                    VerticalAnchor = "bottom",
                    X = 29,
                    Y = 54,
                },
            },
        };

        var migrated = SettingsMigration.Upgrade(settings);

        Assert.Equal(7, migrated.SchemaVersion);
        Assert.Equal(29, migrated.Display.MiniPanelPosition.X);
        Assert.Equal(29, migrated.Display.CompactPanelPosition.X);
        Assert.Equal(29, migrated.Display.DetailPanelPosition.X);
        Assert.Equal("bottom", migrated.Display.DetailPanelPosition.VerticalAnchor);

        migrated.Display.MiniPanelPosition.X = 100;
        Assert.Equal(29, migrated.Display.CompactPanelPosition.X);
        Assert.Equal(29, migrated.Display.DetailPanelPosition.X);
        Assert.Equal(29, migrated.Display.PanelPosition.X);
    }
}
