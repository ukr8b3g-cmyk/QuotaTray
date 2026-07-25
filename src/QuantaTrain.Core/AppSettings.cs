namespace QuantaTrain.Core;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public GeneralSettings General { get; set; } = new();
    public DisplaySettings Display { get; set; } = new();
    public LanguageSettings Language { get; set; } = new();
    public NotificationSettings Notifications { get; set; } = new();
    public HistorySettings History { get; set; } = new();
    public ConnectionSettings Connection { get; set; } = new();
}

public sealed class GeneralSettings
{
    public bool LaunchAtStartup { get; set; }
    public string StartupMode { get; set; } = "tray-only";
    public int RefreshIntervalSeconds { get; set; } = 60;
    public bool RefreshOnPanelOpen { get; set; } = true;
    public bool ShowCachedOnFailure { get; set; } = true;
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
}

public sealed class LanguageSettings
{
    public string Mode { get; set; } = "auto";
    public string Locale { get; set; } = "en-US";
}

public sealed class NotificationSettings
{
    public bool Remaining30 { get; set; }
    public bool Remaining10 { get; set; } = true;
    public bool ScheduledReset { get; set; }
    public bool UnexpectedResetCandidate { get; set; } = true;
    public bool ResetCreditExpiring { get; set; } = true;
    public bool PersistentConnectionFailure { get; set; } = true;
}

public sealed class HistorySettings
{
    public int? RetentionDays { get; set; } = 365;
    public int CheckpointMinutes { get; set; } = 30;
}

public sealed class ConnectionSettings
{
    public string CodexPathMode { get; set; } = "auto";
    public string? CodexExecutablePath { get; set; }
}
