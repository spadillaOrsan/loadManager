using System.Text;
using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;
using Microsoft.Extensions.Options;

namespace LoadManagerApi.Services;

public sealed class AppLogService(
    IWebHostEnvironment environment,
    IOptions<FileLogOptions> options) : IAppLogService
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public async Task<string> WriteAsync(
        ApiLogEntry entry,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var level = NormalizeLevel(entry.Level);
        // Estructura: Logs/AÑO/MES/DIA/SUCCESS|ERROR/Log_yyyyMMdd.txt (uno por dia por nivel).
        var levelFolder = level == "Error" ? "ERROR" : "SUCCESS";
        var filePath = Path.Combine(GetDailyDirectory(now, levelFolder), $"Log_{now:yyyyMMdd}.txt");
        var content = BuildContent(entry, now, level);

        await AppendAsync(filePath, content, cancellationToken);
        return filePath;
    }

    private string GetDailyDirectory(DateTime now, string levelFolder)
    {
        // Raiz del proyecto/aplicacion de LoadManagerApi (carpeta del proyecto en local,
        // carpeta de la app en IIS). Si BasePath es absoluto en appsettings.json se respeta.
        var basePath = Path.IsPathRooted(options.Value.BasePath)
            ? options.Value.BasePath
            : Path.Combine(environment.ContentRootPath, options.Value.BasePath);
        var directory = Path.Combine(
            basePath,
            now.ToString("yyyy"),
            now.ToString("MM"),
            now.ToString("dd"),
            levelFolder);

        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task AppendAsync(string filePath, string content, CancellationToken cancellationToken)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private static string BuildContent(ApiLogEntry entry, DateTime createdAt, string level)
    {
        var parts = new List<string>
        {
            $"Servicio={Format(entry.Service)}",
            $"Mensaje={Format(entry.Message)}"
        };

        Add(parts, "Metodo", entry.Method);
        Add(parts, "URL", entry.Url);
        Add(parts, "IP", entry.IpAddress);
        Add(parts, "MAC", entry.MacAddress);
        Add(parts, "Dispositivo", entry.DeviceName);
        if (entry.HttpStatus.HasValue)
        {
            parts.Add($"HttpStatus={entry.HttpStatus.Value}");
        }

        if (entry.ElapsedMilliseconds.HasValue)
        {
            parts.Add($"DuracionMs={entry.ElapsedMilliseconds.Value}");
        }

        Add(parts, "Body", entry.RequestBody);
        Add(parts, "Respuesta", entry.ResponseBody);
        Add(parts, "Exception", entry.Exception);

        return $"[{FormatTimestamp(createdAt)}]{level} - {string.Join(" | ", parts)}{Environment.NewLine}";
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

    private static string Format(string? value)
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
