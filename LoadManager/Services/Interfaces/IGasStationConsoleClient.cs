using LoadManager.Models;

namespace LoadManager.Services.Interfaces;

public interface IGasStationConsoleClient
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
}
