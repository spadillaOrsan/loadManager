namespace LoadManagerApi.Models;

public sealed class ApiLogEntry
{
    public string Level { get; init; } = "Info";

    public string Service { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? Method { get; init; }

    public string? Url { get; init; }

    public string? IpAddress { get; init; }

    public string? MacAddress { get; init; }

    public string? DeviceName { get; init; }

    public int? HttpStatus { get; init; }

    public string? RequestBody { get; init; }

    public string? ResponseBody { get; init; }

    public string? Exception { get; init; }

    public long? ElapsedMilliseconds { get; init; }
}
