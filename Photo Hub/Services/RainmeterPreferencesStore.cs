using System.Text.Json;

namespace PhotoHub.Services;

public sealed class RainmeterPreferences
{
    public bool PhotoWidgetActive { get; set; } = true;
    public Dictionary<string, bool> IllustroSkins { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> LitboltSkins { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class RainmeterPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string PathFile => Path.Combine(App.DataDirectory, "rainmeter.json");

    public static RainmeterPreferences Load()
    {
        try
        {
            if (!File.Exists(PathFile))
                return new RainmeterPreferences();
            return JsonSerializer.Deserialize<RainmeterPreferences>(File.ReadAllText(PathFile), JsonOptions)
                   ?? new RainmeterPreferences();
        }
        catch
        {
            return new RainmeterPreferences();
        }
    }

    public static void Save(RainmeterPreferences prefs) =>
        File.WriteAllText(PathFile, JsonSerializer.Serialize(prefs, JsonOptions));
}
