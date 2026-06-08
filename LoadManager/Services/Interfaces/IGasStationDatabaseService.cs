using LoadManager.Models;

namespace LoadManager.Services.Interfaces;

public interface IGasStationDatabaseService
{
    Task<ConsoleCommandResult> CheckConnectionAsync(CancellationToken cancellationToken = default);

    Task<int> RegisterAuthorizationAsync(FuelAuthorizationRequest request, CancellationToken cancellationToken = default);

    Task UpdateDispenserStatusAsync(int dispenser, int status, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DispenserProductOption>> GetDispenserProductsAsync(int dispenser, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DispatchTypeOption>> GetDispatchTypesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DispatchHistoryRecord>> GetDispatchHistoryAsync(string? folio, CancellationToken cancellationToken = default);
}
