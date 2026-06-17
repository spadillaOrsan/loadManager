namespace LoadManager.Services.Interfaces;

public interface IReceiptPrinterService
{
    /// <summary>
    /// Imprime el HTML con el dialogo del sistema (selector de impresora).
    /// Devuelve true si lo manejo de forma nativa (Android); false si el llamador
    /// debe usar el dialogo del navegador (window.print() en Windows).
    /// </summary>
    Task<bool> PrintHtmlAsync(string html, string jobName, CancellationToken cancellationToken = default);
}
