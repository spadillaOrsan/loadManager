namespace LoadManager.Models;

public sealed class SimulatedDispatchRecord
{
    public int Sequence { get; init; }

    public int Tpv { get; init; }

    public int Dispenser { get; init; }

    public int Hose { get; init; }

    public int Product { get; init; }

    public string ProductDescription { get; init; } = string.Empty;

    public int DispatchTypeId { get; init; }

    public decimal ProgrammedAmount { get; init; }

    public decimal Amount { get; init; }

    public decimal Liters { get; init; }

    public decimal Price { get; init; }

    public DateTime CreatedAt { get; init; }
}
