using LoadManager.Models;

namespace LoadManager.Services.Interfaces;

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
