namespace LoadManager.Models;

public sealed class ConsoleCommandResult
{
    public bool IsSuccess { get; init; }

    public string UserMessage { get; init; } = string.Empty;

    public string CommandName { get; init; } = string.Empty;

    public string RequestFrame { get; init; } = string.Empty;

    public string? ResponseFrame { get; init; }

    public string? TechnicalMessage { get; init; }

    public string? LogPath { get; init; }
}
