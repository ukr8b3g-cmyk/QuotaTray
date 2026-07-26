namespace QuantaTrain.Core;

public static class SettingsMigration
{
    public const int CurrentSchemaVersion = 2;

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

        settings.Display.DetailWindowHeightLogical =
            Math.Clamp(settings.Display.DetailWindowHeightLogical, 520, 2160);
        settings.Display.SettingsWindowHeightLogical =
            Math.Clamp(settings.Display.SettingsWindowHeightLogical, 520, 2160);
        settings.ResetDetection.ConfirmationSeconds = 20;
        settings.ResetDetection.RecentHistoryCount =
            Math.Clamp(settings.ResetDetection.RecentHistoryCount, 1, 100);
        settings.UsageAnalytics.MaxIndividualModels =
            Math.Clamp(settings.UsageAnalytics.MaxIndividualModels, 1, 5);
        settings.Diagnostics.LogRetentionDays =
            Math.Clamp(settings.Diagnostics.LogRetentionDays, 1, 365);
        settings.SchemaVersion = CurrentSchemaVersion;
        return settings;
    }
}
