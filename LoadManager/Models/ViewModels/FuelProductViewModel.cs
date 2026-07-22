namespace LoadManager.Models.ViewModels;

public sealed record FuelProductViewModel(
    string Key,
    string Name,
    string CssClass,
    string ImagePath,
    int ProductCode,
    string AuthorizationCode,
    int Hose,
    decimal? Price = null);
