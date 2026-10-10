namespace PhotoHub.Services;

public static class HubLog
{
    public static event Action<string, bool>? Line;

    public static void Info(string message) => Line?.Invoke(message, false);

    public static void Error(string message) => Line?.Invoke(message, true);
}
