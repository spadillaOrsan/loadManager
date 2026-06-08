using LoadManager.Contracts.Models;
using LoadManager.Models;

namespace LoadManager.Helpers;

public static class ConsoleLogPathHelper
{
    public static string GetDailyLogFilePath(ConsoleLogOptions options, DateTime createdAt)
    {
        var basePath = Path.IsPathRooted(options.BasePath)
            ? options.BasePath
            : Path.Combine(AppContext.BaseDirectory, options.BasePath);

        var folderPath = Path.Combine(
            basePath,
            createdAt.ToString("yyyy"),
            createdAt.ToString("MM"),
            createdAt.ToString("yyyy-MM-dd"));

        Directory.CreateDirectory(folderPath);

        return Path.Combine(folderPath, $"log_{createdAt:yyyyMMdd}.txt");
    }
}
