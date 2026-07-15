using System.Globalization;
using LoadManager.Models;

namespace LoadManager.Helpers;

/// <summary>
/// Genera el ticket en bytes ESC/POS para impresoras termicas. Replica el contenido
/// de <see cref="TicketHtmlBuilder"/> (formato unico para despacho e historial).
/// </summary>
public static class TicketEscPosBuilder
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");

    public static byte[] Build(DispatchHistoryRecord record, int ticketNumber, int paperColumns)
    {
        static string FormatDecimal(decimal value) => value == 0m ? "0.00" : value.ToString("N2", Culture);

        var printedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Culture);
        var saleDate = record.CreatedAt?.ToString("yyyy-MM-dd HH:mm", Culture) ?? "Sin fecha";
        var product = string.IsNullOrWhiteSpace(record.ProductDescription)
            ? record.Product.ToString()
            : record.ProductDescription;

        var ticket = new EscPosDocumentBuilder(paperColumns)
            .Initialize()
            .AlignCenter()
            .Bold(true)
            .TextLine(record.MarcaGasolinera)
            .Bold(false)
            .TextLine("ORIGINAL");

        // Solo se muestra el numero de ticket si se pudo registrar la impresion (> 0).
        if (ticketNumber > 0)
        {
            ticket.TextLine($"Ticket # {ticketNumber}");
        }

        ticket
            .TextLine(record.Rfc)
            .TextLine(record.NombreEmpresa)
            .Separator()
            .DoubleSize(true)
            .TextLine(record.Sequence.ToString())
            .DoubleSize(false)
            .TextLine("ADMINISTRADOR DE CARGAS")
            .Separator()
            .AlignLeft()
            .TwoColumns("EESS :", record.EstacionUG.ToString())
            .TwoColumns("Folio :", record.Sequence.ToString())
            .TwoColumns("Fecha :", saleDate)
            .TwoColumns("Bomba :", record.Dispenser.ToString())
            .TwoColumns("Dispensario :", record.Dispenser.ToString())
            .TwoColumns("Manguera :", record.Hose.ToString())
            .TwoColumns("Estatus :", record.Status)
            .AlignCenter()
            .TextLine("-------DESPACHO-------")
            .AlignLeft()
            .Bold(true)
            .TextLine(product)
            .Bold(false)
            .TwoColumns("Tipo", record.DispatchTypeId.ToString())
            .TwoColumns("Cantidad", FormatDecimal(record.ProgrammedAmount))
            .TwoColumns("Precio", $"${record.Price.ToString("N2", Culture)}")
            .TwoColumns("Litros", record.Liters.ToString("N2", Culture))
            .TwoColumns("Importe", $"${record.Amount.ToString("N2", Culture)}")
            .Feed(1)
            .AlignCenter()
            .TextLine("FechaImpresion")
            .TextLine(printedAt)
            .Feed(1)
            .TextLine("FIN DE TICKET")
            .Cut();

        return ticket.Build();
    }
}
