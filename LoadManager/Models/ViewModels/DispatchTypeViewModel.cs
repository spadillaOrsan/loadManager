namespace LoadManager.Models.ViewModels;

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
