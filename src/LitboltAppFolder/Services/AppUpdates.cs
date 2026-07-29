using Litbolt.Shell.Updates;

namespace LitboltAppFolder.Services;

public static class AppUpdates
{
    public const string GitHubRepo = "Litbolt123/Litbolt-App-Folder";
    public const string InstallerPrefix = "LitboltAppFolder-Setup-";

    public static GitHubUpdateClient Client { get; } =
        new(GitHubRepo, InstallerPrefix, "LitboltAppFolder-UpdateCheck/1.0");

    public static bool ShouldShowBanner(FolderSettings settings, string latestVersion)
    {
        if (string.IsNullOrWhiteSpace(latestVersion))
            return false;
        var dismissed = settings.CatchUpUpdateDismissedVersion ?? "";
        return !string.Equals(dismissed, latestVersion.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static void DismissBanner(FolderSettings settings, string latestVersion)
    {
        settings.CatchUpUpdateDismissedVersion = latestVersion.Trim();
        FolderSettingsStore.Save(settings);
    }
}
