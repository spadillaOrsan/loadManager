namespace LoadManager.Models.ViewModels;

public sealed record HistorialProductViewModel(
    int Id,
    string Description,
    string CssClass,
    string ImagePath);

public sealed class HistorialCardViewModel
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

    public DateTime? CreatedAt { get; init; }

    public bool IsCanceled { get; init; }

    public bool IsClosed { get; init; }

    public bool IsSimulated { get; init; }

    public string Status => IsCanceled ? "Cancelada" : IsClosed ? "Cerrada" : "Abierta";

    public static HistorialCardViewModel FromReal(DispatchHistoryRecord record) => new()
    {
        Sequence = record.Sequence,
        Tpv = record.Tpv,
        Dispenser = record.Dispenser,
        Hose = record.Hose,
        Product = record.Product,
        ProductDescription = record.ProductDescription,
        DispatchTypeId = record.DispatchTypeId,
        ProgrammedAmount = record.ProgrammedAmount,
        Amount = record.Amount,
        Liters = record.Liters,
        Price = record.Price,
        CreatedAt = record.CreatedAt,
        IsCanceled = record.IsCanceled,
        IsClosed = record.IsClosed,
        IsSimulated = false
    };

    public static HistorialCardViewModel FromSimulated(SimulatedDispatchRecord record) => new()
    {
        Sequence = record.Sequence,
        Tpv = record.Tpv,
        Dispenser = record.Dispenser,
        Hose = record.Hose,
        Product = record.Product,
        ProductDescription = record.ProductDescription,
        DispatchTypeId = record.DispatchTypeId,
        ProgrammedAmount = record.ProgrammedAmount,
        Amount = record.Amount,
        Liters = record.Liters,
        Price = record.Price,
        CreatedAt = record.CreatedAt,
        IsCanceled = false,
        IsClosed = true,
        IsSimulated = true
    };

    public DispatchHistoryRecord ToDispatchHistoryRecord() => new()
    {
        Sequence = Sequence,
        Tpv = Tpv,
        Dispenser = Dispenser,
        Hose = Hose,
        Product = Product,
        ProductDescription = ProductDescription,
        DispatchTypeId = DispatchTypeId,
        ProgrammedAmount = ProgrammedAmount,
        Amount = Amount,
        Liters = Liters,
        Price = Price,
        CreatedAt = CreatedAt,
        IsCanceled = IsCanceled,
        IsClosed = IsClosed
    };
}
