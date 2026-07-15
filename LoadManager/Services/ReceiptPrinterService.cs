using LoadManager.Models;
using LoadManager.Services.Interfaces;

namespace LoadManager.Services;

/// <summary>
/// Enrutador de impresion: decide segun PrinterOptions si el ticket sale por el
/// dialogo del sistema (WindowsPrinterService) o por la termica Bluetooth ESC/POS
/// (BluetoothEscPosPrinterService). Las paginas solo hablan con este servicio.
/// </summary>
public sealed class ReceiptPrinterService(
    WindowsPrinterService windowsPrinter,
    IBluetoothEscPosPrinterService bluetoothPrinter,
    IAppSettingsService settingsProvider) : IReceiptPrinterService
{
    public async Task<PrintOutcome> PrintReceiptAsync(
        DispatchHistoryRecord record,
        int ticketNumber,
        string jobName,
        CancellationToken cancellationToken = default)
    {
        var job = new ReceiptPrintJob
        {
            Record = record,
            TicketNumber = ticketNumber,
            JobName = jobName
        };

        var options = (await settingsProvider.GetSettingsAsync(cancellationToken)).Printer;

        return options.IsBluetoothMode
            ? await bluetoothPrinter.PrintAsync(job, cancellationToken)
            : await windowsPrinter.PrintAsync(job, cancellationToken);
    }

    public Task<bool> PrintHtmlAsync(string html, string jobName, CancellationToken cancellationToken = default) =>
        windowsPrinter.PrintHtmlAsync(html, jobName, cancellationToken);
}
