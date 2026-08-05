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

/// <summary>
/// Impresora termica ESC/POS por Bluetooth. En Windows via puerto serie COM
/// (perfil SPP); en Android via socket Bluetooth directo al dispositivo vinculado.
/// Ademas de imprimir, permite enumerar los destinos y probar la conexion.
/// </summary>
public interface IBluetoothEscPosPrinterService : IPrinterService
{
    /// <summary>
    /// Enumera los destinos Bluetooth disponibles: en Windows los puertos COM
    /// registrados bajo BTHENUM; en Android los dispositivos vinculados.
    /// </summary>
    Task<IReadOnlyList<BluetoothPrinterEndpoint>> GetPrintersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre y cierra la conexion con el destino (puerto COM o direccion MAC) para
    /// validar que responda. El PrintOutcome trae un mensaje claro si esta ocupado,
    /// apagado o fuera de alcance.
    /// </summary>
    Task<PrintOutcome> TestAsync(string endpointId, CancellationToken cancellationToken = default);
}

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
