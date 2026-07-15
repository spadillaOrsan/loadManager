namespace LoadManager.Models;

/// <summary>
/// Destino de impresion Bluetooth seleccionable en Configuracion.
/// En Windows Id y DisplayName son el puerto COM (ej. "COM4"); en Android
/// Id es la direccion MAC del dispositivo vinculado y DisplayName su nombre.
/// </summary>
public sealed class BluetoothPrinterEndpoint
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }
}
