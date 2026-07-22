using System.Text.Json;
using System.Text.Json.Nodes;
using LoadManager.Helpers;
using LoadManager.Models;
using LoadManager.Services.Interfaces;

namespace LoadManager.Services;

public sealed class AppSettingsService : IAppSettingsService
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

        await using var defaultStream = await FileSystem.OpenAppPackageFileAsync("appsettings.json");
        using var defaultReader = new StreamReader(defaultStream);
        var defaultSettings = DeserializeSettings(await defaultReader.ReadToEndAsync(cancellationToken));

        var userSettingsPath = AppSettingsPathHelper.GetUserSettingsPath();
        if (File.Exists(userSettingsPath))
        {
            var userJson = await File.ReadAllTextAsync(userSettingsPath, cancellationToken);
            settings = MergeMissingSettings(DeserializeSettings(userJson), defaultSettings);

            return settings;
        }

        settings = defaultSettings;

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

    private static AppSettings MergeMissingSettings(AppSettings current, AppSettings defaults)
    {
        current.GasStationConsole.IpAddress = UseText(current.GasStationConsole.IpAddress, defaults.GasStationConsole.IpAddress);
        current.GasStationConsole.Port = UsePositive(current.GasStationConsole.Port, defaults.GasStationConsole.Port);
        current.GasStationConsole.ConnectionTimeoutMilliseconds = UsePositive(current.GasStationConsole.ConnectionTimeoutMilliseconds, defaults.GasStationConsole.ConnectionTimeoutMilliseconds);
        current.GasStationConsole.ReadTimeoutMilliseconds = UsePositive(current.GasStationConsole.ReadTimeoutMilliseconds, defaults.GasStationConsole.ReadTimeoutMilliseconds);
        current.GasStationConsole.ReceiveBufferSize = UsePositive(current.GasStationConsole.ReceiveBufferSize, defaults.GasStationConsole.ReceiveBufferSize);
        current.GasStationConsole.MaxSendAttempts = UsePositive(current.GasStationConsole.MaxSendAttempts, defaults.GasStationConsole.MaxSendAttempts);
        current.GasStationConsole.EncodingName = UseText(current.GasStationConsole.EncodingName, defaults.GasStationConsole.EncodingName);
        if (current.GasStationConsole.Commands.Count == 0)
        {
            current.GasStationConsole.Commands = new Dictionary<string, string>(defaults.GasStationConsole.Commands);
        }

        current.AppConfiguration.Tpv = UsePositive(current.AppConfiguration.Tpv, defaults.AppConfiguration.Tpv);
        current.AppConfiguration.Usuario = UsePositive(current.AppConfiguration.Usuario, defaults.AppConfiguration.Usuario);
        current.AppConfiguration.TipoVenta = UsePositive(current.AppConfiguration.TipoVenta, defaults.AppConfiguration.TipoVenta);
        current.AppConfiguration.TipoInterfaz = UseText(current.AppConfiguration.TipoInterfaz, defaults.AppConfiguration.TipoInterfaz);
        current.AppConfiguration.LimiteImporte = UsePositive(current.AppConfiguration.LimiteImporte, defaults.AppConfiguration.LimiteImporte);
        current.AppConfiguration.LimiteLitros = UsePositive(current.AppConfiguration.LimiteLitros, defaults.AppConfiguration.LimiteLitros);
        current.AppConfiguration.CantidadTanqueLleno = UsePositive(current.AppConfiguration.CantidadTanqueLleno, defaults.AppConfiguration.CantidadTanqueLleno);
        current.AppConfiguration.QuantityDecimals = current.AppConfiguration.QuantityDecimals >= 0
            ? current.AppConfiguration.QuantityDecimals
            : defaults.AppConfiguration.QuantityDecimals;
        current.AppConfiguration.AuthorizationCountdownSeconds = UsePositive(current.AppConfiguration.AuthorizationCountdownSeconds, defaults.AppConfiguration.AuthorizationCountdownSeconds);
        current.AppConfiguration.AuthorizationWarningSeconds = UsePositive(current.AppConfiguration.AuthorizationWarningSeconds, defaults.AppConfiguration.AuthorizationWarningSeconds);
        current.AppConfiguration.AuthorizationPollingMilliseconds = UsePositive(current.AppConfiguration.AuthorizationPollingMilliseconds, defaults.AppConfiguration.AuthorizationPollingMilliseconds);
        current.AppConfiguration.FuelingPollingMilliseconds = UsePositive(current.AppConfiguration.FuelingPollingMilliseconds, defaults.AppConfiguration.FuelingPollingMilliseconds);
        current.AppConfiguration.HoseRestorePollingMilliseconds = UsePositive(current.AppConfiguration.HoseRestorePollingMilliseconds, defaults.AppConfiguration.HoseRestorePollingMilliseconds);
        current.AppConfiguration.ToastDurationMilliseconds = UsePositive(current.AppConfiguration.ToastDurationMilliseconds, defaults.AppConfiguration.ToastDurationMilliseconds);
        current.AppConfiguration.DispenserCount = UsePositive(current.AppConfiguration.DispenserCount, defaults.AppConfiguration.DispenserCount);
        current.AppConfiguration.ConfigurationPasswordHash = UseText(current.AppConfiguration.ConfigurationPasswordHash, defaults.AppConfiguration.ConfigurationPasswordHash);
        current.AppConfiguration.HistorialTopRecords = current.AppConfiguration.HistorialTopRecords is >= 3 and <= 15
            ? current.AppConfiguration.HistorialTopRecords
            : defaults.AppConfiguration.HistorialTopRecords;
        current.AppConfiguration.DispenserLabel = UseText(current.AppConfiguration.DispenserLabel, defaults.AppConfiguration.DispenserLabel);

        current.Api.BaseUrl = UseText(current.Api.BaseUrl, defaults.Api.BaseUrl);
        current.Api.RequestTimeoutSeconds = UsePositive(current.Api.RequestTimeoutSeconds, defaults.Api.RequestTimeoutSeconds);

        current.Printer.Mode = UseText(current.Printer.Mode, defaults.Printer.Mode);
        current.Printer.BaudRate = UsePositive(current.Printer.BaudRate, defaults.Printer.BaudRate);
        current.Printer.DataBits = UsePositive(current.Printer.DataBits, defaults.Printer.DataBits);
        current.Printer.StopBits = UseText(current.Printer.StopBits, defaults.Printer.StopBits);
        current.Printer.Parity = UseText(current.Printer.Parity, defaults.Printer.Parity);
        current.Printer.PaperColumns = UsePositive(current.Printer.PaperColumns, defaults.Printer.PaperColumns);

        current.ConsoleLogs.BasePath = UseText(current.ConsoleLogs.BasePath, defaults.ConsoleLogs.BasePath);

        return current;
    }

    private static int UsePositive(int value, int fallback) => value > 0 ? value : fallback;

    private static decimal UsePositive(decimal value, decimal fallback) => value > 0 ? value : fallback;

    private static string UseText(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

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
        EncryptNumber(jsonNode, "GasStationConsole", "ConnectionTimeoutMilliseconds");
        EncryptNumber(jsonNode, "GasStationConsole", "ReadTimeoutMilliseconds");
        EncryptNumber(jsonNode, "GasStationConsole", "ReceiveBufferSize");
        EncryptNumber(jsonNode, "GasStationConsole", "MaxSendAttempts");
        EncryptString(jsonNode, "GasStationConsole", "EncodingName");
        EncryptDictionaryValues(jsonNode, "GasStationConsole", "Commands");

        EncryptNumber(jsonNode, "AppConfiguration", "Tpv");
        EncryptNumber(jsonNode, "AppConfiguration", "Usuario");
        EncryptNumber(jsonNode, "AppConfiguration", "TipoVenta");
        EncryptString(jsonNode, "AppConfiguration", "TipoInterfaz");
        EncryptNumber(jsonNode, "AppConfiguration", "LimiteImporte");
        EncryptNumber(jsonNode, "AppConfiguration", "LimiteLitros");
        EncryptNumber(jsonNode, "AppConfiguration", "CantidadTanqueLleno");
        EncryptNumber(jsonNode, "AppConfiguration", "QuantityDecimals");
        EncryptNumber(jsonNode, "AppConfiguration", "AuthorizationCountdownSeconds");
        EncryptNumber(jsonNode, "AppConfiguration", "AuthorizationWarningSeconds");
        EncryptNumber(jsonNode, "AppConfiguration", "AuthorizationPollingMilliseconds");
        EncryptNumber(jsonNode, "AppConfiguration", "FuelingPollingMilliseconds");
        EncryptNumber(jsonNode, "AppConfiguration", "HoseRestorePollingMilliseconds");
        EncryptNumber(jsonNode, "AppConfiguration", "ToastDurationMilliseconds");
        EncryptNumber(jsonNode, "AppConfiguration", "DispenserCount");
        EncryptBoolean(jsonNode, "AppConfiguration", "DispenserFillSequential");
        EncryptString(jsonNode, "AppConfiguration", "DispenserNumbers");
        EncryptString(jsonNode, "AppConfiguration", "ConfigurationPasswordHash");
        EncryptNumber(jsonNode, "AppConfiguration", "HistorialTopRecords");
        EncryptString(jsonNode, "AppConfiguration", "DispenserLabel");
        EncryptBoolean(jsonNode, "AppConfiguration", "UseCustomProductNames");
        EncryptDictionaryValues(jsonNode, "AppConfiguration", "ProductNameOverrides");

        EncryptString(jsonNode, "Api", "BaseUrl");
        EncryptNumber(jsonNode, "Api", "RequestTimeoutSeconds");

        EncryptString(jsonNode, "Printer", "Mode");
        EncryptString(jsonNode, "Printer", "ComPort");
        EncryptString(jsonNode, "Printer", "BluetoothAddress");
        EncryptString(jsonNode, "Printer", "BluetoothName");
        EncryptNumber(jsonNode, "Printer", "BaudRate");
        EncryptNumber(jsonNode, "Printer", "DataBits");
        EncryptString(jsonNode, "Printer", "StopBits");
        EncryptString(jsonNode, "Printer", "Parity");
        EncryptNumber(jsonNode, "Printer", "PaperColumns");
    }

    private static void DecryptSettingsNode(JsonNode jsonNode)
    {
        DecryptString(jsonNode, "GasStationConsole", "IpAddress");
        DecryptNumber(jsonNode, "GasStationConsole", "Port");
        DecryptNumber(jsonNode, "GasStationConsole", "ConnectionTimeoutMilliseconds");
        DecryptNumber(jsonNode, "GasStationConsole", "ReadTimeoutMilliseconds");
        DecryptNumber(jsonNode, "GasStationConsole", "ReceiveBufferSize");
        DecryptNumber(jsonNode, "GasStationConsole", "MaxSendAttempts");
        DecryptString(jsonNode, "GasStationConsole", "EncodingName");
        DecryptDictionaryValues(jsonNode, "GasStationConsole", "Commands");

        DecryptNumber(jsonNode, "AppConfiguration", "Tpv");
        DecryptNumber(jsonNode, "AppConfiguration", "Usuario");
        DecryptNumber(jsonNode, "AppConfiguration", "TipoVenta");
        DecryptString(jsonNode, "AppConfiguration", "TipoInterfaz");
        DecryptNumber(jsonNode, "AppConfiguration", "LimiteImporte");
        DecryptNumber(jsonNode, "AppConfiguration", "LimiteLitros");
        DecryptNumber(jsonNode, "AppConfiguration", "CantidadTanqueLleno");
        DecryptNumber(jsonNode, "AppConfiguration", "QuantityDecimals");
        DecryptNumber(jsonNode, "AppConfiguration", "AuthorizationCountdownSeconds");
        DecryptNumber(jsonNode, "AppConfiguration", "AuthorizationWarningSeconds");
        DecryptNumber(jsonNode, "AppConfiguration", "AuthorizationPollingMilliseconds");
        DecryptNumber(jsonNode, "AppConfiguration", "FuelingPollingMilliseconds");
        DecryptNumber(jsonNode, "AppConfiguration", "HoseRestorePollingMilliseconds");
        DecryptNumber(jsonNode, "AppConfiguration", "ToastDurationMilliseconds");
        DecryptNumber(jsonNode, "AppConfiguration", "DispenserCount");
        DecryptBoolean(jsonNode, "AppConfiguration", "DispenserFillSequential");
        DecryptString(jsonNode, "AppConfiguration", "DispenserNumbers");
        DecryptString(jsonNode, "AppConfiguration", "ConfigurationPasswordHash");
        DecryptNumber(jsonNode, "AppConfiguration", "HistorialTopRecords");
        DecryptString(jsonNode, "AppConfiguration", "DispenserLabel");
        DecryptBoolean(jsonNode, "AppConfiguration", "UseCustomProductNames");
        DecryptDictionaryValues(jsonNode, "AppConfiguration", "ProductNameOverrides");

        DecryptString(jsonNode, "Api", "BaseUrl");
        DecryptNumber(jsonNode, "Api", "RequestTimeoutSeconds");

        DecryptString(jsonNode, "Printer", "Mode");
        DecryptString(jsonNode, "Printer", "ComPort");
        DecryptString(jsonNode, "Printer", "BluetoothAddress");
        DecryptString(jsonNode, "Printer", "BluetoothName");
        DecryptNumber(jsonNode, "Printer", "BaudRate");
        DecryptNumber(jsonNode, "Printer", "DataBits");
        DecryptString(jsonNode, "Printer", "StopBits");
        DecryptString(jsonNode, "Printer", "Parity");
        DecryptNumber(jsonNode, "Printer", "PaperColumns");
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
