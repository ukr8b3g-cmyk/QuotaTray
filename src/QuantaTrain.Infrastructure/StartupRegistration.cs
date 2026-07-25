using Microsoft.Win32;

namespace QuantaTrain.Infrastructure;

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuantaTray";
    private const string LegacyValueName = "QuantaTrain";

    public static void SetEnabled(bool enabled, string executablePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Windows startup key is unavailable.");
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{executablePath}\" --background", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
