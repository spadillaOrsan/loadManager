using System.Globalization;

namespace LoadManager.Helpers;

public static class FuelProductHelper
{
    public static string GetCssClass(string description)
    {
        if (description.Contains("premium", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("supreme", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("suprema", StringComparison.OrdinalIgnoreCase))
        {
            return "fuel-premium";
        }

        if (description.Contains("extra", StringComparison.OrdinalIgnoreCase))
        {
            return "fuel-extra";
        }

        if (description.Contains("diesel", StringComparison.OrdinalIgnoreCase))
        {
            return "fuel-diesel";
        }

        return "fuel-magna";
    }

    // Icono del producto (mosaico usado en despacho e historial).
    public static string GetImagePath(string description)
    {
        if (description.Contains("diesel", StringComparison.OrdinalIgnoreCase))
        {
            return "images/products/DIESEL.png";
        }

        if (description.Contains("supreme", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("premium", StringComparison.OrdinalIgnoreCase))
        {
            return "images/products/SUPREME.png";
        }

        return "images/products/EXTRA.png";
    }

    public static string GetAuthorizationProductCode(string description, int productId)
    {
        if (description.Contains("premium", StringComparison.OrdinalIgnoreCase))
        {
            return "P";
        }

        if (description.Contains("diesel", StringComparison.OrdinalIgnoreCase))
        {
            return "D";
        }

        if (description.Contains("magna", StringComparison.OrdinalIgnoreCase))
        {
            return "G";
        }

        return productId.ToString(CultureInfo.InvariantCulture);
    }
}
