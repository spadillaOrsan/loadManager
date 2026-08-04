using LoadManager.Models;
using LoadManager.Services.Interfaces;

namespace LoadManager.Services;

/// <inheritdoc cref="ISimulatedDispatchHistoryStore" />
public sealed class SimulatedDispatchHistoryStore : ISimulatedDispatchHistoryStore
{
    private readonly object gate = new();
    private readonly List<SimulatedDispatchRecord> records = [];

    public void Add(SimulatedDispatchRecord record)
    {
        lock (gate)
        {
            records.Add(record);
        }
    }

    public IReadOnlyList<SimulatedDispatchRecord> GetFor(int dispenser, int product)
    {
        lock (gate)
        {
            return records
                .Where(r => r.Dispenser == dispenser && r.Product == product)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            records.Clear();
        }
    }
}
