using LoadManager.Models;

namespace LoadManager.Services.Interfaces;

public interface IGasStationService
{
    bool IsConnected { get; }

    Task<ConsoleAvailabilityResult> ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync();

    Task<ConsoleAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

    Task<ConsoleCommandResult> SendConfiguredCommandAsync(string commandName, CancellationToken cancellationToken = default);

    Task<ConsoleCommandResult> SendRawFrameAsync(string requestFrame, CancellationToken cancellationToken = default);

    Task<ConsoleCommandResult> SendDispenserSummaryAsync(CancellationToken cancellationToken = default);

    Task<ConsoleCommandResult> SendDispenserDetailAsync(string dispenserNumber, CancellationToken cancellationToken = default);

    Task<ConsoleCommandResult> SendDispenserStatusAsync(string dispenserNumber, CancellationToken cancellationToken = default);

    Task<ConsoleCommandResult> CheckDatabaseConnectionAsync(CancellationToken cancellationToken = default);

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
