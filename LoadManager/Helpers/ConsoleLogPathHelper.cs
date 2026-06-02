using LoadManager.Models;

namespace LoadManager.Helpers;

public static class ConsoleLogPathHelper
{
    public static string GetDailyLogFilePath(ConsoleLogOptions options, bool isSuccess, DateTime createdAt)
    {
        var statusFolder = isSuccess ? options.SuccessFolderName : options.ErrorFolderName;
        var basePath = Path.IsPathRooted(options.BasePath)
            ? options.BasePath
            : Path.Combine(AppContext.BaseDirectory, options.BasePath);

        var folderPath = Path.Combine(
            basePath,
            statusFolder,
            createdAt.ToString("yyyy-MM"),
            createdAt.ToString("yyyy-MM-dd"));

        Directory.CreateDirectory(folderPath);

        return Path.Combine(folderPath, $"log_{createdAt:yyyyMMdd}.txt");
    }
}
