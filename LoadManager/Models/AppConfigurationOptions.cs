namespace LoadManager.Models;

public sealed class AppConfigurationOptions
{
    public int Tpv { get; set; }

    public int Usuario { get; set; }

    public int TipoVenta { get; set; }

    public string TipoInterfaz { get; set; } = string.Empty;

    public decimal LimiteImporte { get; set; }

    public decimal LimiteLitros { get; set; }

    public decimal CantidadTanqueLleno { get; set; }

    public int QuantityDecimals { get; set; }

    public int AuthorizationCountdownSeconds { get; set; }

    public int AuthorizationWarningSeconds { get; set; }

    public int AuthorizationPollingMilliseconds { get; set; }

    public int FuelingPollingMilliseconds { get; set; }

    public int HoseRestorePollingMilliseconds { get; set; }

    public int ToastDurationMilliseconds { get; set; }

    public int DispenserCount { get; set; }

    public string ConfigurationPasswordHash { get; set; } = string.Empty;
}
