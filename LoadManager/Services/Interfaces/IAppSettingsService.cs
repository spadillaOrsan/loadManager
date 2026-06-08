using LoadManager.Models;

namespace LoadManager.Services.Interfaces;

public interface IAppSettingsService
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
