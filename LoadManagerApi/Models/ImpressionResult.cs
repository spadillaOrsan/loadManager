namespace LoadManagerApi.Models;

/// <summary>Resultado de registrar una impresion: el numero de ticket (total de impresiones del folio).</summary>
public sealed class ImpressionResult
{
    public int TicketNumber { get; init; }
}
