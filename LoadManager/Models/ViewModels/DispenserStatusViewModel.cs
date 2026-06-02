namespace LoadManager.Models.ViewModels;

public sealed class DispenserStatusViewModel
{
    public string Number { get; init; } = string.Empty;

    public string StatusCode { get; set; } = string.Empty;

    public string StatusText { get; set; } = "Sin consultar";

    public string ImagePath { get; set; } = "images/dispensers/inactivo.png";

    public string? LastResponse { get; set; }
}
