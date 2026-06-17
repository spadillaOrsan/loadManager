namespace LoadManager.Models;

/// <summary>
/// La API respondio 409 Conflict: el dispensario ya tiene una carga activa
/// registrada por otro equipo. El cliente no debe mandar el AUTH a la consola.
/// </summary>
public sealed class DispenserBusyException(int dispenser)
    : InvalidOperationException($"El dispensario {dispenser} ya tiene una carga activa en otro equipo.")
{
    public int Dispenser { get; } = dispenser;
}
