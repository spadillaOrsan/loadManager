using System.Globalization;
using System.Net;
using LoadManager.Models;

namespace LoadManager.Helpers;

/// <summary>
/// Construye el HTML del ticket (formato unico para despacho e historial), con los
/// datos de la estacion (tblParametros) y el numero de ticket (conteo de impresiones).
/// </summary>
public static class TicketHtmlBuilder
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");

    public static string Build(DispatchHistoryRecord record, int ticketNumber)
    {
        static string Encode(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
        static string FormatDecimal(decimal value) => value == 0m ? "0.00" : value.ToString("N2", Culture);

        var printedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Culture);
        var saleDate = record.CreatedAt?.ToString("yyyy-MM-dd HH:mm", Culture) ?? "Sin fecha";
        var product = string.IsNullOrWhiteSpace(record.ProductDescription)
            ? record.Product.ToString()
            : record.ProductDescription;

        // Solo se muestra el numero de ticket si se pudo registrar la impresion (> 0).
        var ticketNumberLine = ticketNumber > 0
            ? $"<div class=\"center ticket-num\">Ticket # {ticketNumber}</div>"
            : string.Empty;

        return "<!doctype html><html><head><meta charset=\"utf-8\">" +
            "<style>" +
            "body{color:#111;font-family:'Courier New',monospace;margin:0;padding:10px;font-size:13px;font-weight:700;}" +
            ".ticket{width:100%;max-width:280px;margin:0 auto;}" +
            ".center{text-align:center;}" +
            ".brand{font-size:14px;letter-spacing:.08em;margin-bottom:2px;}" +
            ".title{font-size:13px;letter-spacing:.08em;margin-bottom:4px;}" +
            ".ticket-num{font-size:13px;letter-spacing:.04em;margin-bottom:6px;}" +
            ".line{border-top:1px dashed #111;margin:8px 0;}" +
            ".folio-code{font-size:15px;letter-spacing:.04em;margin:7px 0;}" +
            ".row{display:flex;justify-content:space-between;gap:8px;line-height:1.45;}" +
            ".row span:first-child{white-space:nowrap;}" +
            ".row strong{text-align:right;}" +
            ".section-title{text-align:center;letter-spacing:.08em;margin:8px 0 6px;}" +
            ".product{font-size:14px;margin:8px 0 5px;}" +
            ".print-date{margin-top:18px;text-align:center;}" +
            ".end{margin-top:22px;text-align:center;font-size:15px;letter-spacing:.12em;}" +
            "</style></head><body><section class=\"ticket\">" +
            $"<div class=\"center brand\">{Encode(record.MarcaGasolinera)}</div>" +
            "<div class=\"center title\">ORIGINAL</div>" +
            ticketNumberLine +
            $"<div class=\"center\">{Encode(record.Rfc)}</div>" +
            $"<div class=\"center\">{Encode(record.NombreEmpresa)}</div>" +
            "<div class=\"line\"></div>" +
            $"<div class=\"center folio-code\">{record.Sequence}</div>" +
            "<div class=\"center\">ADMINISTRADOR DE CARGAS</div>" +
            "<div class=\"line\"></div>" +
            $"<div class=\"row\"><span>EESS :</span><strong>{record.EstacionUG}</strong></div>" +
            $"<div class=\"row\"><span>Folio :</span><strong>{record.Sequence}</strong></div>" +
            $"<div class=\"row\"><span>Fecha :</span><strong>{Encode(saleDate)}</strong></div>" +
            $"<div class=\"row\"><span>Bomba :</span><strong>{record.Dispenser}</strong></div>" +
            $"<div class=\"row\"><span>Dispensario :</span><strong>{record.Dispenser}</strong></div>" +
            $"<div class=\"row\"><span>Manguera :</span><strong>{record.Hose}</strong></div>" +
            $"<div class=\"row\"><span>Estatus :</span><strong>{Encode(record.Status)}</strong></div>" +
            "<div class=\"section-title\">-------DESPACHO-------</div>" +
            $"<div class=\"product\">{Encode(product)}</div>" +
            $"<div class=\"row\"><span>Tipo</span><strong>{record.DispatchTypeId}</strong></div>" +
            $"<div class=\"row\"><span>Cantidad</span><strong>{Encode(FormatDecimal(record.ProgrammedAmount))}</strong></div>" +
            $"<div class=\"row\"><span>Precio</span><strong>${record.Price:N2}</strong></div>" +
            $"<div class=\"row\"><span>Litros</span><strong>{record.Liters:N2}</strong></div>" +
            $"<div class=\"row\"><span>Importe</span><strong>${record.Amount:N2}</strong></div>" +
            "<div class=\"print-date\">FechaImpresion<br/>" +
            $"{Encode(printedAt)}</div>" +
            "<div class=\"end\">FIN DE TICKET</div>" +
            "</section></body></html>";
    }
}
