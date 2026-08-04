namespace LoadManager.Models;

/// <summary>
/// Configuracion de la ruta de impresion del ticket.
/// Mode "Windows" usa el dialogo del sistema (flujo actual);
/// Mode "BluetoothCom" imprime ESC/POS directo por puerto serie (solo Windows).
/// </summary>
public sealed class PrinterOptions
{
    public const string ModeWindows = "Windows";
    public const string ModeBluetoothCom = "BluetoothCom";

    public string Mode { get; set; } = ModeWindows;

    // Puerto serie Bluetooth (ej. "COM4") cuando Mode = BluetoothCom (Windows).
    public string ComPort { get; set; } = string.Empty;

    // Direccion MAC y nombre del dispositivo vinculado cuando Mode = BluetoothCom (Android).
    public string BluetoothAddress { get; set; } = string.Empty;

    public string BluetoothName { get; set; } = string.Empty;

    public int BaudRate { get; set; } = 115200;

    public int DataBits { get; set; } = 8;

    // Nombres de System.IO.Ports.StopBits / Parity; se parsean con Enum.TryParse.
    public string StopBits { get; set; } = "One";

    public string Parity { get; set; } = "None";

    // Ancho del ticket en caracteres: 32 = papel de 58 mm; 42/48 = 80 mm.
    public int PaperColumns { get; set; } = 32;

    public bool IsBluetoothMode =>
        string.Equals(Mode, ModeBluetoothCom, StringComparison.OrdinalIgnoreCase);
}

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

/// <summary>
/// Datos necesarios para imprimir un ticket por cualquier ruta (dialogo o ESC/POS).
/// </summary>
public sealed class ReceiptPrintJob
{
    public required DispatchHistoryRecord Record { get; init; }

    // Numero de ticket (conteo de impresiones); 0 = no se pudo registrar.
    public int TicketNumber { get; init; }

    public required string JobName { get; init; }
}

/// <summary>
/// Destino de impresion Bluetooth seleccionable en Configuracion.
/// En Windows Id y DisplayName son el puerto COM (ej. "COM4"); en Android
/// Id es la direccion MAC del dispositivo vinculado y DisplayName su nombre.
/// </summary>
public sealed class BluetoothPrinterEndpoint
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }
}
