namespace LoadManager.Contracts.Models;

public sealed class DispenserProductOption
{
    public int Hose { get; init; }

    public int ProductId { get; init; }

    public string Description { get; init; } = string.Empty;

    public decimal? Price { get; init; }
}
