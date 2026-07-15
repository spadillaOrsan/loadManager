namespace LoadManager.Models;

/// <summary>
/// Datos necesarios para imprimir un ticket por cualquier ruta (dialogo o ESC/POS).
/// </summary>
public sealed class ReceiptPrintJob
{
    public required DispatchHistoryRecord Record { get; init; }

    // Numero de ticket (conteo de impresiones); 0 = no se pudo registrar.
    public int TicketNumber { get; init; }

    public required string JobName { get; init; }
}
