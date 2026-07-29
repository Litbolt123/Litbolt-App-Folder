namespace Litbolt.Shell;

using System.IO;

/// <summary>Named-mutex single instance with optional activate.signal for second launches.</summary>
public static class SingleInstanceService
{
    public static string ActivateSignalFileName => "activate.signal";

    public static string ActivateSignalPath(string dataDirectory) =>
        Path.Combine(dataDirectory, ActivateSignalFileName);

    public static bool TryBecomePrimaryInstance(string mutexName, out Mutex? mutex)
    {
        mutex = new Mutex(true, mutexName, out var createdNew);
        if (createdNew)
            return true;

        mutex.Dispose();
        mutex = null;
        return false;
    }

    public static void NotifyPrimaryInstance(string dataDirectory, bool writeActivateSignal = true)
    {
        try
        {
            Directory.CreateDirectory(dataDirectory);
            if (writeActivateSignal)
                File.WriteAllText(ActivateSignalPath(dataDirectory), DateTime.UtcNow.ToString("o"));
        }
        catch
        {
            /* ignore */
        }
    }
}
