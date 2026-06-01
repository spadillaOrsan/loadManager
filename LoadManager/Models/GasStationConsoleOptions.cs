namespace LoadManager.Models;

public sealed class GasStationConsoleOptions
{
    public string IpAddress { get; set; } = string.Empty;

    public int Port { get; set; }

    public int ReadTimeoutMilliseconds { get; set; } = 10000;

    public string EncodingName { get; set; } = "ASCII";

    public Dictionary<string, string> Commands { get; set; } = [];
}
