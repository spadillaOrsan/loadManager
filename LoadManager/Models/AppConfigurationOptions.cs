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

    // true: la "Cantidad de dispensarios" genera numeros secuenciales 1..N.
    // false: se usan los numeros especificos capturados en DispenserNumbers.
    public bool DispenserFillSequential { get; set; } = true;

    // Numeros de dispensario especificos (ej. "2,4,6,20,13") cuando no es secuencial.
    public string DispenserNumbers { get; set; } = string.Empty;

    public string ConfigurationPasswordHash { get; set; } = string.Empty;

    // Cantidad de registros a mostrar en el historial (min 3, max 15).
    public int HistorialTopRecords { get; set; } = 3;
}
