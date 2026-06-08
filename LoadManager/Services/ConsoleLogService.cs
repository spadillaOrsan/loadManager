using System.Text;
using LoadManager.Helpers;
using LoadManager.Models;
using LoadManager.Services.Interfaces;

namespace LoadManager.Services;

public sealed class ConsoleLogService(IAppSettingsService settingsService) : IConsoleLogService
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);

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
