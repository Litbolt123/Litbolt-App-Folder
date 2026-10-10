namespace PhotoHub.Services;

public static class PhotoPaths
{
    public const string SortTools = @"C:\Users\Augus\Pictures\_sort-tools";
    public const string PythonExe = SortTools + @"\venv\Scripts\python.exe";
    public const string SortScript = SortTools + @"\photo_hub\sort_inbox.py";
    public const string PeopleRoot = @"C:\Users\Augus\Pictures\People";
    public const string FaceGroups = PeopleRoot + @"\_face-groups";
    public const string FoldLog = PeopleRoot + @"\_fold-log.txt";
    public const string IPhoneBackup = @"C:\Users\Augus\Pictures\iPhone Backup";
    public const string WidgetRoot = @"C:\Users\Augus\Pictures\Widget Photos";
    public const string WidgetFeatured = WidgetRoot + @"\Featured";
    public const string FeaturedList = WidgetRoot + @"\featured-people.txt";
    public const string UpdateFeaturedScript = WidgetRoot + @"\Update-FeaturedPhotos.ps1";
    public const string StraightenFeaturedScript = @"scripts\Straighten-FeaturedJpg.ps1";
    public const string RainmeterIni = @"C:\Users\Augus\Rainmeter\Skins\Photo Widget\Photo Widget.ini";
    public const string RainmeterCurrentPhotoInc = @"C:\Users\Augus\Rainmeter\Skins\Photo Widget\@Resources\current-photo.inc";
    public const string RainmeterExe = @"C:\Program Files\Rainmeter\Rainmeter.exe";
    public const string RainmeterSkinsRoot = @"C:\Users\Augus\Rainmeter\Skins";
    public const string IllustroRoot = RainmeterSkinsRoot + @"\illustro";
    public const string LitboltSkinsRoot = RainmeterSkinsRoot + @"\Litbolt";
    public const string GroupPhotosCsv = PeopleRoot + @"\_August group photos\group-photos.csv";
    public const string AugustPerson = "August Littell";
    public const string IPhoneSyncExe = @"C:\Users\Augus\Projects\iPhone Photo Sync\artifacts\iPhone Photo Sync.exe";

    public static readonly string[] ImageExtensions =
        [".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp", ".bmp", ".tif", ".tiff", ".gif"];

    public static string HubScriptsDir =>
        Path.Combine(AppContext.BaseDirectory, "scripts");

    public static string StraightenFeaturedPath =>
        Path.Combine(HubScriptsDir, "Straighten-FeaturedJpg.ps1");
}
