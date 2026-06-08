namespace LoadManager.Models;

public sealed class ApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public int RequestTimeoutSeconds { get; set; }
}
