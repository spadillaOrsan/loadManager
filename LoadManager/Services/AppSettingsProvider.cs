using System.Text.Json;
using System.Text.Json.Nodes;
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
            var userJson = await File.ReadAllTextAsync(userSettingsPath, cancellationToken);
            settings = DeserializeSettings(userJson);

            return settings;
        }

        await using var stream = await FileSystem.OpenAppPackageFileAsync("appsettings.json");
        using var reader = new StreamReader(stream);
        settings = DeserializeSettings(await reader.ReadToEndAsync(cancellationToken));

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
        var jsonNode = JsonSerializer.SerializeToNode(settingsToSave, SerializerOptions) ?? new JsonObject();
        EnsureConfigurationPasswordHash(jsonNode);
        EncryptSettingsNode(jsonNode);
        await JsonSerializer.SerializeAsync(stream, jsonNode, SerializerOptions, cancellationToken);
    }

    private static AppSettings DeserializeSettings(string json)
    {
        var jsonNode = JsonNode.Parse(json) ?? new JsonObject();
        DecryptSettingsNode(jsonNode);
        EnsureConfigurationPasswordHash(jsonNode);

        return jsonNode.Deserialize<AppSettings>(SerializerOptions) ?? new AppSettings();
    }

    private static void EnsureConfigurationPasswordHash(JsonNode jsonNode)
    {
        var appConfiguration = jsonNode["AppConfiguration"] as JsonObject;
        if (appConfiguration is null)
        {
            appConfiguration = [];
            jsonNode["AppConfiguration"] = appConfiguration;
        }

        if (string.IsNullOrWhiteSpace(appConfiguration["ConfigurationPasswordHash"]?.GetValue<string>()))
        {
            appConfiguration["ConfigurationPasswordHash"] = AppSettingsCryptoHelper.GetDefaultConfigurationPasswordHash();
        }
    }

    private static void EncryptSettingsNode(JsonNode jsonNode)
    {
        EncryptString(jsonNode, "GasStationConsole", "IpAddress");
        EncryptNumber(jsonNode, "GasStationConsole", "Port");
        EncryptNumber(jsonNode, "GasStationConsole", "ReadTimeoutMilliseconds");
        EncryptString(jsonNode, "GasStationConsole", "EncodingName");
        EncryptDictionaryValues(jsonNode, "GasStationConsole", "Commands");

        EncryptNumber(jsonNode, "AppConfiguration", "Tpv");
        EncryptNumber(jsonNode, "AppConfiguration", "Usuario");
        EncryptString(jsonNode, "AppConfiguration", "TipoInterfaz");
        EncryptNumber(jsonNode, "AppConfiguration", "LimiteImporte");
        EncryptNumber(jsonNode, "AppConfiguration", "LimiteLitros");
        EncryptNumber(jsonNode, "AppConfiguration", "QuantityDecimals");
        EncryptNumber(jsonNode, "AppConfiguration", "AuthorizationCountdownSeconds");
        EncryptNumber(jsonNode, "AppConfiguration", "DispenserCount");
        EncryptString(jsonNode, "AppConfiguration", "ConfigurationPasswordHash");

        EncryptString(jsonNode, "Database", "Server");
        EncryptString(jsonNode, "Database", "Database");
        EncryptString(jsonNode, "Database", "UserId");
        EncryptString(jsonNode, "Database", "Password");
        EncryptBoolean(jsonNode, "Database", "TrustServerCertificate");
        EncryptBoolean(jsonNode, "Database", "Encrypt");
        EncryptNumber(jsonNode, "Database", "ConnectionTimeoutSeconds");
    }

    private static void DecryptSettingsNode(JsonNode jsonNode)
    {
        DecryptString(jsonNode, "GasStationConsole", "IpAddress");
        DecryptNumber(jsonNode, "GasStationConsole", "Port");
        DecryptNumber(jsonNode, "GasStationConsole", "ReadTimeoutMilliseconds");
        DecryptString(jsonNode, "GasStationConsole", "EncodingName");
        DecryptDictionaryValues(jsonNode, "GasStationConsole", "Commands");

        DecryptNumber(jsonNode, "AppConfiguration", "Tpv");
        DecryptNumber(jsonNode, "AppConfiguration", "Usuario");
        DecryptString(jsonNode, "AppConfiguration", "TipoInterfaz");
        DecryptNumber(jsonNode, "AppConfiguration", "LimiteImporte");
        DecryptNumber(jsonNode, "AppConfiguration", "LimiteLitros");
        DecryptNumber(jsonNode, "AppConfiguration", "QuantityDecimals");
        DecryptNumber(jsonNode, "AppConfiguration", "AuthorizationCountdownSeconds");
        DecryptNumber(jsonNode, "AppConfiguration", "DispenserCount");
        DecryptString(jsonNode, "AppConfiguration", "ConfigurationPasswordHash");

        DecryptString(jsonNode, "Database", "Server");
        DecryptString(jsonNode, "Database", "Database");
        DecryptString(jsonNode, "Database", "UserId");
        DecryptString(jsonNode, "Database", "Password");
        DecryptBoolean(jsonNode, "Database", "TrustServerCertificate");
        DecryptBoolean(jsonNode, "Database", "Encrypt");
        DecryptNumber(jsonNode, "Database", "ConnectionTimeoutSeconds");
    }

    private static void EncryptString(JsonNode jsonNode, string section, string property)
    {
        if (jsonNode[section] is not JsonObject sectionObject || sectionObject[property] is null)
        {
            return;
        }

        var value = sectionObject[property]!.GetValue<string>();
        sectionObject[property] = AppSettingsCryptoHelper.IsEncrypted(value)
            ? value
            : AppSettingsCryptoHelper.Encrypt(value);
    }

    private static void EncryptNumber(JsonNode jsonNode, string section, string property)
    {
        EncryptScalar(jsonNode, section, property);
    }

    private static void EncryptBoolean(JsonNode jsonNode, string section, string property)
    {
        EncryptScalar(jsonNode, section, property);
    }

    private static void EncryptScalar(JsonNode jsonNode, string section, string property)
    {
        if (jsonNode[section] is not JsonObject sectionObject || sectionObject[property] is null)
        {
            return;
        }

        var value = sectionObject[property]!;
        var text = value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue)
            ? stringValue
            : value.ToJsonString();

        sectionObject[property] = AppSettingsCryptoHelper.IsEncrypted(text)
            ? text
            : AppSettingsCryptoHelper.Encrypt(text);
    }

    private static void EncryptDictionaryValues(JsonNode jsonNode, string section, string property)
    {
        if (jsonNode[section]?[property] is not JsonObject dictionary)
        {
            return;
        }

        foreach (var key in dictionary.Select(item => item.Key).ToList())
        {
            var value = dictionary[key]?.GetValue<string>() ?? string.Empty;
            dictionary[key] = AppSettingsCryptoHelper.IsEncrypted(value)
                ? value
                : AppSettingsCryptoHelper.Encrypt(value);
        }
    }

    private static void DecryptString(JsonNode jsonNode, string section, string property)
    {
        if (!TryGetEncryptedValue(jsonNode, section, property, out var sectionObject, out var value))
        {
            return;
        }

        sectionObject[property] = AppSettingsCryptoHelper.Decrypt(value);
    }

    private static void DecryptNumber(JsonNode jsonNode, string section, string property)
    {
        if (!TryGetEncryptedValue(jsonNode, section, property, out var sectionObject, out var value))
        {
            return;
        }

        var decrypted = AppSettingsCryptoHelper.Decrypt(value);
        if (long.TryParse(decrypted, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integerNumber))
        {
            sectionObject[property] = integerNumber;
            return;
        }

        sectionObject[property] = decimal.TryParse(decrypted, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var decimalNumber)
            ? JsonValue.Create(decimalNumber)
            : JsonValue.Create(0);
    }

    private static void DecryptBoolean(JsonNode jsonNode, string section, string property)
    {
        if (!TryGetEncryptedValue(jsonNode, section, property, out var sectionObject, out var value))
        {
            return;
        }

        sectionObject[property] = bool.TryParse(AppSettingsCryptoHelper.Decrypt(value), out var booleanValue) && booleanValue;
    }

    private static void DecryptDictionaryValues(JsonNode jsonNode, string section, string property)
    {
        if (jsonNode[section]?[property] is not JsonObject dictionary)
        {
            return;
        }

        foreach (var key in dictionary.Select(item => item.Key).ToList())
        {
            var value = dictionary[key]?.GetValue<string>();
            if (AppSettingsCryptoHelper.IsEncrypted(value))
            {
                dictionary[key] = AppSettingsCryptoHelper.Decrypt(value!);
            }
        }
    }

    private static bool TryGetEncryptedValue(
        JsonNode jsonNode,
        string section,
        string property,
        out JsonObject sectionObject,
        out string value)
    {
        sectionObject = [];
        value = string.Empty;

        if (jsonNode[section] is not JsonObject foundSection || foundSection[property] is null)
        {
            return false;
        }

        if (!foundSection[property]!.AsValue().TryGetValue<string>(out var foundValue)
            || !AppSettingsCryptoHelper.IsEncrypted(foundValue))
        {
            return false;
        }

        sectionObject = foundSection;
        value = foundValue;
        return true;
    }
}
