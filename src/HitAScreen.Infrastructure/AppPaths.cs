namespace HitAScreen.Infrastructure;

public static class AppPaths
{
    public static string GetAppSupportDirectory()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hitascreen");
        }

        return Path.Combine(baseDir, "HitAScreen");
    }

    public static string SettingsPath => Path.Combine(GetAppSupportDirectory(), "settings.json");

    public static string LogPath => Path.Combine(GetAppSupportDirectory(), "hitascreen.log");
}
