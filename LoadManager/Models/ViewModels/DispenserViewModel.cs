namespace LoadManager.Models.ViewModels;

public sealed class DispenserViewModel
{
    public string Number { get; init; } = string.Empty;

    public string StatusText { get; set; } = "Revisando";

    public string ImagePath { get; set; } = "images/dispensers/inactivo.png";

    public string? LastResponse { get; set; }

    public string? StatusCode { get; set; }

    public bool IsAvailable { get; set; }
}
