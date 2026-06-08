# Base de Datos — Operaciones CRUD

## Conexión

| Parámetro | Valor |
|---|---|
| Servidor | `172.20.11.40` |
| Base de datos | `dbctg` |
| Usuario | `sa` |
| Puerto | Default (1433) |
| Encrypt | true |
| TrustServerCertificate | true |
| Timeout conexión | 10 segundos |

---

## Objetos Requeridos

La app valida al iniciar que existan los siguientes objetos:

| Objeto | Tipo |
|---|---|
| `dbo.tblMangueras` | Tabla |
| `dbo.tblProductos` | Tabla |
| `dbo.tblTipoDespacho` | Tabla |
| `dbo.tblDispensarios` | Tabla |
| `dbo.sp_UA_foliosecuencia_app` | Stored Procedure |
| `dbo.sp_UA_Bitacora_APP` | Stored Procedure |

---

## Operaciones

### 1. Verificar objetos requeridos
**Tipo:** READ — Metadata  
**Cuándo:** Al presionar "Comenzar" en el despacho

```sql
SELECT required.ObjectName
FROM (VALUES
    (@object0), (@object1), (@object2),
    (@object3), (@object4), (@object5)
) AS required(ObjectName)
WHERE OBJECT_ID(required.ObjectName) IS NOT NULL
```

---

### 2. Obtener siguiente folio
**Tipo:** EXEC SP  
**Cuándo:** Justo antes de enviar la autorización a la consola  
**Retorna:** `int` — número de folio de la siguiente operación

```sql
EXEC dbo.sp_UA_foliosecuencia_app
```

---

### 3. Registrar autorización de despacho
**Tipo:** EXEC SP (INSERT interno)  
**Cuándo:** Después de recibir respuesta "Autorizado" de la consola TCP  
**Tabla destino:** `tblBitacora`

```sql
EXEC dbo.sp_UA_Bitacora_APP
    @intTPV            = {Tpv},
    @intTipoVenta      = {TipoVenta},
    @intDispensario    = {Dispensario},
    @intManguera       = {Manguera},
    @intProducto       = {Producto},
    @intUsuario        = {Usuario},
    @strTarjeta        = {Tarjeta},
    @intTipoProgramado = {TipoProgramado},
    @dblProgramado     = {Programado},
    @intFolioSecuencia = {FolioSecuencia},
    @strCliente        = {Cliente},
    @strBandaMagnetica = {BandaMagnetica},
    @strVehiculo       = {Vehiculo},
    @strOdometro       = {Odometro},
    @strPie1           = '',
    @strPie2           = '',
    @strPie3           = '',
    @strPie4           = ''
```

| Parámetro | Tipo | Descripción |
|---|---|---|
| `@intTPV` | int | Terminal punto de venta |
| `@intTipoVenta` | int | Tipo de venta (default 1) |
| `@intDispensario` | int | Número de dispensario |
| `@intManguera` | int | Número de manguera |
| `@intProducto` | int | ID del producto/combustible |
| `@intUsuario` | int | ID del usuario operador |
| `@strTarjeta` | varchar | Número de tarjeta (vacío en flujo normal) |
| `@intTipoProgramado` | int | Tipo: importe / volumen / tanque lleno |
| `@dblProgramado` | decimal | Cantidad autorizada |
| `@intFolioSecuencia` | int | Folio generado por SP anterior |
| `@strCliente` | varchar | Nombre del cliente (vacío en flujo normal) |
| `@strBandaMagnetica` | varchar | Datos de banda (vacío en flujo normal) |
| `@strVehiculo` | varchar | Vehículo (vacío en flujo normal) |
| `@strOdometro` | varchar | Odómetro (vacío en flujo normal) |
| `@strPie1..4` | varchar | Pies de ticket (vacíos) |

---

### 4. Actualizar estado de dispensario
**Tipo:** UPDATE  
**Cuándo:** Cada vez que la consola responde con un nuevo estado del dispensario  
**Tabla:** `tblDispensarios`

```sql
UPDATE tblDispensarios
SET    intEstatus     = @status
WHERE  intDispensario = @dispenser
```

---

### 5. Consultar productos por dispensario
**Tipo:** READ  
**Cuándo:** Al seleccionar un dispensario en el paso 2  
**Tablas:** `tblMangueras` JOIN `tblProductos`

