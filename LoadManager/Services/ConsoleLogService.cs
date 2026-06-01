using System.Text;
using LoadManager.Models;

namespace LoadManager.Services;

public sealed class ConsoleLogService(IAppSettingsProvider settingsProvider) : IConsoleLogService
{
    private const string Source = "LoadManager.Services.GasStationConsoleClient";
    private const string Channel = "[Despacho][TCP]";

    public async Task<string> WriteAsync(ConsoleCommandResult result, CancellationToken cancellationToken = default)
    {
        var settings = await settingsProvider.GetSettingsAsync(cancellationToken);
        var options = settings.ConsoleLogs;
        var now = DateTime.Now;
        var statusFolder = result.IsSuccess ? options.SuccessFolderName : options.ErrorFolderName;
        var basePath = Path.IsPathRooted(options.BasePath)
            ? options.BasePath
            : Path.Combine(AppContext.BaseDirectory, options.BasePath);

        var folderPath = Path.Combine(
            basePath,
            statusFolder,
            now.ToString("yyyy-MM"),
            now.ToString("yyyy-MM-dd"));

        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, $"log_{now:yyyyMMdd}.txt");
        var content = BuildContent(result, now);

        await File.AppendAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken);

        return filePath;
    }

    private static string BuildContent(ConsoleCommandResult result, DateTime createdAt)
    {
        var builder = new StringBuilder();
        var level = result.IsSuccess ? "DBG" : "ERR";
        var messageType = result.IsSuccess ? "S" : "E";
        var message = $"{Channel} [P] {FormatValue(result.RequestFrame)} | [R] {FormatValue(result.ResponseFrame)} | [{messageType}] {result.UserMessage}";

        if (!string.IsNullOrWhiteSpace(result.TechnicalMessage))
        {
            message += $" | [I] {Normalize(result.TechnicalMessage)}";
        }

        AppendLine(builder, createdAt, level, message);

        return builder.ToString();
    }

    private static void AppendLine(StringBuilder builder, DateTime createdAt, string level, string message)
    {
        builder.AppendLine($"{createdAt:yyyy-MM-dd HH:mm:ss.fff} [{level}] {Source} | {message}");
    }

    private static string FormatValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(sin datos)" : value;
    }

    private static string Normalize(string value)
    {
        return value.ReplaceLineEndings(" | ");
    }
}
