using LoadManager.Models;

namespace LoadManager.Services;

public interface IConsoleLogService
{
    Task<string> WriteAsync(ConsoleCommandResult result, CancellationToken cancellationToken = default);
}
