# LoadManager (Frontend MAUI)

App **.NET MAUI 8 + Blazor Hybrid** para tablets Android y Windows. Es la terminal que el
operador de la gasolinera usa para autorizar despachos, ver historial y diagnosticar
dispensarios. Habla **TCP/IP** directo con la consola dispensaria (hardware) y **HTTP REST**
con [`LoadManagerApi`](../LoadManagerApi/README.md). Ver el panorama completo en el
[CLAUDE.md raíz](../CLAUDE.md).

## Estructura

| Carpeta | Contenido |
| --- | --- |
| `Components/Pages/` | Páginas Razor (ver tabla abajo) |
| `Components/Layout/` | `MainLayout.razor`, `NavMenu.razor` |
| `Services/` | Lógica de negocio, todos singletons registrados en `MauiProgram.cs` |
| `Helpers/` | Parsers de tramas, cifrado, utilidades |
| `Models/` | DTOs y `ViewModels/` |
| `wwwroot/` | `app.css`, `index.html`, imágenes, Bootstrap |
| `Resources/`, `Platforms/` | Boilerplate estándar de MAUI (íconos, splash, bootstrap por plataforma) |

### Páginas principales

| Página | Archivo | Función |
| --- | --- | --- |
| Despacho | `Home.razor` | Flujo completo de autorización y surtido (el archivo más grande del repo, ~2700 líneas — contiene toda la máquina de estados del despacho) |
| Configuración | `Configuracion.razor` | Settings TCP/IP, API, límites, tiempos |
| Historial | `Historial.razor` | Consulta de despachos pasados con filtros |
| Estado Dispensario | `EstadoDispensario.razor` | Diagnóstico y estados en tiempo real |
| Tester | `Tester.razor` | Envío de tramas TCP crudas para debug |

### Servicios

| Servicio | Responsabilidad |
| --- | --- |
| `GasStationService` | Gateway único: TCP/IP con consolas dispensarias + llamadas REST a la API |
| `AppSettingsService` | Carga/guarda config cifrada en `appsettings.json` |
| `ConsoleLogService` | Logs locales por día/nivel |
| `ReceiptPrinterService` | Router de impresión: decide la ruta según `PrinterOptions` (ver sección Impresión) |
| `WindowsPrinterService` | Ticket por diálogo del sistema (Android: `PrintManager` nativo; Windows: `window.print()`) |
| `BluetoothEscPosPrinterService` | Ticket ESC/POS por Bluetooth (Windows: puerto COM/SPP; Android: socket al dispositivo vinculado) |
| `ConnectionValidationService` | Valida estado TCP y API antes de despachar |
| `ActiveDispatchTracker` | Control de despachos activos en pantalla |
| `DevModeService` | Expone los flags `Bypass*` de Modo DEV (ver abajo) |

### Helpers

| Helper | Responsabilidad |
| --- | --- |
| `DispenserFrameHelper` | Parser de tramas TCP del dispensario |
| `ConsoleFrameHelper` | Parser de tramas de consola |
| `AppSettingsCryptoHelper` | Cifrado/descifrado AES-256 de config |
| `TicketHtmlBuilder` | Generación de HTML del ticket de despacho |
| `TicketEscPosBuilder` | El mismo ticket pero en bytes ESC/POS para térmicas (texto + corte, a N columnas) |
| `EscPosDocumentBuilder` | Builder fluido de comandos ESC/POS: texto, alineación, negrita, QR, CODE128, corte |
| `FuelProductHelper` | Utilidades de productos de combustible |

### Modelos

`Models/` (y su subcarpeta `ViewModels/`) define los DTOs del cliente — por ejemplo
`FuelAuthorizationRequest`, `AuthorizationRegistrationResult`, `DispenserBusyException`. **Están
duplicados 1:1 con `LoadManagerApi/Models/`** — no hay proyecto compartido entre ambos. Cambiar un
contrato implica editar las dos copias en sync.

## Configuración del cliente (`appsettings.json`)

Todos los valores están cifrados con AES-256 (prefijo `enc:v1:`, ver `AppSettingsCryptoHelper`).
**No editar los valores a mano**: se gestionan desde la página **Configuración** de la propia app.

