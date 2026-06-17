namespace LoadManagerApi.Models;

/// <summary>
/// Se lanza cuando el dispensario ya tiene una carga activa (bitCerrada = 0)
/// al momento de intentar registrar una nueva autorizacion.
/// El controlador la convierte en HTTP 409 Conflict.
/// </summary>
public sealed class DispenserBusyException(int dispenser)
    : InvalidOperationException($"El dispensario {dispenser} ya tiene una carga activa.")
{
    public int Dispenser { get; } = dispenser;
}
