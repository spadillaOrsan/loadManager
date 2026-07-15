namespace LoadManager.Models;

/// <summary>
/// Resultado de un intento de impresion.
/// Handled=false indica que el llamador debe usar el fallback JS (window.print)
/// con el Html incluido; ErrorMessage trae un texto claro para mostrar en toast.
/// </summary>
public sealed class PrintOutcome
{
    public bool Handled { get; init; }

    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    // Descripcion de la ruta usada, para logs (ej. "Impresora nativa", "ESC/POS COM4").
    public string Route { get; init; } = string.Empty;

    // HTML del ticket cuando Handled=false (fallback al dialogo del navegador).
    public string? Html { get; init; }

    public static PrintOutcome Ok(string route) =>
        new() { Handled = true, Success = true, Route = route };

    public static PrintOutcome Fallback(string route, string html) =>
        new() { Handled = false, Success = true, Route = route, Html = html };

    public static PrintOutcome Fail(string route, string errorMessage) =>
        new() { Handled = true, Success = false, Route = route, ErrorMessage = errorMessage };
}
