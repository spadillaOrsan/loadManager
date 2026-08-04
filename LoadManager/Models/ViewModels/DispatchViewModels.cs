namespace LoadManager.Models.ViewModels;

public sealed record DispatchStep(int Index, string Name);

public sealed class DispenserViewModel
{
    public string Number { get; init; } = string.Empty;

    public string StatusText { get; set; } = "Revisando";

    public string ImagePath { get; set; } = "images/dispensers/inactivo.png";

    public string? LastResponse { get; set; }

    public string? StatusCode { get; set; }

    public bool IsAvailable { get; set; }
}

public sealed class DispenserStatusViewModel
{
    public string Number { get; init; } = string.Empty;

    public string StatusCode { get; set; } = string.Empty;

    public string StatusText { get; set; } = "Sin consultar";

    public string ImagePath { get; set; } = "images/dispensers/inactivo.png";

    public string? LastResponse { get; set; }
}

public sealed record DispatchTypeViewModel(int Id, string Description)
{
    public bool IsMoney =>
        Description.Contains("peso", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("importe", StringComparison.OrdinalIgnoreCase) ||
        Id == 1;

    public bool IsVolume =>
        Description.Contains("litro", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("volumen", StringComparison.OrdinalIgnoreCase) ||
        Id == 2;

    public bool IsFull =>
        Description.Contains("lleno", StringComparison.OrdinalIgnoreCase) ||
        Description.Contains("tanque", StringComparison.OrdinalIgnoreCase) ||
        Id == 3;

    public int AuthorizationLoadType =>
        IsFull ? 3 :
        IsVolume ? 2 :
        IsMoney ? 1 :
        Id;
}

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

public sealed record FuelProductViewModel(
    string Key,
    string Name,
    string CssClass,
    string ImagePath,
    int ProductCode,
    string AuthorizationCode,
    int Hose,
    decimal? Price = null);
