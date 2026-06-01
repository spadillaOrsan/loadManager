namespace LoadManager.Models;

public sealed class ConsoleAvailabilityResult
{
    public bool IsAvailable { get; init; }

    public string UserMessage { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public string? TechnicalMessage { get; init; }
}
