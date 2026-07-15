using LoadManager.Models;

namespace LoadManager.Services.Interfaces;

public interface IReceiptPrinterService
{
    /// <summary>
    /// Imprime el ticket por la ruta configurada en PrinterOptions: dialogo del
    /// sistema (Windows/Android) o ESC/POS por COM Bluetooth (Windows). Si el
    /// resultado trae Handled=false, el llamador debe invocar el fallback JS
    /// (uaacPrintHtml) con el Html del PrintOutcome.
    /// </summary>
    Task<PrintOutcome> PrintReceiptAsync(
        DispatchHistoryRecord record,
        int ticketNumber,
        string jobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imprime un HTML arbitrario con el dialogo del sistema (selector de impresora).
    /// Devuelve true si lo manejo de forma nativa (Android); false si el llamador
    /// debe usar el dialogo del navegador (window.print() en Windows).
    /// </summary>
    Task<bool> PrintHtmlAsync(string html, string jobName, CancellationToken cancellationToken = default);
}
