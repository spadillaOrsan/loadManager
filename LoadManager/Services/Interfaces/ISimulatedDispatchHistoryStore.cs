using LoadManager.Models;

namespace LoadManager.Services.Interfaces;

/// <summary>
/// Guarda en memoria las cargas simuladas de Modo DEV para que Historial pueda
/// mostrarlas. No persiste a disco ni llama a la API/BD: se pierde al cerrar la
/// app o al desactivar Modo DEV.
/// </summary>
public interface ISimulatedDispatchHistoryStore
{
    void Add(SimulatedDispatchRecord record);

    IReadOnlyList<SimulatedDispatchRecord> GetFor(int dispenser, int product);

    void Clear();
}
