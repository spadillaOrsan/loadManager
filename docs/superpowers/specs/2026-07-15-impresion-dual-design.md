# Impresión dual: diálogo de Windows + térmica Bluetooth ESC/POS (COM)

**Fecha:** 2026-07-15 · **Estado:** Aprobado por el usuario

## Objetivo

Al imprimir un ticket (tarjeta del Historial o ticket de despacho en Home), la app debe poder
usar dos rutas de impresión en Windows:

1. **Windows (actual):** diálogo de impresión del sistema (`window.print()` en el WebView), donde
   el usuario elige cualquier impresora instalada (HP, PDF, etc.). Android conserva su flujo
   nativo con `PrintManager`.
2. **Bluetooth térmica (nueva, solo Windows):** impresión directa ESC/POS por puerto serie COM
   (perfil SPP de Bluetooth) usando `System.IO.Ports.SerialPort`.

La selección de modo se hace **una vez en Configuración** y queda persistida (cifrada) — al
imprimir no se pregunta nada.

## Decisiones tomadas (con el usuario)

- Selector de impresora: sección nueva en la página **Configuración**, persistida en el
  appsettings cifrado. Sin modal al imprimir.
- Ticket ESC/POS: **solo texto + corte de papel**. El builder ESC/POS sí expone QR (GS ( k) y
  código de barras CODE128 (GS k) para uso futuro, pero el ticket no los usa aún.
- Bluetooth/COM aplica **solo a Windows** (`System.IO.Ports` no existe en Android). Android
  queda exactamente como está.

## Configuración

Nueva clase `Models/PrinterOptions.cs`, agregada a `AppSettings` como sección `Printer`:

| Propiedad | Default | Notas |
| --- | --- | --- |
| `Mode` | `"Windows"` | `"Windows"` o `"BluetoothCom"` |
| `ComPort` | `""` | Ej. `COM4` |
| `BaudRate` | `115200` | Editable en UI |
| `DataBits` | `8` | Fijo en UI (solo config file) |
| `StopBits` | `"One"` | Fijo en UI (solo config file) |
| `Parity` | `"None"` | Fijo en UI (solo config file) |
| `PaperColumns` | `32` | 32 = papel 58 mm; 42/48 = 80 mm |

`AppSettingsService`: se agregan las entradas de la sección `Printer` a
`EncryptSettingsNode`/`DecryptSettingsNode` y defaults en `MergeMissingSettings`. El
`appsettings.json` empaquetado no necesita la sección (los defaults del POCO aplican).

## Servicios

### `IPrinterService` (nueva interfaz)

```csharp
public interface IPrinterService
{
    Task<PrintOutcome> PrintAsync(ReceiptPrintJob job, CancellationToken ct = default);
}
```

- `ReceiptPrintJob`: `DispatchHistoryRecord Record`, `int TicketNumber`, `string JobName`.
- `PrintOutcome`: `bool Handled` (false ⇒ el llamador usa fallback JS `uaacPrintHtml`),
  `bool Success`, `string? ErrorMessage` (mensaje claro para toast), `string Route`
  (descripción para logs: "Impresora nativa", "ESC/POS COM4", etc.).

### `WindowsPrinterService : IPrinterService`

Absorbe la lógica actual de `ReceiptPrinterService`: construye el HTML con
`TicketHtmlBuilder`; en Android imprime con `PrintManager` + WebView (Handled=true); en
Windows devuelve Handled=false para que la página invoque `window.print()` (diálogo del
sistema). El nombre es el que pidió el usuario aunque también contenga la rama Android.

### `BluetoothEscPosPrinterService : IPrinterService`

Solo funcional en Windows (`#if WINDOWS`); en Android devuelve error claro (no alcanzable en la
práctica porque la UI de selección solo existe efectiva en Windows).

- `Task<IReadOnlyList<string>> GetBluetoothPortsAsync()` — **solo puertos Bluetooth**:
  intersección de `SerialPort.GetPortNames()` con los `PortName` registrados bajo
  `HKLM\SYSTEM\CurrentControlSet\Enum\BTHENUM` (registro estándar de Bluetooth SPP en
  Windows). Sin librerías externas.
- `Task<PrintOutcome> TestPortAsync(string port, PrinterOptions opts)` — abre y cierra el
  puerto; traduce excepciones a mensajes claros:
  - `UnauthorizedAccessException` → "El puerto X está ocupado por otra aplicación."
  - `FileNotFoundException`/`IOException` → "El puerto X no existe o el dispositivo no responde."
  - `TimeoutException` → "El dispositivo en X no responde."
- `PrintAsync` — abre `SerialPort` con la config de `PrinterOptions`
  (115200/8/N/1 por defecto, `WriteTimeout` 5 s), escribe los bytes ESC/POS generados por
  `TicketEscPosBuilder`, avanza papel y corta; **libera el puerto en `finally`** (`using`).

### Builders (Helpers, sin dependencias)

- `EscPosDocumentBuilder`: builder fluido que acumula bytes — `Initialize` (ESC @), `Align`,
  `Bold`, `TextLine`, `TwoColumns(izq, der)` a N columnas, `Feed`, `QrCode` (GS ( k),
  `Barcode` CODE128 (GS k), `Cut` (GS V 66). Texto normalizado a ASCII (se eliminan
  diacríticos) para no depender del codepage de cada impresora.
- `TicketEscPosBuilder`: replica el contenido de `TicketHtmlBuilder` (marca, ORIGINAL,
  ticket #, RFC, empresa, folio, filas EESS/Fecha/Bomba/…, DESPACHO, producto, importes,
  fecha de impresión, FIN DE TICKET) en texto a `PaperColumns` columnas + corte.

### Router: `ReceiptPrinterService : IReceiptPrinterService`

`IReceiptPrinterService` gana:

```csharp
Task<PrintOutcome> PrintReceiptAsync(DispatchHistoryRecord record, int ticketNumber,
    string jobName, CancellationToken ct = default);
```

Lee `PrinterOptions` de `IAppSettingsService` y enruta: `BluetoothCom` →
`BluetoothEscPosPrinterService`; si no → `WindowsPrinterService`. `PrintHtmlAsync` se conserva
(compatibilidad). `Historial.razor` y `Home.razor` pasan a llamar `PrintReceiptAsync`; si
`Handled == false` hacen el fallback JS actual; si `Success == false` muestran el
`ErrorMessage` en toast.

## DI (`MauiProgram`)

```csharp
builder.Services.AddKeyedSingleton<IPrinterService, WindowsPrinterService>(PrinterServiceKeys.Windows);
builder.Services.AddKeyedSingleton<IPrinterService, BluetoothEscPosPrinterService>(PrinterServiceKeys.BluetoothCom);
builder.Services.AddSingleton<IReceiptPrinterService, ReceiptPrinterService>(); // router
```

## UI — Configuración

Nueva sección de acordeón **"Impresora"** (índice 4, mismo patrón que las existentes):

- Selector de modo: dos botones tipo radio — "Impresora de Windows" / "Bluetooth (COM)".
- Si Bluetooth: dropdown con los puertos Bluetooth detectados, botón "Actualizar" (re-enumera)
  y botón "Probar conexión" (toast con el resultado), campo numérico BaudRate.
- Indicador del header de la sección: "Windows" o "Bluetooth · COM4".
- Se guarda con el botón "Guardar y conectar" existente; validación: si modo Bluetooth,
  `ComPort` no puede quedar vacío.

En Android la sección muestra el modo fijo "Diálogo del sistema (Android)" sin opciones COM
(`OperatingSystem.IsWindows()` decide).

## Manejo de errores

- Probar conexión y fallos de impresión → toast con el mensaje claro del `PrintOutcome` +
  registro en `IConsoleLogService` (patrón existente de `AppLogEntry`).
- El puerto siempre se cierra en `finally`, incluso ante excepción de escritura.

## Paquetes

- `System.IO.Ports` (oficial de Microsoft) referenciado **solo** para el TFM de Windows.

## Verificación

No hay proyectos de test en el repo (regla del proyecto): compilar ambos TFMs
(`net8.0-windows10.0.19041.0` y `net8.0-android`) y prueba manual con impresora térmica.
