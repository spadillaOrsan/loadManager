namespace LoadManager.Models;

public sealed class AppConfigurationOptions
{
    public int Tpv { get; set; } = 88;

    public int Usuario { get; set; } = 1;

    public string TipoInterfaz { get; set; } = "G";

    public decimal LimiteImporte { get; set; } = 9999m;

    public decimal LimiteLitros { get; set; } = 500m;

    public int QuantityDecimals { get; set; } = 2;

    public int AuthorizationCountdownSeconds { get; set; } = 45;

    public int DispenserCount { get; set; } = 2;

    public string ConfigurationPasswordHash { get; set; } = string.Empty;
}
