using System.Text;
using LoadManagerApi.Interfaces;
using LoadManagerApi.Helpers;
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
        var solutionRoot = SolutionPathHelper.GetSolutionRoot(environment.ContentRootPath);
        var basePath = Path.IsPathRooted(options.Value.BasePath)
            ? options.Value.BasePath
            : Path.Combine(solutionRoot, options.Value.BasePath);
        var directory = Path.Combine(
            basePath,
            now.ToString("yyyy"),
            now.ToString("MM"),
            now.ToString("yyyy-MM-dd"));
        var filePath = Path.Combine(directory, $"log_{now:yyyyMMdd}.txt");
        var content = BuildContent(entry, now);

        Directory.CreateDirectory(directory);
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

    private static string BuildContent(ApiLogEntry entry, DateTime createdAt)
    {
        var parts = new List<string>
        {
            $"Servicio={Format(entry.Service)}",
            $"Mensaje={Format(entry.Message)}"
        };

        Add(parts, "Metodo", entry.Method);
        Add(parts, "URL", entry.Url);
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
