# LoadManager

Sistema integral de gestión de despacho de combustible en gasolineras. Compuesto por una aplicación **.NET MAUI** (terminal multiplataforma) y una **API REST en ASP.NET Core** que intermedia entre la terminal, la base de datos SQL Server y las consolas dispensarias de combustible.

---

## Estructura del Repositorio

```
LoadManager/
├── LoadManager/          # App MAUI (frontend Android/Windows)
├── LoadManagerApi/       # API REST (ASP.NET Core 8)
├── Script/               # Scripts SQL centralizados (referencia y respaldo)
│   ├── sp_folio_app.sql
│   └── sp_bitacora_app.sql
├── publish_api/          # Binarios publicados listos para copiar al servidor IIS
├── assets/               # Recursos para presentaciones (logos, imágenes)
│   ├── UA4.png           # Ícono rojo "iA" de Ultra Access
│   ├── UA3.png           # Logotipo "Ultra Access" (texto negro)
│   └── UA5.png           # Logo ORSAN (empresa cliente)
├── LoadManager.slnx      # Solución Visual Studio
├── global.json           # Versión SDK .NET 8
├── DEPLOY-IIS.md         # Guía de despliegue en IIS
└── presentacion.html     # Presentación del proyecto (10 slides HTML)
```

---

## Tecnologías

| Capa | Tecnología |
| --- | --- |
| Frontend | .NET MAUI 8 + Blazor WebView (Razor Components) |
| Backend | ASP.NET Core 8 REST API |
| Base de datos | Microsoft SQL Server (dbctg) |
| Acceso a datos | ADO.NET directo (SqlCommand, SqlDataReader) |
| Protocolo consola | TCP/IP socket (TcpClient) |
| Documentación API | Swagger / OpenAPI |
| Despliegue | IIS (Windows Server) |
| Cifrado config | AES-256 (prefijo `enc:v1:`) |

---

## Arquitectura General

```
┌─────────────────────────────────────────────────────────────────┐
│                  MAUI Frontend (Tablets/Android)                 │
│   Home.razor | Configuracion | Historial | Estado               │
│                                                                  │
│   GasStationService (TCP)     │    HttpClient (REST)            │
└───────────────┬───────────────┴──────────────┬──────────────────┘
                │  TCP/IP                       │  HTTP REST
                │  :8005                        │  :8083 / :8084
                │                              │
   ┌────────────▼────────────┐    ┌─────────────▼────────────────────────┐
   │  Consola/Dispens.       │    │  LoadManagerApi (IIS)                │
   │  (Hardware POS)         │    │  Controllers + Services + SPs        │
   └─────────────────────────┘    └──────────────────┬───────────────────┘
                                                     │  T-SQL :1433
                                        ┌────────────▼──────────────┐
                                        │  SQL Server (dbctg)       │
                                        │  tblBitacora              │
                                        │  tblParametros            │
                                        │  tblDispensarios          │
                                        │  tblMangueras             │
                                        │  tblProductos             │
                                        └───────────────────────────┘
```

---

## Módulo: LoadManager (Frontend MAUI)

### Páginas principales

| Página | Archivo | Función |
| --- | --- | --- |
| Despacho | `Home.razor` | Flujo completo de autorización y surtido |
| Configuración | `Configuracion.razor` | Settings TCP/IP, API, límites, tiempos |
| Historial | `Historial.razor` | Consulta de despachos pasados con filtros |
| Estado Dispensario | `EstadoDispensario.razor` | Diagnóstico y estados en tiempo real |
| Tester | `Tester.razor` | Envío de tramas TCP crudas para debug |

### Servicios

| Servicio | Responsabilidad |
| --- | --- |
| `GasStationService` | Comunicación TCP/IP con consolas dispensarias + llamadas REST a la API |
| `AppSettingsService` | Carga/guarda config cifrada en `appsettings.json` |
| `ConsoleLogService` | Logs locales por día/nivel |
| `ReceiptPrinterService` | Impresión de tickets vía HTML |
| `ConnectionValidationService` | Valida estado TCP y API antes de despachar |
| `ActiveDispatchTracker` | Control de despachos activos en pantalla |

### Helpers

