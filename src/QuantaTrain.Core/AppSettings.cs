namespace QuantaTrain.Core;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 3;
    public GeneralSettings General { get; set; } = new();
    public DisplaySettings Display { get; set; } = new();
    public LanguageSettings Language { get; set; } = new();
    public NotificationSettings Notifications { get; set; } = new();
    public HistorySettings History { get; set; } = new();
    public ConnectionSettings Connection { get; set; } = new();
    public ResetDetectionSettings ResetDetection { get; set; } = new();
    public UsageAnalyticsSettings UsageAnalytics { get; set; } = new();
    public DiagnosticSettings Diagnostics { get; set; } = new();

    public AppSettings Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        General = General.Clone(),
        Display = Display.Clone(),
        Language = Language.Clone(),
        Notifications = Notifications.Clone(),
        History = History.Clone(),
        Connection = Connection.Clone(),
        ResetDetection = ResetDetection.Clone(),
        UsageAnalytics = UsageAnalytics.Clone(),
        Diagnostics = Diagnostics.Clone(),
    };

    public void CopyFrom(AppSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy = source.Clone();
        SchemaVersion = copy.SchemaVersion;
        General = copy.General;
        Display = copy.Display;
        Language = copy.Language;
        Notifications = copy.Notifications;
        History = copy.History;
        Connection = copy.Connection;
        ResetDetection = copy.ResetDetection;
        UsageAnalytics = copy.UsageAnalytics;
        Diagnostics = copy.Diagnostics;
    }
}

public sealed class GeneralSettings
{
    public bool LaunchAtStartup { get; set; }
    public string StartupMode { get; set; } = "tray-only";
    public int RefreshIntervalSeconds { get; set; } = 60;
    public bool RefreshOnPanelOpen { get; set; } = true;
    public bool ShowCachedOnFailure { get; set; } = true;

    internal GeneralSettings Clone() => (GeneralSettings)MemberwiseClone();
}

public sealed class DisplaySettings
{
    public string Theme { get; set; } = "dark";
    public string Accent { get; set; } = "green";
    public int OpacityPercent { get; set; } = 100;
    public bool AlwaysOnTop { get; set; }
    public bool LockPosition { get; set; }
    public bool RememberPosition { get; set; }
    public bool SnapToEdge { get; set; }
    public bool MiniClickThrough { get; set; }
    public bool RememberDetailHeight { get; set; } = true;
    public int DetailWindowHeightLogical { get; set; } = 600;
    public bool RememberSettingsHeight { get; set; } = true;
    public int SettingsWindowHeightLogical { get; set; } = 650;
    public PanelPositionSettings PanelPosition { get; set; } = new();

    internal DisplaySettings Clone()
    {
        var copy = (DisplaySettings)MemberwiseClone();
        copy.PanelPosition = PanelPosition.Clone();
        return copy;
    }
}

public sealed class PanelPositionSettings
{
    public string? MonitorDeviceName { get; set; }
    public string HorizontalAnchor { get; set; } = "left";
    public string VerticalAnchor { get; set; } = "top";
    public int? X { get; set; }
    public int? Y { get; set; }

    internal PanelPositionSettings Clone() => (PanelPositionSettings)MemberwiseClone();
}

public sealed class LanguageSettings
{
    public string Mode { get; set; } = "auto";
    public string Locale { get; set; } = "en-US";

    internal LanguageSettings Clone() => (LanguageSettings)MemberwiseClone();
}

public sealed class NotificationSettings
{
    public bool Remaining30 { get; set; } = true;
    public bool Remaining10 { get; set; } = true;
    public bool ScheduledReset { get; set; } = true;
    public bool UnexpectedResetCandidate { get; set; } = true;
    public bool ResetCreditExpiring { get; set; } = true;
    public bool PersistentConnectionFailure { get; set; } = true;

    internal NotificationSettings Clone() => (NotificationSettings)MemberwiseClone();
}

public sealed class HistorySettings
{
    public int? RetentionDays { get; set; } = 1095;
    public int CheckpointMinutes { get; set; } = 30;

    internal HistorySettings Clone() => (HistorySettings)MemberwiseClone();
}

public sealed class ConnectionSettings
{
    public string CodexPathMode { get; set; } = "auto";
    public string? CodexExecutablePath { get; set; }

    internal ConnectionSettings Clone() => (ConnectionSettings)MemberwiseClone();
}

public sealed class ResetDetectionSettings
{
    public bool StoreQuotaState { get; set; } = true;
    public bool DetectUnexpectedRecovery { get; set; } = true;
    public bool ConfirmRecovery { get; set; } = true;
    public int ConfirmationSeconds { get; set; } = 20;
    public int RecentHistoryCount { get; set; } = 3;

    internal ResetDetectionSettings Clone() =>
        (ResetDetectionSettings)MemberwiseClone();
}

public sealed class UsageAnalyticsSettings
{
    public bool Enabled { get; set; }
    public int RefreshIntervalMinutes { get; set; } = 5;
    public bool RefreshWhenOpened { get; set; } = true;
    public bool IncludeArchivedSessions { get; set; } = true;
    public string? CodexHomeOverride { get; set; }
    public bool CollectModel { get; set; } = true;
    public bool CollectReasoningEffort { get; set; } = true;
    public bool CollectServiceTier { get; set; } = true;
    public bool CollectTokens { get; set; } = true;
    public bool CollectElapsedTime { get; set; } = true;
    public bool CollectTurnCount { get; set; } = true;
    public string DefaultPeriod { get; set; } = "current-window";
    public string DefaultMetric { get; set; } = "total-tokens";
    public string ChartStyle { get; set; } = "horizontal-bar";
    public string SortOrder { get; set; } = "descending";
    public string NumberFormat { get; set; } = "grouped";
    public int MaxIndividualModels { get; set; } = 5;
    public bool ShowElapsedTime { get; set; } = true;
    public bool ShowTurnCount { get; set; } = true;
    public bool ShowReasoningBreakdown { get; set; } = true;
    public bool ShowServiceTierBreakdown { get; set; } = true;
    public bool GroupOtherModels { get; set; } = true;
    public bool ShowEstimatedConsumption { get; set; }

    internal UsageAnalyticsSettings Clone() =>
        (UsageAnalyticsSettings)MemberwiseClone();
}

public sealed class DiagnosticSettings
{
    public int LogRetentionDays { get; set; } = 14;

    internal DiagnosticSettings Clone() => (DiagnosticSettings)MemberwiseClone();
}
