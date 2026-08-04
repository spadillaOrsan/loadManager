namespace LoadManager.Models;

public sealed class AppLogEntry
{
    public string Level { get; init; } = "Info";

    public string Service { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? Method { get; init; }

    public string? Url { get; init; }

    public int? HttpStatus { get; init; }

    public string? RequestBody { get; init; }

    public string? ResponseBody { get; init; }

    public string? Exception { get; init; }
}

public sealed class ConsoleLogOptions
{
    public string BasePath { get; set; } = "Logs";
}
