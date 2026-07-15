using LoadManager.Models;

namespace LoadManager.Services.Interfaces;

/// <summary>
/// Ruta de impresion de tickets. Implementaciones: WindowsPrinterService (dialogo
/// del sistema) y BluetoothEscPosPrinterService (termica por puerto serie).
/// Se registran como keyed services con las llaves de <see cref="PrinterServiceKeys"/>.
/// </summary>
public interface IPrinterService
{
    Task<PrintOutcome> PrintAsync(ReceiptPrintJob job, CancellationToken cancellationToken = default);
}

public static class PrinterServiceKeys
{
    public const string Windows = "windows";
    public const string BluetoothCom = "bluetooth-com";
}