| Helper | Responsabilidad |
| --- | --- |
| `DispenserFrameHelper` | Parser de tramas TCP del dispensario |
| `ConsoleFrameHelper` | Parser de tramas de consola |
| `AppSettingsCryptoHelper` | Cifrado/descifrado AES-256 de config |
| `TicketHtmlBuilder` | Generación de HTML del ticket de despacho |
| `FuelProductHelper` | Utilidades de productos de combustible |

### Configuración del cliente (`appsettings.json`)

Todos los valores están cifrados con AES-256 (prefijo `enc:v1:`):

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
    }
}
```

---

## Módulo: LoadManagerApi (Backend REST)

### Controllers y Endpoints

#### `POST /api/authorizations`
Registra una autorización de despacho. Genera folio, valida doble-tap, bloquea dispensario y registra en `tblBitacora`.

```json
// Request
{
    "tpv": 1,
    "tipoVenta": 1,
    "dispensario": 1,
    "manguera": 1,
    "producto": 1,
    "usuario": 1,
    "tipoProgramado": 1,
    "programado": 100.50,
    "tarjeta": "",
    "cliente": "",
    "vehiculo": "",
    "odometro": ""
}
// Response: { "folio": 12345 }
// 409 Conflict si dispensario ocupado
```

#### `GET /api/dispensers/{dispenser}/products`
Retorna las mangueras y productos disponibles para un dispensario.

#### `PUT /api/dispensers/{dispenser}/status`
Actualiza el estado de un dispensario en `tblDispensarios`.

#### `GET /api/products`
Catálogo de productos de combustible.

#### `GET /api/dispatch-types`
Tipos de despacho: Importe / Litros / Lleno.

#### `GET /api/history?folio={folio}`
Consulta el registro completo de una transacción en `tblBitacora`.

#### `GET /api/history/current?dispenser=&product=&top=3`
Últimos N despachos para un dispensario y producto.

#### `POST /api/impressions/{transaccion}`
Registra una impresión de ticket en `tblImpresiones`. Retorna número de impresión (1ª, 2ª, etc.).

#### `GET /api/devices/authorization?ip=&mac=&device=`
Valida si el dispositivo está en `tblConexionesAutorizadas` con `bitAutorizada=1`.

#### `GET /api/database/health`
Verifica conexión a SQL Server y existencia de objetos requeridos.

#### `POST /api/logs`
Recibe entradas de log desde el cliente MAUI y las persiste en archivo.

### Servicios

| Servicio | Responsabilidad |
| --- | --- |
| `GasStationService` | Lógica de negocio principal: BD, autorizaciones, historial |
| `AppLogService` | Logging estructurado a archivo por fecha/nivel |
| `DatabaseScriptService` | Deploy automático de Stored Procedures al iniciar la API |
| `GasStationServiceLoggingDecorator` | Wrapper de logging sobre `GasStationService` |

### Mecanismos de concurrencia

Para evitar doble autorización en el mismo dispensario:

1. `SemaphoreSlim` en memoria por dispensario (evita race conditions locales)
2. `ConcurrentDictionary<int, DateTime>` — bloquea si se autorizó hace menos de 10 segundos
3. `sp_getapplock` (Exclusive, Session) — lock distribuido en SQL Server
4. Validación en `tblBitacora` con `READ COMMITTED LOCK` — doble check en BD

### Configuración del servidor (`appsettings.json`)

```json
{
    "ConnectionStrings": {
        "GasStationDatabase": "Server=172.20.11.40;Database=dbctg;User Id=sa;Password=***;Encrypt=True;TrustServerCertificate=True;"
    },
    "FileLogs": {
        "BasePath": "Logs"
    }
}
```

### Middlewares

- `ApiExceptionHandler` — manejo centralizado de errores, retorna respuesta estructurada
- `HttpTransactionLoggingMiddleware` — logging HTTP completo (método, URL, body, duración, IP)

---

## Base de Datos (SQL Server — `dbctg`)

### Tablas principales

| Tabla | Contenido |
| --- | --- |
| `tblBitacora` | Registro de todas las transacciones de despacho |
| `tblParametros` | Config de gasolinera: secuencia folio, marca, RFC, empresa |
| `tblDispensarios` | Estado actual de cada dispensario (`intEstatus`) |
| `tblMangueras` | Mangueras por dispensario, con producto y límites |
| `tblProductos` | Catálogo de combustibles y precios |
| `tblTipoDespacho` | Tipos: Importe / Litros / Lleno |
| `tblImpresiones` | Registro de cada impresión de ticket |
| `tblTransacciones` | Transacciones cerradas (confirma si un despacho se completó) |
| `tblConexionesAutorizadas` | Whitelist de dispositivos por IP/MAC/nombre |
| `tblConsultasUG` | Consultas de crédito UG (para ventas tipo 3/33) |
| `tblTiposVenta` | Catálogo de tipos de venta |
| `tblUsuarioIsla` | Usuarios asignados por isla |

### Stored Procedures

| SP | Script | Función |
| --- | --- | --- |
| `dbo.sp_folio_app` | `sp_folio_app.sql` | Genera secuencia atómica de folio con `UPDLOCK + HOLDLOCK`. Abre su propia transacción si no hay una activa. |
| `dbo.sp_bitacora_app` | `sp_bitacora_app.sql` | Valida límites, inserta en `tblBitacora`, maneja lógica UG/GoBenefits. Llama a `sp_folio_app` internamente. |

Los SPs se despliegan automáticamente via `CREATE OR ALTER` desde `LoadManagerApi/Script/*.sql` al arrancar la API (una vez por proceso). Si un script falla, se loguea el error pero la API sigue iniciando.

### Scripts SQL

Los scripts fuente se mantienen en dos ubicaciones:

- `Script/` — carpeta raíz del repositorio (referencia y respaldo)
- `LoadManagerApi/Script/` — carpeta que usa la API para el auto-deploy al arrancar

Ambas carpetas deben mantenerse sincronizadas. Al modificar un SP, actualizar ambas.

---

## Flujo de una Transacción Completa

```
1.  Usuario selecciona Dispensario → Producto → Tipo → Cantidad
2.  MAUI valida conexión TCP y API
3.  TCP TDE|{dispensario} → consola → respuesta con estado y folio actual
4.  HTTP POST /api/authorizations
        ├── Semáforo en memoria por dispensario
        ├── sp_getapplock (lock distribuido SQL Server)
        ├── Check anti double-tap (10 seg) en tblBitacora
        ├── sp_bitacora_app (sin transacción exterior en C#):
        │       ├── sp_folio_app → genera e incrementa folio (transacción propia, committed)
        │       ├── Valida límites de importe/litros
        │       └── INSERT tblBitacora → retorna folio
        └── retorna folio al cliente
5.  MAUI recibe folio → envía trama AUTH|... por TCP al dispensario
6.  Consola autoriza → usuario descuelga manguera
7.  MAUI hace polling TCP (cada 500ms) → actualiza litros en pantalla
8.  Dispensario finaliza (estado = 2)
9.  MAUI consulta GET /api/history?folio=X → obtiene datos completos
10. TicketHtmlBuilder genera ticket HTML con datos de tblBitacora + tblParametros
11. ReceiptPrinterService imprime
12. POST /api/impressions/{folio} → registra impresión en tblImpresiones
```

---

## Conexiones y Puertos

| Componente | Host | Puerto | Protocolo |
| --- | --- | --- | --- |
| Consola dispensarios | 172.20.11.40 | 8005 | TCP/IP socket |
| API Backend (Producción) | 172.20.11.40 | 8083 | HTTP REST |
| API Backend (Desarrollo) | 172.20.11.40 | 8084 | HTTP REST |
| SQL Server | 172.20.11.40 | 1433 | T-SQL |

---

## Tiempos y Límites (valores típicos)

| Parámetro | Valor | Descripción |
| --- | --- | --- |
| `ConnectionTimeoutMilliseconds` | 5000 | Timeout conexión TCP |
| `ReadTimeoutMilliseconds` | 10000 | Timeout lectura socket |
| `Api.RequestTimeoutSeconds` | 30 | Timeout HTTP a la API |
| `AuthorizationCountdownSeconds` | 60 | Tiempo para que usuario descuelgue manguera |
| `AuthorizationWarningSeconds` | 10 | Advertencia antes de timeout |
| `AuthorizationPollingMilliseconds` | 500 | Frecuencia de polling de estado |
| `FuelingPollingMilliseconds` | 500 | Frecuencia de polling de litros |
| `ToastDurationMilliseconds` | 3000 | Duración de mensajes de notificación |

---

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

---

## Despliegue en IIS

Ver `DEPLOY-IIS.md` para instrucciones detalladas. Resumen:

```bash
# 1. Publicar desde Visual Studio → Build → Publish LoadManagerApi → Output: publish_api/
#    O desde consola:
dotnet publish LoadManagerApi/LoadManagerApi.csproj -c Release -o publish_api

# 2. Copiar publish_api/ al servidor 172.20.11.40 (reemplaza los archivos del sitio)

# 3. En IIS Manager:
#    - Reciclar el Application Pool para que DatabaseScriptService aplique los SPs actualizados
#    - Application Pool: "LoadManagerApi", No Managed Code
#    - Sitio prod: binding http://172.20.11.40:8083
#    - Sitio dev:  binding http://172.20.11.40:8084, variable ASPNETCORE_ENVIRONMENT=Development

# 4. Verificar
#    http://172.20.11.40:8083/api/database/health  → {"isSuccess":true}
```

Los logs se guardan en `Logs/YYYY/MM/DD/` con nivel (Info, Success, Error).

---

## Ejecutar Localmente

```bash
# Backend API
cd LoadManagerApi
dotnet run
# Swagger en http://localhost:5000

# Frontend MAUI (Windows)
cd LoadManager
dotnet run -f net8.0-windows

# Frontend MAUI (Android, requiere Android SDK)
cd LoadManager
dotnet run -f net8.0-android
```

---

## Seguridad

- **Autenticación de dispositivos**: IP/MAC en `tblConexionesAutorizadas` (sin JWT)
- **Config cifrada**: AES-256 en `appsettings.json` del cliente
- **SQL injection**: Queries 100% parametrizados con `SqlCommand`
- **Concurrencia**: Semáforos + locks distribuidos (`sp_getapplock`)
- **Auditoría**: Logging completo de cada transacción con IP, MAC, device, duración

---

## Presentación (`presentacion.html`)

### Imágenes y logos

Todos los recursos visuales de la presentación se guardan en `assets/` (raíz del repositorio):

| Archivo | Contenido | Uso |
| --- | --- | --- |
| `assets/UA4.png` | Ícono rojo "iA" de Ultra Access | Logo principal en portada y slides interiores |
| `assets/UA3.png` | Logotipo "Ultra Access" texto negro | Barra de logos en portada y cierre |
| `assets/UA5.png` | Logo ORSAN | Logo del cliente en portada y cierre |

Al añadir imágenes futuras: copiarlas a `assets/` y referenciarlas como `src="assets/NombreArchivo.png"`.

### Estructura de slides

El archivo es un HTML standalone sin dependencias externas (funciona al abrir directo en el navegador). Tiene **10 slides**:

| # | Slide | Clase CSS |
| --- | --- | --- |
| 1 | Portada con logos y nombre app | `slide-cover` |
| 2 | ¿Qué es? — descripción general | — |
| 3 | Módulo 1: Despacho (flujo 3 pasos) | — |
| 4 | Surtido en tiempo real (tanque animado) | — |
| 5 | Ticket de despacho | — |
| 6 | Módulo 2: Historial | — |
| 7 | Módulo 3: Configuración | — |
| 8 | Arquitectura del sistema | — |
| 9 | Multiplataforma Windows / Android | — |
| 10 | Cierre con logos y pills de tecnología | `slide-cover` |

### Cómo añadir un slide nuevo

```html
<!-- dentro de <div class="deck" id="deck"> -->
<div class="slide" data-index="N">   <!-- N = índice 0-based -->
  <div class="card">
    <div class="section-badge">...</div>
    <div class="slide-title">Título del slide</div>
    <div class="slide-lead">Descripción corta.</div>
    <!-- contenido: .grid-2/.grid-3, .steps, .split, .feat, etc. -->
  </div>
</div>
```

El JS detecta automáticamente todos los `.slide` y genera los puntos de navegación.

### Nombre de la app en la presentación

El nombre usado en `presentacion.html` es **UAFLOW / UAHUB** (nombre comercial del producto). El nombre interno del repositorio y el código sigue siendo `LoadManager`. **No cambiar** el nombre en el código fuente.

### Navegación de la presentación

- **Teclado**: `←` / `→` o `↑` / `↓`
- **Mouse**: botones de la barra inferior
- **Touch**: swipe izquierda/derecha
