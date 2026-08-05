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

    Task<DeviceAuthorizationResult> CheckDeviceAuthorizationAsync(CancellationToken cancellationToken = default);

    Task<int> RegisterAuthorizationAsync(
        FuelAuthorizationRequest request,
        CancellationToken cancellationToken = default);

    Task<int> RegisterImpressionAsync(
        int transaccion,
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

    Task<IReadOnlyList<DispatchTypeOption>> GetActiveProductsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DispatchHistoryRecord>> GetHistorialAsync(
        int dispenser,
        int product,
        int top,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Rastrea la carga (autorizacion) activa para poder cancelarla desde el ciclo de
/// vida de la app (ej. al pasar a segundo plano). Lo alimenta la pantalla de despacho.
/// </summary>
public interface IActiveDispatchTracker
{
    /// <summary>Dispensario con autorizacion/carga activa, o null si no hay ninguna.</summary>
    int? ActiveDispenser { get; set; }

    /// <summary>Se dispara cada vez que ActiveDispenser cambia de valor.</summary>
    event Action? StateChanged;

    /// <summary>Cancela en la consola la carga activa (CAUTH) si la hay.</summary>
    Task CancelActiveDispatchAsync(CancellationToken cancellationToken = default);
}

public interface IConnectionValidationService
{
    bool? DatabaseIsConnected { get; }

    string DatabaseStatus { get; }

    DateTime? DatabaseValidatedAt { get; }

    void SetDatabaseResult(ConsoleCommandResult result);

    void ResetDatabase();
}
