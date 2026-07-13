using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LoadManager.Helpers;
using LoadManager.Models;
using LoadManager.Services.Interfaces;

namespace LoadManager.Services;

public sealed class ConsoleLogService(
    IAppSettingsService settingsService,
    HttpClient httpClient) : IConsoleLogService
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    // Timeout corto propio: el log nunca debe frenar la operacion esperando a la API.
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(5);

    // Tras una falla de la API se escribe local durante este lapso, para no pagar
    // un timeout por cada linea de log mientras el servidor este caido.
    private static readonly TimeSpan ApiRetryCooldown = TimeSpan.FromSeconds(30);

    private DateTime skipApiUntilUtc = DateTime.MinValue;

    public Task<string> WriteAsync(
        ConsoleCommandResult result,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync(new AppLogEntry
        {
            Level = result.IsSuccess ? "Success" : "Error",
            Service = string.IsNullOrWhiteSpace(result.CommandName)
                ? "LoadManager"
                : result.CommandName,
            Message = result.UserMessage,
            RequestBody = result.RequestFrame,
            ResponseBody = result.ResponseFrame,
            Exception = result.TechnicalMessage
        }, cancellationToken);
    }

    public async Task<string> WriteAsync(
        AppLogEntry entry,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);

        // Los logs se centralizan en el servidor via POST api/logs; el archivo local
        // queda solo como respaldo cuando la API no esta disponible.
        var apiPath = await TrySendToApiAsync(settings, entry, cancellationToken);
        if (apiPath is not null)
        {
            return apiPath;
        }

        return await WriteLocalAsync(settings, entry, cancellationToken);
    }

    private async Task<string?> TrySendToApiAsync(
        AppSettings settings,
        AppLogEntry entry,
        CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow < skipApiUntilUtc
            || !Uri.TryCreate(settings.Api.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ApiTimeout);

            var payload = new
            {
                entry.Level,
                entry.Service,
                entry.Message,
                entry.Method,
                entry.Url,
                entry.HttpStatus,
                entry.RequestBody,
                entry.ResponseBody,
                entry.Exception,
                DeviceName = GetDeviceName()
            };

            using var response = await httpClient.PostAsJsonAsync(
                new Uri(baseUri, "api/logs"),
                payload,
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                skipApiUntilUtc = DateTime.UtcNow + ApiRetryCooldown;
                return null;
            }

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(timeout.Token));
            var path = document.RootElement.TryGetProperty("path", out var value)
                ? value.GetString()
                : null;

            return string.IsNullOrWhiteSpace(path) ? "LoadManagerApi (api/logs)" : path;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            skipApiUntilUtc = DateTime.UtcNow + ApiRetryCooldown;
            return null;
        }
    }

    private async Task<string> WriteLocalAsync(
        AppSettings settings,
        AppLogEntry entry,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var filePath = ConsoleLogPathHelper.GetDailyLogFilePath(settings.ConsoleLogs, now);
        var content = BuildContent(entry, now);

        await FileLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }

        return filePath;
    }

    private static string? GetDeviceName()
    {
        try
        {
            var info = Microsoft.Maui.Devices.DeviceInfo.Current;
            return $"{info.Manufacturer} {info.Model}".Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildContent(AppLogEntry entry, DateTime createdAt)
    {
        var parts = new List<string>
        {
            $"Servicio={FormatValue(entry.Service)}",
            $"Mensaje={FormatValue(entry.Message)}"
        };

        Add(parts, "Metodo", entry.Method);
        Add(parts, "URL", entry.Url);
        if (entry.HttpStatus.HasValue)
        {
            parts.Add($"HttpStatus={entry.HttpStatus.Value}");
        }

        Add(parts, "Body", entry.RequestBody);
        Add(parts, "Respuesta", entry.ResponseBody);
        Add(parts, "Exception", entry.Exception);

        return $"[{FormatTimestamp(createdAt)}]{NormalizeLevel(entry.Level)} - {string.Join(" | ", parts)}{Environment.NewLine}";
    }

    private static void Add(ICollection<string> parts, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{name}={Normalize(value)}");
        }
    }

    private static string FormatTimestamp(DateTime value)
    {
        var suffix = value.Hour < 12 ? "a.m." : "p.m.";
        return $"{value:yyyy-MM-dd hh:mm:ss.fff} {suffix}";
    }

    private static string NormalizeLevel(string? level)
    {
        return level?.Trim().ToLowerInvariant() switch
        {
            "success" => "Success",
            "error" => "Error",
            _ => "Info"
        };
    }

    private static string FormatValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(sin datos)" : Normalize(value);
    }

    private static string Normalize(string value)
    {
        return value
            .ReplaceLineEndings(" ")
            .Replace('\0', ' ')
            .Trim();
    }
}
