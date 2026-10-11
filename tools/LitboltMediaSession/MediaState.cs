namespace LitboltMediaSession;

internal static class MediaState
{
    public static string? LastTitle;
    public static long CoverRev;
    public static long LastCoverHash;
    public static string CoverFileName = "cover.jpg";
    public static Mutex? WatchMutex;
}
