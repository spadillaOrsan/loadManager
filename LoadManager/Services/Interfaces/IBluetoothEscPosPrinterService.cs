using LoadManager.Models;

namespace LoadManager.Services.Interfaces;

/// <summary>
/// Impresora termica ESC/POS por puerto serie Bluetooth (solo Windows).
/// Ademas de imprimir, permite enumerar los COM Bluetooth y probar la conexion.
/// </summary>
public interface IBluetoothEscPosPrinterService : IPrinterService
{
    /// <summary>
    /// Enumera unicamente los puertos serie asociados a dispositivos Bluetooth
    /// (BTHENUM). En Android devuelve lista vacia.
    /// </summary>
    Task<IReadOnlyList<string>> GetBluetoothPortsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre y cierra el puerto con la configuracion actual para validar que este
    /// disponible. El PrintOutcome trae un mensaje claro si esta ocupado o no responde.
    /// </summary>
    Task<PrintOutcome> TestPortAsync(string portName, CancellationToken cancellationToken = default);
}
