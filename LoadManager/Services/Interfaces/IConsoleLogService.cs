using LoadManager.Models;

namespace LoadManager.Services.Interfaces;

public interface IConsoleLogService
{
    Task<string> WriteAsync(ConsoleCommandResult result, CancellationToken cancellationToken = default);
}