```json
{
    "GasStationConsole": {
        "IpAddress": "enc:v1:...",
        "Port": "enc:v1:...",
        "ConnectionTimeoutMilliseconds": "enc:v1:...",
        "ReadTimeoutMilliseconds": "enc:v1:...",
        "Commands": { "getVersion": "...", "dispenserDetail": "...", ... }
    },
    "AppConfiguration": {
        "Tpv": "enc:v1:...",
        "LimiteImporte": "enc:v1:...",
        "LimiteLitros": "enc:v1:...",
        "AuthorizationCountdownSeconds": "enc:v1:...",
        "AuthorizationPollingMilliseconds": "enc:v1:...",
        "FuelingPollingMilliseconds": "enc:v1:...",
        "DispenserCount": "enc:v1:...",
        "HistorialTopRecords": "enc:v1:..."
    },
    "Api": {
        "BaseUrl": "enc:v1:...",
        "RequestTimeoutSeconds": "enc:v1:..."
    },
    "Printer": {
        "Mode": "enc:v1:...",
        "ComPort": "enc:v1:...",
        "BluetoothAddress": "enc:v1:...",
        "BluetoothName": "enc:v1:...",
        "BaudRate": "enc:v1:...",
        "PaperColumns": "enc:v1:..."
    }
}
```

La sección `Printer` es opcional: si falta, aplican los defaults del POCO `PrinterOptions`
(`Mode=Windows`, `BaudRate=115200`, 8/N/1, `PaperColumns=32`).

## Impresión de tickets

Dos rutas, seleccionables en **Configuración → Impresora** (persistido cifrado en `Printer`):

| Modo | Windows | Android |
| --- | --- | --- |
| `Windows` (default) | `window.print()` → diálogo del sistema, el usuario elige cualquier impresora instalada (HP, PDF, etc.) | `PrintManager` nativo (diálogo de impresión de Android) |
| `BluetoothCom` | `SerialPort` sobre el COM **saliente** del emparejamiento Bluetooth (115200/8/N/1) | `BluetoothSocket` SPP directo al dispositivo vinculado (por MAC) |

Detalles de la ruta térmica (`BluetoothEscPosPrinterService`):

- **Enumeración automática**: en Windows solo lista COMs Bluetooth (intersección de
  `SerialPort.GetPortNames()` con el registro `BTHENUM`); en Android lista los dispositivos
  vinculados por nombre (`BondedDevices`). Botones "Actualizar" y "Probar conexión" en la UI.
- **Agnóstico a la marca**: no hay whitelist ni matching por nombre/modelo — cualquier impresora
  térmica que se empareje por Bluetooth SPP y entienda ESC/POS estándar funciona (p. ej. las
  genéricas chinas tipo **Speed**, Xprinter, Goojprt, etc.), sin cambios de código.
- El ticket ESC/POS (`TicketEscPosBuilder`) replica el contenido de `TicketHtmlBuilder`:
  solo texto + corte de papel; el builder también soporta QR y CODE128 (sin usar aún).
  Texto normalizado a ASCII para no depender del codepage de la impresora.
- **Permisos Android**: `BLUETOOTH_CONNECT` se pide en runtime (Android 12+); declarado en
  `AndroidManifest.xml` junto con los legacy (`maxSdkVersion=30`).
- Errores traducidos a mensajes claros (puerto ocupado / apagada / fuera de alcance) que las
  páginas muestran en toast; la conexión siempre se libera al terminar.
- Al elegir el COM en Windows: usar el puerto **Saliente** del emparejamiento (ver pestaña
  "Puertos COM" en las opciones Bluetooth de Windows); el Entrante abre pero no imprime.

Las páginas imprimen vía `IReceiptPrinterService.PrintReceiptAsync(record, ticketNumber, jobName)`;
si el resultado trae `Handled=false` (modo Windows en desktop), hacen fallback a
`uaacPrintHtml` (JS `window.print()`). `System.IO.Ports` se referencia solo en el TFM de Windows.

## UI / Responsive

**A partir de ahora, todo elemento de UI nuevo (modales, overlays, paneles, etc.) tiene que
funcionar bien (resize) en cualquier tamaño de pantalla/ventana.**

- No usar `100vw` / `100vh` para estirar overlays de pantalla completa: en WebView2 (Windows) esas
  unidades pueden resolverse mal (se vio un caso real donde daban 1.25x más grande que
  `window.innerWidth/innerHeight`, descentrando todos los modales). Usar `position: fixed; inset: 0;`
  en su lugar, que no depende de `vw`/`vh`.
