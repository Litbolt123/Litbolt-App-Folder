using System.IO;

namespace Litbolt.Shell;

public static class AppPathResolver
{
    public static string LocalAppPrograms =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs");

    public static string? FindInstalledExe(string folderName, string exeName)
    {
        var path = Path.Combine(LocalAppPrograms, folderName, exeName);
        return File.Exists(path) ? path : null;
    }

    public static string? FindBesideOrInstalled(string folderName, string exeName, params string[] extraCandidates)
    {
        foreach (var c in extraCandidates)
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                return c;
        }

        return FindInstalledExe(folderName, exeName);
    }
}
