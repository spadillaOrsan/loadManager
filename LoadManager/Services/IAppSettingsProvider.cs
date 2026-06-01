using LoadManager.Models;

namespace LoadManager.Services;

public interface IAppSettingsProvider
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
