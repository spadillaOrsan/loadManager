using LoadManagerApi.Models;

namespace LoadManagerApi.Interfaces;

public interface IGasStationService
{
    Task<DeviceAuthorizationResult> CheckDeviceAuthorizationAsync(
        string ipAddress,
        CancellationToken cancellationToken = default);

    Task<ConsoleCommandResult> CheckConnectionAsync(CancellationToken cancellationToken = default);

    Task<int> RegisterAuthorizationAsync(
        FuelAuthorizationRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateDispenserStatusAsync(
        int dispenser,
        int status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DispenserProductOption>> GetDispenserProductsAsync(
        int dispenser,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DispatchTypeOption>> GetDispatchTypesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DispatchHistoryRecord>> GetDispatchHistoryAsync(
        string? folio,
        CancellationToken cancellationToken = default);
}
