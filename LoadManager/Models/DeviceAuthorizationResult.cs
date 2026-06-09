namespace LoadManager.Models;

public sealed class DeviceAuthorizationResult
{
    public bool IsAuthorized { get; init; }

    public string IpAddress { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
