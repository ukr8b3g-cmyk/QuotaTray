namespace QuantaTrain.Core;

public static class SettingsMigration
{
    public const int CurrentSchemaVersion = 7;

    public static AppSettings Upgrade(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.General ??= new GeneralSettings();
        settings.Display ??= new DisplaySettings();
        settings.Language ??= new LanguageSettings();
        settings.Notifications ??= new NotificationSettings();
        settings.History ??= new HistorySettings();
        settings.Connection ??= new ConnectionSettings();
        settings.ResetDetection ??= new ResetDetectionSettings();
        settings.UsageAnalytics ??= new UsageAnalyticsSettings();
        settings.Diagnostics ??= new DiagnosticSettings();
        settings.Display.PanelPosition ??= new PanelPositionSettings();
        settings.Display.MiniPanelPosition ??= new PanelPositionSettings();
        settings.Display.CompactPanelPosition ??= new PanelPositionSettings();
        settings.Display.DetailPanelPosition ??= new PanelPositionSettings();

        if (settings.SchemaVersion < 5)
        {
            // v0.2.5 could persist already DPI-scaled heights as logical values.
            // Reset them once so OS-managed GDI scaling starts from safe sizes.
            settings.Display.DetailWindowHeightLogical = 600;
            settings.Display.SettingsWindowHeightLogical = 680;
        }
        if (settings.SchemaVersion < 6)
        {
            if (settings.Display.DetailWindowHeightLogical == 600)
            {
                settings.Display.DetailWindowHeightLogical = 700;
            }
            if (settings.ResetDetection.RecentHistoryCount == 3)
            {
                settings.ResetDetection.RecentHistoryCount = 4;
            }
            if (settings.Display.SettingsWindowHeightLogical == 680)
            {
                settings.Display.SettingsWindowHeightLogical = 720;
            }
        }
        if (settings.SchemaVersion < 7)
        {
            // Older releases shared one position across all panel sizes. Seed each
            // mode once, then let the three positions evolve independently.
            settings.Display.MiniPanelPosition =
                settings.Display.PanelPosition.Clone();
            settings.Display.CompactPanelPosition =
                settings.Display.PanelPosition.Clone();
            settings.Display.DetailPanelPosition =
                settings.Display.PanelPosition.Clone();
        }
        settings.Display.DetailWindowHeightLogical =
            Math.Clamp(settings.Display.DetailWindowHeightLogical, 520, 2160);
        settings.Display.SettingsWindowHeightLogical =
            Math.Clamp(settings.Display.SettingsWindowHeightLogical, 520, 2160);
        settings.ResetDetection.ConfirmationSeconds = 20;
        settings.ResetDetection.RecentHistoryCount =
            Math.Clamp(settings.ResetDetection.RecentHistoryCount, 1, 100);
        settings.UsageAnalytics.MaxIndividualModels =
            Math.Clamp(settings.UsageAnalytics.MaxIndividualModels, 1, 5);
        if (settings.UsageAnalytics.RefreshIntervalMinutes is not
            (0 or 1 or 5 or 15 or 30))
        {
            settings.UsageAnalytics.RefreshIntervalMinutes = 5;
        }
        settings.Diagnostics.LogRetentionDays =
            Math.Clamp(settings.Diagnostics.LogRetentionDays, 1, 365);
        settings.SchemaVersion = CurrentSchemaVersion;
        return settings;
    }
}
