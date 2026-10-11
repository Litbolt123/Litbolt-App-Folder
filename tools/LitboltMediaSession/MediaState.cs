namespace LitboltMediaSession;

internal static class MediaState
{
    public static string? LastTitle;
    public static long CoverRev;
    public static long LastCoverHash;
    public static Mutex? WatchMutex;
}
