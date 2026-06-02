namespace LoadManager.Helpers;

public static class AppSettingsPathHelper
{
    public static string GetUserSettingsPath()
    {
        return Path.Combine(FileSystem.AppDataDirectory, "appsettings.json");
    }

    public static IEnumerable<string> GetWritableAppSettingsPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var baseDirectory = AppContext.BaseDirectory;
        var outputSettingsPath = Path.Combine(baseDirectory, "appsettings.json");

        if (File.Exists(outputSettingsPath))
        {
            paths.Add(outputSettingsPath);
        }

        var current = new DirectoryInfo(baseDirectory);
        while (current is not null)
        {
            var projectSettingsPath = Path.Combine(current.FullName, "appsettings.json");
            var projectFilePath = Path.Combine(current.FullName, "LoadManager.csproj");

            if (File.Exists(projectSettingsPath) && File.Exists(projectFilePath))
            {
                paths.Add(projectSettingsPath);
                break;
            }

            current = current.Parent;
        }

        return paths;
    }
}
