namespace LoadManager.Contracts.Models;

public sealed class FuelAuthorizationRequest
{
    public int Tpv { get; init; }

    public int TipoVenta { get; init; }

    public int Dispensario { get; init; }

    public int Manguera { get; init; }

    public int Producto { get; init; }

    public int Usuario { get; init; }

    public string Tarjeta { get; init; } = string.Empty;

    public int TipoProgramado { get; init; }

    public decimal Programado { get; init; }

    public string Cliente { get; init; } = string.Empty;

    public string BandaMagnetica { get; init; } = string.Empty;

    public string Vehiculo { get; init; } = string.Empty;

    public string Odometro { get; init; } = string.Empty;
}
