using Microsoft.Win32;

namespace Litbolt.Shell.Tray;

/// <summary>Per-user HKCU Run key entries for Litbolt apps.</summary>
public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled(string valueName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            if (key is null)
                return false;
            return key.GetValue(valueName) is string s && s.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(string valueName, bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
                return;

            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                    return;
                key.SetValue(valueName, $"\"{exe}\" --minimized", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            /* registry writes can fail on locked-down machines */
        }
    }

    public static bool ArgsRequestTray(IEnumerable<string> args) =>
        args.Any(a =>
            string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase));
}
