using LoadManager.Models;

namespace LoadManager.Helpers;

public static class ProductNameHelper
{
    // Con el toggle apagado, o sin override cargado para ese producto puntual, se muestra el
    // nombre original de la base tal cual.
    public static string ResolveDisplayName(string originalDescription, AppConfigurationOptions config) =>
        config.UseCustomProductNames &&
        config.ProductNameOverrides.TryGetValue(originalDescription, out var custom) &&
        !string.IsNullOrWhiteSpace(custom)
            ? custom
            : originalDescription;
}
