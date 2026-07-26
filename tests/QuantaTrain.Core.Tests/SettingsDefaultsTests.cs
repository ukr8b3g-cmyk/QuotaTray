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
        Assert.Equal(600, settings.Display.DetailWindowHeightLogical);
        Assert.True(settings.Display.RememberSettingsHeight);
        Assert.Equal(650, settings.Display.SettingsWindowHeightLogical);
        Assert.Null(settings.Display.PanelPosition.X);
        Assert.Null(settings.Display.PanelPosition.Y);
        Assert.Equal(1095, settings.History.RetentionDays);
        Assert.True(settings.Notifications.Remaining30);
        Assert.True(settings.Notifications.Remaining10);
        Assert.True(settings.Notifications.ScheduledReset);
        Assert.False(settings.UsageAnalytics.Enabled);
        Assert.Equal(5, settings.UsageAnalytics.RefreshIntervalMinutes);
        Assert.True(settings.UsageAnalytics.RefreshWhenOpened);
        Assert.True(settings.UsageAnalytics.IncludeArchivedSessions);
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

        Assert.Equal(3, migrated.SchemaVersion);
        Assert.Equal(650, migrated.Display.SettingsWindowHeightLogical);
        Assert.Equal(365, migrated.History.RetentionDays);
        Assert.False(migrated.UsageAnalytics.Enabled);
    }

    [Fact]
    public void CloneKeepsLiveSettingsIndependent()
    {
        var settings = new AppSettings();
        var copy = settings.Clone();

        copy.Display.Theme = "light";
        copy.Display.PanelPosition.X = 120;
        copy.UsageAnalytics.Enabled = true;

        Assert.Equal("dark", settings.Display.Theme);
        Assert.Null(settings.Display.PanelPosition.X);
        Assert.False(settings.UsageAnalytics.Enabled);
    }
}
