namespace LoadManager.Models;

public sealed class DatabaseOptions
{
    public string Server { get; set; } = string.Empty;

    public string Database { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool TrustServerCertificate { get; set; } = true;

    public bool Encrypt { get; set; }

    public int ConnectionTimeoutSeconds { get; set; } = 10;
}
