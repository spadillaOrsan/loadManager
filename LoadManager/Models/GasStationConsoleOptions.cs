namespace LoadManager.Models;

public sealed class GasStationConsoleOptions
{
    public string IpAddress { get; set; } = string.Empty;

    public int Port { get; set; }

    public int ConnectionTimeoutMilliseconds { get; set; }

    public int ReadTimeoutMilliseconds { get; set; }

    public int ReceiveBufferSize { get; set; }

    public int MaxSendAttempts { get; set; }

    public string EncodingName { get; set; } = string.Empty;

    public Dictionary<string, string> Commands { get; set; } = [];
}
