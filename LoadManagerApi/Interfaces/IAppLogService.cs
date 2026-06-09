using LoadManagerApi.Models;

namespace LoadManagerApi.Interfaces;

public interface IAppLogService
{
    Task<string> WriteAsync(
        ApiLogEntry entry,
        CancellationToken cancellationToken = default);
}
