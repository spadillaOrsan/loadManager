using System.Text.Json;
using LoadManager.Models;

namespace LoadManager.Services;

public sealed class AppSettingsProvider : IAppSettingsProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    private AppSettings? settings;

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (settings is not null)
        {
            return settings;
        }

        var userSettingsPath = GetUserSettingsPath();
        if (File.Exists(userSettingsPath))
        {
            await using var userStream = File.OpenRead(userSettingsPath);
            settings = await JsonSerializer.DeserializeAsync<AppSettings>(userStream, SerializerOptions, cancellationToken)
                ?? new AppSettings();

            return settings;
        }

        await using var stream = await FileSystem.OpenAppPackageFileAsync("appsettings.json");
        settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
            ?? new AppSettings();

        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settingsToSave, CancellationToken cancellationToken = default)
    {
        var userSettingsPath = GetUserSettingsPath();
        await WriteSettingsFileAsync(userSettingsPath, settingsToSave, cancellationToken);

        foreach (var appSettingsPath in GetWritableAppSettingsPaths())
        {
            await WriteSettingsFileAsync(appSettingsPath, settingsToSave, cancellationToken);
        }

        settings = settingsToSave;
    }

    private static async Task WriteSettingsFileAsync(
        string path,
        AppSettings settingsToSave,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settingsToSave, SerializerOptions, cancellationToken);
    }

    private static IEnumerable<string> GetWritableAppSettingsPaths()
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

    private static string GetUserSettingsPath()
    {
        return Path.Combine(FileSystem.AppDataDirectory, "appsettings.json");
    }
}
