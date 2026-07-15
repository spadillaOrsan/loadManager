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
