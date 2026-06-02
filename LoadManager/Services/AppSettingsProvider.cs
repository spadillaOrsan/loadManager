using System.Text.Json;
using LoadManager.Helpers;
using LoadManager.Models;
using LoadManager.Services.Interfaces;

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

        var userSettingsPath = AppSettingsPathHelper.GetUserSettingsPath();
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
        var userSettingsPath = AppSettingsPathHelper.GetUserSettingsPath();
        await WriteSettingsFileAsync(userSettingsPath, settingsToSave, cancellationToken);

        foreach (var appSettingsPath in AppSettingsPathHelper.GetWritableAppSettingsPaths())
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

}
