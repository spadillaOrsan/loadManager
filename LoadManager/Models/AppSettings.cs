namespace LoadManager.Models;

public sealed class AppSettings
{
    public GasStationConsoleOptions GasStationConsole { get; set; } = new();

    public AppConfigurationOptions AppConfiguration { get; set; } = new();

    public DatabaseOptions Database { get; set; } = new();

    public ConsoleLogOptions ConsoleLogs { get; set; } = new();
}
