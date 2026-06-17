namespace LoadManager.Models;

public sealed class DispatchHistoryRecord
{
    public int Sequence { get; init; }

    public int Tpv { get; init; }

    public int Dispenser { get; init; }

    public int Hose { get; init; }

    public int Product { get; init; }

    public string ProductDescription { get; init; } = string.Empty;

    public int DispatchTypeId { get; init; }

    public decimal ProgrammedAmount { get; init; }

    public decimal Amount { get; init; }

    public decimal Liters { get; init; }

    public decimal Price { get; init; }

    public DateTime? CreatedAt { get; init; }

    public bool IsCanceled { get; init; }

    public bool IsClosed { get; init; }

    // Datos de la estacion (tblParametros) para el ticket.
    public string MarcaGasolinera { get; init; } = string.Empty;

    public string Rfc { get; init; } = string.Empty;

    public string NombreEmpresa { get; init; } = string.Empty;

    public int EstacionUG { get; init; }

    public string Status => IsCanceled ? "Cancelada" : IsClosed ? "Cerrada" : "Abierta";
}