```sql
SELECT
    tm.intManguera,
    tm.intProducto,
    tp.strDescripcion,
    tp.dblPrecioU
FROM dbo.tblMangueras tm
INNER JOIN dbo.tblProductos tp
    ON tp.intProducto = tm.intProducto
WHERE tm.bitActivo   = 1
  AND tm.intDispensario = @dispenser
  AND tp.bitEstatus  = 1
ORDER BY tm.intManguera
```

---

### 6. Consultar tipos de despacho
**Tipo:** READ  
**Cuándo:** Al pasar al paso 2 (selección)  
**Tabla:** `tblTipoDespacho`

```sql
SELECT *
FROM tblTipoDespacho
WHERE bitEstatus = 1
```

> La app es flexible con los nombres de columna — busca `intTipoDespacho`, `intTipoProgramado`, `intTipoVenta`, `intTipoCarga`, `intID`, `Id` en ese orden.

---

### 7. Consultar historial de despachos
**Tipo:** READ  
**Cuándo:** Al abrir el módulo Historial  
**Tablas:** `tblBitacora` LEFT JOIN `tblProductos`  
**Límite:** TOP 150 registros

```sql
SELECT TOP 150
    b.*,
    tp.strDescripcion AS ProductoDescripcion,
    tp.dblPrecioU     AS ProductoPrecio
FROM dbo.tblBitacora b
LEFT JOIN dbo.tblProductos tp
    ON tp.intProducto = b.intProducto
WHERE @folio IS NULL
   OR CONVERT(varchar(50), b.intSecuencia)     LIKE @folioLike
   OR CONVERT(varchar(50), b.intFolioSecuencia) LIKE @folioLike
ORDER BY b.intSecuencia DESC
```

> `intFolioSecuencia` se incluye solo si la columna existe en `tblBitacora` (verificación dinámica).  
> Si no se pasa folio de búsqueda, trae los últimos 150 registros.

**Campos leídos de `tblBitacora` (flexible por nombre de columna):**

| Campo app | Columnas buscadas en BD |
|---|---|
| `Sequence` | `intFolioSecuencia`, `intSecuencia`, `Folio` |
| `Dispenser` | `intDispensario` |
| `Hose` | `intManguera` |
| `Product` | `intProducto` |
| `DispatchTypeId` | `intTipoProgramado`, `intTipoDespacho`, `intTipoCarga` |
| `ProgrammedAmount` | `dblProgramado`, `dblCantidadProgramada` |
| `Amount` | `dblImporte`, `dblMonto`, `dblVendido`, `dblVenta` |
| `Liters` | `dblLitros`, `dblVolumen`, `dblCantidad` |
| `Price` | `dblPrecioU`, `ProductoPrecio`, `dblPrecio` |
| `CreatedAt` | `dtmFecha`, `dtFecha`, `Fecha`, `fecFecha`, `dteFecha` |
| `IsCanceled` | `bitCancelada`, `bitCancelado`, `Cancelada`, `Cancelado` |
| `IsClosed` | `bitCerrada`, `bitCerrado`, `Cerrada`, `Cerrado` |

---

### 8. Verificar existencia de columna (interno)
**Tipo:** READ — Metadata  
**Cuándo:** Antes de ejecutar el historial, para saber si `intFolioSecuencia` existe

```sql
SELECT 1
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @schema
  AND TABLE_NAME   = @table
  AND COLUMN_NAME  = @column
```

---

## Resumen General

| # | Método | Operación | Objeto BD |
|---|---|---|---|
| 1 | `CheckConnectionAsync` | READ | `OBJECT_ID` metadata |
| 2 | `GetNextFolioAsync` | EXEC SP | `sp_UA_foliosecuencia_app` |
| 3 | `RegisterAuthorizationAsync` | EXEC SP | `sp_UA_Bitacora_APP` → `tblBitacora` |
| 4 | `UpdateDispenserStatusAsync` | UPDATE | `tblDispensarios` |
| 5 | `GetDispenserProductsAsync` | READ | `tblMangueras` + `tblProductos` |
| 6 | `GetDispatchTypesAsync` | READ | `tblTipoDespacho` |
| 7 | `GetDispatchHistoryAsync` | READ | `tblBitacora` + `tblProductos` |
| 8 | `ColumnExistsAsync` | READ | `INFORMATION_SCHEMA.COLUMNS` |

> No existe ningún **DELETE** — todos los registros se conservan para auditoría.

---

*Generado el 2026-06-06 — Proyecto LoadManager / UAAC*
