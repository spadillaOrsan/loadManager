namespace LoadManager.Models.ViewModels;

public sealed class DispatchTicketViewModel
{
    public string Dispenser { get; init; } = string.Empty;

    public string Hose { get; init; } = string.Empty;

    public string Product { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    public string DispatchType { get; init; } = string.Empty;

    public string ProgrammedAmount { get; init; } = string.Empty;

    public string Price { get; init; } = string.Empty;

    public string Liters { get; init; } = string.Empty;

    public string Amount { get; init; } = string.Empty;

    public string Folio { get; init; } = string.Empty;

    public string RawResponse { get; init; } = string.Empty;
}