- Evitar tamaños/posiciones fijos en px cuando el contenido deba adaptarse a distintas
  resoluciones (tablet Windows vs Android, distintos DPI); preferir `%`, `min()`/`max()`/`clamp()`,
  flex/grid.
- Antes de dar por buena una pantalla o popup nuevo, probarlo compilado (no solo a simple vista en
  el editor) y verificar que se vea centrado/completo en la resolución real del dispositivo.

## Convenciones de UI (NavMenu, botones, texto)

Reglas ya aplicadas en todo el proyecto — seguirlas al agregar pantallas o
componentes nuevos para no romper la consistencia visual:

- **Tipografía**: OpenSans (`ConfigureFonts` en `MauiProgram.cs`) en toda la app, sin excepciones.
- **Color de acento**: rojo institucional `#e30613` (folios, selección activa, marca). No usar otros
  rojos/azules para lo mismo.
- **Botones de accion en un header de seccion** (Historial, Tester, Configuración → Impresora):
  solo ícono (sin texto), clase `btn btn-outline-secondary icon-only-button` + SVG stroke-based
  (`viewBox="0 0 24 24"`, `fill:none; stroke:currentColor`) + `title`/`aria-label` con el texto real
  (accesibilidad/tooltip, no se muestra en pantalla). El título de la sección va a la izquierda, el
  o los íconos a la derecha, **a la altura del título** — no centrados contra bloques de dos líneas
  (título + subtítulo). Patrón robusto: contenedor con `position: relative`, botón con
  `position: absolute; top: 0; right: 0;` (ver `.response-clear-button`/`.tester-back-button` en
  `app.css`) — no depender solo de `align-items` en flex cuando el bloque vecino tiene más de una
  línea de texto.
- **Acciones destructivas** (desactivar, eliminar, cancelar): `btn btn-outline-danger` (rojo). El
  resto de acciones: `btn btn-outline-secondary` (gris).
- **Un control que minimiza/expande contenido nunca debe ocultarse a sí mismo ni a botones de acción
  vecinos** (ej. Modo DEV: el chevron que colapsa la lista de toggles vive fuera del bloque que
  colapsa, junto al botón "Desactivar", para que ambos sigan visibles aunque la lista esté minimizada).
- **NavMenu**: no reservar espacio (`padding`) para un botón condicional que casi nunca se muestra
  (ej. el toggle de tema, solo visible con Modo DEV + flag `ShowThemeButton`). Agregar una clase
  modificadora en el contenedor padre (`has-theme-toggle` en `.sidebar`) y que el CSS del padding
  dependa de esa clase, no aplicarlo siempre "por si acaso".
- **Modales de estado bloqueante** (loading, confirmaciones): todos comparten la clase `.app-modal`;
  los overrides de tema deben apuntar a esa clase genérica, no a clases puntuales por pantalla, para
  que un tema nuevo cubra todos los modales automáticamente.

## Modo DEV (Flags de Testing)

Permite probar la UI sin hardware real ni base de datos:

| Flag | Efecto |
| --- | --- |
| `BypassDatabase` | Salta validación de BD |
| `BypassTcpConnection` | Salta conexión TCP |
| `BypassDeviceAuthorization` | Salta whitelist de dispositivos |
| `BypassDispenserCommunication` | Usa datos mock en lugar de TCP |
| `BypassSendAuthorization` | Simula respuesta de autorización |
| `BypassFueling` | Simula despacho completo |
| `BypassAmountLimits` | No valida límites de importe/litros |
| `ShouldShowCountdown` | Muestra/oculta contador regresivo |

## Build / Run

El SDK está fijado por `global.json` (8.0.421, `rollForward latestFeature`). Este proyecto
necesita el workload `maui`:

```bash
dotnet workload restore

# Windows
dotnet build LoadManager/LoadManager.csproj -f net8.0-windows10.0.19041.0
dotnet run --project LoadManager -f net8.0-windows10.0.19041.0

# Android (produce APK)
dotnet build LoadManager/LoadManager.csproj -f net8.0-android
```

> El TFM completo (`net8.0-windows10.0.19041.0`) es obligatorio — un `-f net8.0-windows` corto no
> coincide con ningún TFM del proyecto y falla.

No hay proyectos de test. Verificación = compilar ambos TFMs + correr manualmente. Para builds de
verificación desechables, usar `artifacts/<slug>/` o `build-check-*/` (ambos gitignored), p. ej.
`dotnet build ... -o artifacts/mi-cambio`.
