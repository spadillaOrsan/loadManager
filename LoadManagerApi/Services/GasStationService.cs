using LoadManagerApi.Interfaces;
using LoadManagerApi.Helpers;
using LoadManagerApi.Models;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Data;

namespace LoadManagerApi.Services;

public sealed class GasStationService(
    IConfiguration configuration,
    IDatabaseScriptService databaseScriptService,
    IAppLogService logService) : IGasStationService
{
    // Candado en memoria por dispensario: primera linea de defensa contra la doble autorizacion
    // simultanea. Garantiza que solo una solicitud por dispensario se procesa a la vez dentro
    // del mismo proceso, incluso si sp_getapplock o el check de bitacora fallan.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> DispenserSemaphores = new();

    // Registro en memoria del ultimo folio autorizado por dispensario con su hora UTC.
    // Segunda linea de defensa: bloquea al segundo equipo aunque datFechaHora en BD sea NULL.
    private static readonly ConcurrentDictionary<int, DateTime> LastAuthorizationUtc = new();

    private static SemaphoreSlim GetDispenserSemaphore(int dispenser) =>
        DispenserSemaphores.GetOrAdd(dispenser, _ => new SemaphoreSlim(1, 1));

    private static readonly string[] RequiredObjects =
    [
        "dbo.tblMangueras",
        "dbo.tblProductos",
        "dbo.tblTipoDespacho",
        "dbo.tblDispensarios",
        "dbo.tblParametros",
        "dbo.tblBitacora",
        "dbo.tblTiposVenta",
        "dbo.tblUsuarioIsla",
        "dbo.tblConsultasUG",
        "dbo.tblImpresiones",
        "dbo.sp_folio_app",
        "dbo.sp_bitacora_app"
    ];

    // Ventana (segundos) para el rechazo anti doble-tap: dos autorizaciones del mismo
    // dispensario dentro de este lapso se consideran el mismo "presionar al mismo tiempo".
    private const int DoubleTapWindowSeconds = 10;

    public async Task<ConsoleCommandResult> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await databaseScriptService.EnsureRequiredStoredProceduresAsync(connection, cancellationToken);
            var missingObjects = await GetMissingRequiredObjectsAsync(connection, cancellationToken);
            if (missingObjects.Count > 0)
            {
                return await LogAndReturnAsync(new ConsoleCommandResult
                {
                    IsSuccess = false,
                    CommandName = "database",
                    RequestFrame = "SQL CHECK REQUIRED OBJECTS",
                    ResponseFrame = connection.Database,
                    UserMessage = "La conexion a base de datos no contiene las tablas o procedimientos requeridos.",
                    TechnicalMessage = $"Base conectada: {connection.Database}. Objetos faltantes: {string.Join(", ", missingObjects)}"
                }, cancellationToken);
            }

            var result = new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "database",
                RequestFrame = "SQL CHECK CONNECTION",
                ResponseFrame = connection.Database,
                UserMessage = "La conexion a base de datos fue correcta."
            };

            return await LogAndReturnAsync(result, cancellationToken);
        }
        catch (Exception ex)
        {
            return await LogAndReturnAsync(CreateErrorResult(
                "SQL CHECK CONNECTION",
                "No se pudo conectar a la base de datos.",
                ex), cancellationToken);
        }
    }

    public async Task<DeviceAuthorizationResult> CheckDeviceAuthorizationAsync(
        string ipAddress,
        string macAddress,
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        var ip = (ipAddress ?? string.Empty).Trim();
        var mac = (macAddress ?? string.Empty).Trim();
        var device = (deviceName ?? string.Empty).Trim();

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);

            // El equipo esta autorizado si existe al menos una fila con su IP y bitAutorizada = 1.
            await using var command = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.tblConexionesAutorizadas WHERE strIP = @ip AND bitAutorizada = 1",
                connection);
            command.Parameters.AddWithValue("@ip", ip);

            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            var authorized = count > 0;

            // El nombre se toma del que envia el equipo (para auditoria/registro).
            var resolvedName = device;
            var message = authorized
                ? $"Equipo autorizado ({resolvedName})."
                : "El equipo no tiene autorizacion.";

            // El resultado (autorizado/no) se registra con IP, MAC y dispositivo en su
            // campo dedicado, en el log estructurado (SUCCESS o ERROR segun el caso).
            await logService.WriteAsync(new ApiLogEntry
            {
                Level = authorized ? "Success" : "Error",
                Service = "GasStationService.DeviceAuthorization",
                Message = authorized ? "Equipo autorizado." : "Intento de conexion NO autorizado.",
                IpAddress = ip,
                MacAddress = mac,
                DeviceName = string.IsNullOrWhiteSpace(resolvedName) ? device : resolvedName,
                RequestBody = $"SELECT COUNT(*) tblConexionesAutorizadas WHERE strIP={ip} AND bitAutorizada=1",
                ResponseBody = $"Autorizado={authorized}"
            }, cancellationToken);

            return new DeviceAuthorizationResult
            {
                IsAuthorized = authorized,
                IpAddress = ip,
                DeviceName = resolvedName,
                Message = message
            };
        }
        catch (Exception ex)
        {
            await logService.WriteAsync(new ApiLogEntry
            {
                Level = "Error",
                Service = "GasStationService.DeviceAuthorization",
                Message = "No se pudo validar la autorizacion del equipo.",
                IpAddress = ip,
                MacAddress = mac,
                DeviceName = device,
                Exception = ex.ToString()
            }, cancellationToken);

            return new DeviceAuthorizationResult
            {
                IsAuthorized = false,
                IpAddress = ip,
                DeviceName = device,
                Message = "No se pudo validar la autorizacion del equipo."
            };
        }
    }

    public async Task<int> RegisterAuthorizationAsync(
        FuelAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Primera linea de defensa: semaforo en memoria por dispensario.
        // Garantiza que dos solicitudes simultaneas para el mismo dispensario
        // se serialicen sin importar lo que ocurra en la BD.
        var semaphore = GetDispenserSemaphore(request.Dispensario);
        var acquired = await semaphore.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        if (!acquired)
        {
            throw new DispenserBusyException(request.Dispensario);
        }

        try
        {
            return await RegisterAuthorizationCoreAsync(request, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<int> RegisterAuthorizationCoreAsync(
        FuelAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await databaseScriptService.EnsureRequiredStoredProceduresAsync(connection, cancellationToken);

        // Candado de sesion (Session scope): persiste aunque el COMMIT libere la conexion.
        // Garantiza serializacion incluso entre instancias/procesos distintos de IIS
        // (web garden) que comparten la misma base de datos.
        var lockResource = $"LM_D{request.Dispensario}";
        var lockAcquired = false;

        try
        {
            await using (var acquireLock = new SqlCommand("sp_getapplock", connection)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                acquireLock.Parameters.AddWithValue("@Resource", lockResource);
                acquireLock.Parameters.AddWithValue("@LockMode", "Exclusive");
                acquireLock.Parameters.AddWithValue("@LockOwner", "Session");
                acquireLock.Parameters.AddWithValue("@LockTimeout", 10000);
                var rv = acquireLock.Parameters.Add("@rv", SqlDbType.Int);
                rv.Direction = ParameterDirection.ReturnValue;
                await acquireLock.ExecuteNonQueryAsync(cancellationToken);
                if (rv.Value is int rvInt && rvInt < 0)
                    throw new DispenserBusyException(request.Dispensario);
            }
            lockAcquired = true;

            // Check rapido en memoria (evita una ida a BD si el mismo proceso
            // acaba de autorizar este dispensario hace menos de N segundos).
            if (LastAuthorizationUtc.TryGetValue(request.Dispensario, out var lastAuth)
                && (DateTime.UtcNow - lastAuth).TotalSeconds < DoubleTapWindowSeconds)
            {
                throw new DispenserBusyException(request.Dispensario);
            }

            // Sin transaccion exterior: sp_folio_app abre y commitea su propio
            // contador antes del INSERT, asi que si el INSERT falla el contador ya
            // quedo avanzado y el siguiente intento usa un folio nuevo (no hay loop).
            // La serializacion la garantiza sp_getapplock (Session scope) arriba.
            await using (var checkCmd = new SqlCommand(
                """
                SELECT TOP 1 1
                WHERE EXISTS (
                    SELECT 1 FROM dbo.tblBitacora WITH (READCOMMITTEDLOCK)
                    WHERE intDispensario = @dispensario
                      AND datFechaHora >= DATEADD(SECOND, -@ventana, GETDATE())
                )
                OR EXISTS (
                    SELECT 1 FROM dbo.tblDispensarios WITH (READCOMMITTEDLOCK)
                    WHERE intDispensario = @dispensario
                      AND intEstatus IN (4, 5, 6)
                )
                """, connection))
            {
                checkCmd.Parameters.AddWithValue("@dispensario", request.Dispensario);
                checkCmd.Parameters.AddWithValue("@ventana", DoubleTapWindowSeconds);
                var exists = await checkCmd.ExecuteScalarAsync(cancellationToken);
                if (exists is not null)
                    throw new DispenserBusyException(request.Dispensario);
            }

            long folio;
            await using (var bitacoraCommand = new SqlCommand("dbo.sp_bitacora_app", connection)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                bitacoraCommand.Parameters.AddWithValue("@intTPV",            request.Tpv);
                bitacoraCommand.Parameters.AddWithValue("@intTipoVenta",      request.TipoVenta);
                bitacoraCommand.Parameters.AddWithValue("@intDispensario",    request.Dispensario);
                bitacoraCommand.Parameters.AddWithValue("@intManguera",       request.Manguera);
                bitacoraCommand.Parameters.AddWithValue("@intProducto",       request.Producto);
                bitacoraCommand.Parameters.AddWithValue("@intUsuario",        request.Usuario);
                bitacoraCommand.Parameters.AddWithValue("@strTarjeta",        request.Tarjeta ?? string.Empty);
                bitacoraCommand.Parameters.AddWithValue("@intTipoProgramado", request.TipoProgramado);
                bitacoraCommand.Parameters.AddWithValue("@dblProgramado",     (double)request.Programado);
                bitacoraCommand.Parameters.AddWithValue("@strCliente",        request.Cliente ?? string.Empty);
                bitacoraCommand.Parameters.AddWithValue("@strBandaMagnetica", request.BandaMagnetica ?? string.Empty);
                bitacoraCommand.Parameters.AddWithValue("@strVehiculo",       request.Vehiculo ?? string.Empty);
                bitacoraCommand.Parameters.AddWithValue("@strOdometro",       request.Odometro ?? string.Empty);
                bitacoraCommand.Parameters.AddWithValue("@strPie1",           "Adm.Cargas");
                bitacoraCommand.Parameters.AddWithValue("@strPie2",           string.Empty);
                bitacoraCommand.Parameters.AddWithValue("@strPie3",           string.Empty);
                bitacoraCommand.Parameters.AddWithValue("@strPie4",           string.Empty);
                await using var reader = await bitacoraCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new InvalidOperationException("sp_bitacora_app no devolvio el folio generado.");
                folio = reader.GetInt64(reader.GetOrdinal("intFolioSecuencia"));
            }

            LastAuthorizationUtc[request.Dispensario] = DateTime.UtcNow;

            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "database",
                RequestFrame = $"EXEC dbo.sp_Bitacora_APP @Json={{TPV={request.Tpv}, Disp={request.Dispensario}, Mang={request.Manguera}, Prod={request.Producto}, Tipo={request.TipoProgramado}, Prog={request.Programado}}}",
                ResponseFrame = $"Folio: {folio}",
                UserMessage = "Folio y bitacora registrados correctamente."
            }, cancellationToken);

            return (int)folio;
        }
        finally
        {
            // El candado de sesion SIEMPRE debe liberarse explicitamente.
            // Si la conexion se cierra sin liberarlo, SQL Server lo libera
            // automaticamente al terminar la sesion.
            if (lockAcquired)
            {
                try
                {
                    await using var releaseLock = new SqlCommand("sp_releaseapplock", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    releaseLock.Parameters.AddWithValue("@Resource", lockResource);
                    releaseLock.Parameters.AddWithValue("@LockOwner", "Session");
                    await releaseLock.ExecuteNonQueryAsync(CancellationToken.None);
                }
                catch { }
            }
        }
    }
    catch (DispenserBusyException)
    {
        throw; // se propaga al controlador, que devuelve 409.
    }
    catch (Exception ex)
    {
        await LogAndReturnAsync(CreateErrorResult(
            "EXEC dbo.sp_folio_app + dbo.sp_Bitacora_APP",
            "No se pudo generar el folio ni registrar la bitacora de autorizacion.",
            ex), cancellationToken);
        throw;
    }
}

    public async Task<int> RegisterImpressionAsync(int transaccion, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);

            // Inserta el registro de impresion y devuelve el numero de ticket = total de
            // impresiones del folio. intTipo = 1 la primera vez, 2 en reimpresiones.
            await using var command = new SqlCommand("""
                DECLARE @prev INT = (SELECT COUNT(*) FROM dbo.tblImpresiones WHERE intTransaccion = @t);
                INSERT INTO dbo.tblImpresiones (intTransaccion, intTipo, datFechaHora)
                VALUES (@t, CASE WHEN @prev = 0 THEN 1 ELSE 2 END, GETDATE());
                SELECT COUNT(*) FROM dbo.tblImpresiones WHERE intTransaccion = @t;
                """, connection);
            command.Parameters.AddWithValue("@t", transaccion);

            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            var ticketNumber = scalar is null || scalar is DBNull ? 0 : Convert.ToInt32(scalar);

            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "database",
                RequestFrame = $"INSERT dbo.tblImpresiones intTransaccion={transaccion}",
                ResponseFrame = $"Ticket #{ticketNumber}",
                UserMessage = "Impresion registrada correctamente."
            }, cancellationToken);

            return ticketNumber;
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateErrorResult(
                "INSERT dbo.tblImpresiones",
                "No se pudo registrar la impresion del ticket.",
                ex), cancellationToken);
            throw;
        }
    }

    public async Task UpdateDispenserStatusAsync(int dispenser, int status, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand(
                "UPDATE tblDispensarios SET intEstatus = @status WHERE intDispensario = @dispenser",
                connection);

            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@dispenser", dispenser);

            await command.ExecuteNonQueryAsync(cancellationToken);

            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "database",
                RequestFrame = $"UPDATE tblDispensarios SET intEstatus = {status} WHERE intDispensario = {dispenser}",
                ResponseFrame = "OK",
                UserMessage = "Estatus de dispensario actualizado."
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateErrorResult(
                $"UPDATE tblDispensarios SET intEstatus = {status} WHERE intDispensario = {dispenser}",
                "No se pudo actualizar el estatus del dispensario en base de datos.",
                ex), cancellationToken);
        }
    }

    public async Task<IReadOnlyList<DispenserProductOption>> GetDispenserProductsAsync(
        int dispenser,
        CancellationToken cancellationToken = default)
    {
        var products = new List<DispenserProductOption>();

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand("""
                SELECT tm.intManguera, tm.intProducto, tp.strDescripcion, tp.dblPrecioU
                FROM dbo.tblMangueras tm
                INNER JOIN dbo.tblProductos tp ON tp.intProducto = tm.intProducto
                WHERE tm.bitActivo = 1
                  AND tm.intDispensario = @dispenser
                  AND tp.bitEstatus = 1
                ORDER BY tm.intManguera
                """, connection);

            command.Parameters.AddWithValue("@dispenser", dispenser);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                products.Add(new DispenserProductOption
                {
                    Hose = Convert.ToInt32(reader["intManguera"]),
                    ProductId = Convert.ToInt32(reader["intProducto"]),
                    Description = Convert.ToString(reader["strDescripcion"]) ?? string.Empty,
                    Price = reader["dblPrecioU"] == DBNull.Value
                        ? null
                        : Convert.ToDecimal(reader["dblPrecioU"])
                });
            }

            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "database",
                RequestFrame = $"SELECT mangueras/productos/precios WHERE intDispensario = {dispenser}",
                ResponseFrame = $"{products.Count} productos",
                UserMessage = "Productos del dispensario consultados correctamente."
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateErrorResult(
                $"SELECT mangueras/productos/precios WHERE intDispensario = {dispenser}",
                "No se pudieron consultar los productos del dispensario.",
                ex), cancellationToken);
        }

        return products;
    }

    public async Task<IReadOnlyList<DispatchTypeOption>> GetDispatchTypesAsync(CancellationToken cancellationToken = default)
    {
        var dispatchTypes = new List<DispatchTypeOption>();

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand(
                "SELECT * FROM tblTipoDespacho WHERE bitEstatus = 1",
                connection);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var fallbackId = 1;
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = SqlDataReaderHelper.GetFirstInt(
                    reader,
                    "intTipoDespacho",
                    "intTipoProgramado",
                    "intTipoVenta",
                    "intTipoCarga",
                    "intID",
                    "Id",
                    "ID");
                if (id <= 0)
                {
                    id = fallbackId;
                }

                var description = SqlDataReaderHelper.GetFirstString(
                    reader,
                    "strDescripcion",
                    "strTipoDespacho",
                    "strTipoProgramado",
                    "strTipoVenta",
                    "strNombre",
                    "Descripcion",
                    "vchDescripcion",
                    "Nombre");

                dispatchTypes.Add(new DispatchTypeOption
                {
                    Id = id,
                    Description = string.IsNullOrWhiteSpace(description) ? $"Tipo {id}" : description
                });

                fallbackId++;
            }

            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "database",
                RequestFrame = "SELECT * FROM tblTipoDespacho WHERE bitEstatus = 1",
                ResponseFrame = $"{dispatchTypes.Count} tipos",
                UserMessage = "Tipos de despacho consultados correctamente."
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateErrorResult(
                "SELECT * FROM tblTipoDespacho WHERE bitEstatus = 1",
                "No se pudieron consultar los tipos de despacho.",
                ex), cancellationToken);
        }

        return dispatchTypes;
    }

    public async Task<IReadOnlyList<DispatchHistoryRecord>> GetDispatchHistoryAsync(
        string? folio,
        CancellationToken cancellationToken = default)
    {
        var records = new List<DispatchHistoryRecord>();
        var normalizedFolio = string.IsNullOrWhiteSpace(folio) ? null : folio.Trim();

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            var hasFolioSequenceColumn = await ColumnExistsAsync(
                connection,
                "dbo",
                "tblBitacora",
                "intFolioSecuencia",
                cancellationToken);
            var folioPredicate = hasFolioSequenceColumn
                ? "OR CONVERT(varchar(50), b.intFolioSecuencia) LIKE @folioLike"
                : string.Empty;

            await using var command = new SqlCommand($"""
                SELECT TOP 3
                    b.*,
                    tp.strDescripcion AS ProductoDescripcion,
                    tp.dblPrecioU AS ProductoPrecio,
                    tparam.strMarcaGasolinera AS MarcaGasolinera,
                    tparam.strRFC AS Rfc,
                    tparam.strNomEmpresa AS NombreEmpresa,
                    tparam.intEstUG AS EstacionUG,
                    CASE WHEN EXISTS (
                        SELECT 1 FROM dbo.tblTransacciones t WHERE t.intSecuencia = b.intSecuencia
                    ) THEN 1 ELSE 0 END AS EstaCerrada
                FROM dbo.tblBitacora b
                LEFT JOIN dbo.tblProductos tp ON tp.intProducto = b.intProducto
                CROSS JOIN dbo.tblParametros tparam
                WHERE @folio IS NULL
                   OR CONVERT(varchar(50), b.intSecuencia) LIKE @folioLike
                   {folioPredicate}
                ORDER BY b.intSecuencia DESC
                """, connection);

            command.Parameters.AddWithValue("@folio", normalizedFolio is null ? DBNull.Value : normalizedFolio);
            command.Parameters.AddWithValue("@folioLike", normalizedFolio is null ? DBNull.Value : $"%{normalizedFolio}%");

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                records.Add(new DispatchHistoryRecord
                {
                    Sequence = SqlDataReaderHelper.GetFirstInt(reader, "intFolioSecuencia", "intSecuencia", "Folio"),
                    Tpv = SqlDataReaderHelper.GetFirstInt(reader, "intTPV"),
                    Dispenser = SqlDataReaderHelper.GetFirstInt(reader, "intDispensario"),
                    Hose = SqlDataReaderHelper.GetFirstInt(reader, "intManguera"),
                    Product = SqlDataReaderHelper.GetFirstInt(reader, "intProducto"),
                    ProductDescription = SqlDataReaderHelper.GetFirstString(reader, "ProductoDescripcion", "strProducto"),
                    DispatchTypeId = SqlDataReaderHelper.GetFirstInt(reader, "intTipoProgramado", "intTipoDespacho", "intTipoCarga"),
                    ProgrammedAmount = SqlDataReaderHelper.GetFirstDecimal(reader, "dblProgramado", "dblCantidadProgramada"),
                    Amount = SqlDataReaderHelper.GetFirstDecimal(reader, "dblImporte", "dblMonto", "dblVendido", "dblVenta"),
                    Liters = SqlDataReaderHelper.GetFirstDecimal(reader, "dblLitros", "dblVolumen", "dblCantidad"),
                    Price = SqlDataReaderHelper.GetFirstDecimal(reader, "dblPrecioUnitario", "dblPrecioU", "ProductoPrecio", "dblPrecio"),
                    CreatedAt = SqlDataReaderHelper.GetFirstDateTime(reader, "datFechaHora", "dtmFecha", "dtFecha", "Fecha", "fecFecha", "dteFecha"),
                    IsCanceled = SqlDataReaderHelper.GetFirstBool(reader, "bitCancelada", "bitCancelado", "Cancelada", "Cancelado"),
                    IsClosed = SqlDataReaderHelper.GetFirstBool(reader, "EstaCerrada"),
                    MarcaGasolinera = SqlDataReaderHelper.GetFirstString(reader, "MarcaGasolinera"),
                    Rfc = SqlDataReaderHelper.GetFirstString(reader, "Rfc"),
                    NombreEmpresa = SqlDataReaderHelper.GetFirstString(reader, "NombreEmpresa"),
                    EstacionUG = SqlDataReaderHelper.GetFirstInt(reader, "EstacionUG")
                });
            }

            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "database",
                RequestFrame = normalizedFolio is null
                    ? "SELECT TOP 3 historial FROM dbo.tblBitacora"
                    : $"SELECT TOP 3 historial FROM dbo.tblBitacora WHERE folio LIKE {normalizedFolio}",
                ResponseFrame = $"{records.Count} registros",
                UserMessage = "Historial consultado correctamente."
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateErrorResult(
                "SELECT historial FROM dbo.tblBitacora",
                "No se pudo consultar el historial de cargas.",
                ex), cancellationToken);
        }

        return records;
    }

    public async Task<IReadOnlyList<DispatchTypeOption>> GetActiveProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var products = new List<DispatchTypeOption>();

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand("""
                SELECT intProducto, strDescripcion
                FROM dbo.tblProductos
                WHERE bitEstatus = 1
                ORDER BY strDescripcion
                """, connection);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                products.Add(new DispatchTypeOption
                {
                    Id = Convert.ToInt32(reader["intProducto"]),
                    Description = Convert.ToString(reader["strDescripcion"]) ?? string.Empty
                });
            }
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateErrorResult(
                "SELECT intProducto, strDescripcion FROM dbo.tblProductos WHERE bitEstatus = 1",
                "No se pudieron consultar los productos activos.",
                ex), cancellationToken);
        }

        return products;
    }

    public async Task<IReadOnlyList<DispatchHistoryRecord>> GetHistorialAsync(
        int dispenser,
        int hose,
        int product,
        int top,
        CancellationToken cancellationToken = default)
    {
        var records = new List<DispatchHistoryRecord>();
        var topClamped = Math.Clamp(top, 1, 100);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);

            await using var command = new SqlCommand("""
                SELECT TOP (@intTop)
                    b.intFolioCorte,
                    b.intSecuencia,
                    b.datFechaHora,
                    b.intDispensario,
                    b.intManguera,
                    b.intProducto,
                    b.dblProgramado,
                    b.dblVendido,
                    b.dblVolumenVendido,
                    b.bitCerrada,
                    b.strObservaciones,
                    tp.strDescripcion  AS ProductoDescripcion,
                    tp.dblPrecioU      AS ProductoPrecio,
                    tparam.strMarcaGasolinera AS MarcaGasolinera,
                    tparam.strRFC             AS Rfc,
                    tparam.strNomEmpresa      AS NombreEmpresa,
                    tparam.intEstUG           AS EstacionUG,
                    CASE WHEN EXISTS (
                        SELECT 1 FROM dbo.tblTransacciones t WHERE t.intSecuencia = b.intSecuencia
                    ) THEN 1 ELSE 0 END AS EstaCerrada
                FROM dbo.tblBitacora b
                LEFT JOIN dbo.tblProductos  tp     ON tp.intProducto  = b.intProducto
                LEFT JOIN dbo.tblParametros tparam ON 1=1
                WHERE b.intDispensario = @dispensario
                  AND b.intManguera   = @manguera
                  AND b.intProducto   = @producto
                ORDER BY b.datFechaHora DESC
                """, connection);

            command.Parameters.AddWithValue("@intTop",      topClamped);
            command.Parameters.AddWithValue("@dispensario", dispenser);
            command.Parameters.AddWithValue("@manguera",    hose);
            command.Parameters.AddWithValue("@producto",    product);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                records.Add(new DispatchHistoryRecord
                {
                    Sequence          = SqlDataReaderHelper.GetFirstInt(reader,      "intSecuencia"),
                    Dispenser         = SqlDataReaderHelper.GetFirstInt(reader,      "intDispensario"),
                    Hose              = SqlDataReaderHelper.GetFirstInt(reader,      "intManguera"),
                    Product           = SqlDataReaderHelper.GetFirstInt(reader,      "intProducto"),
                    ProductDescription= SqlDataReaderHelper.GetFirstString(reader,   "ProductoDescripcion"),
                    ProgrammedAmount  = SqlDataReaderHelper.GetFirstDecimal(reader,  "dblProgramado"),
                    Amount            = SqlDataReaderHelper.GetFirstDecimal(reader,  "dblVendido"),
                    Liters            = SqlDataReaderHelper.GetFirstDecimal(reader,  "dblVolumenVendido"),
                    Price             = SqlDataReaderHelper.GetFirstDecimal(reader,  "ProductoPrecio"),
                    CreatedAt         = SqlDataReaderHelper.GetFirstDateTime(reader, "datFechaHora"),
                    IsClosed          = SqlDataReaderHelper.GetFirstBool(reader,     "EstaCerrada"),
                    Observations      = SqlDataReaderHelper.GetFirstString(reader,   "strObservaciones"),
                    MarcaGasolinera   = SqlDataReaderHelper.GetFirstString(reader,   "MarcaGasolinera"),
                    Rfc               = SqlDataReaderHelper.GetFirstString(reader,   "Rfc"),
                    NombreEmpresa     = SqlDataReaderHelper.GetFirstString(reader,   "NombreEmpresa"),
                    EstacionUG        = SqlDataReaderHelper.GetFirstInt(reader,      "EstacionUG")
                });
            }

            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "database",
                RequestFrame = $"tblBitacora disp={dispenser} mang={hose} prod={product} top={topClamped}",
                ResponseFrame = $"{records.Count} registros",
                UserMessage = "Historial consultado correctamente."
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateErrorResult(
                "SELECT tblBitacora historial",
                "No se pudo consultar el historial.",
                ex), cancellationToken);
        }

        return records;
    }

    private static async Task<bool> ColumnExistsAsync(
        SqlConnection connection,
        string schema,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            SELECT 1
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema
              AND TABLE_NAME = @table
              AND COLUMN_NAME = @column
            """, connection);

        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", table);
        command.Parameters.AddWithValue("@column", column);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null;
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("GasStationDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No existe ConnectionStrings:GasStationDatabase en la configuracion de LoadManagerApi.");
        }

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<IReadOnlyList<string>> GetMissingRequiredObjectsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var foundObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var values = string.Join(", ", RequiredObjects.Select((_, index) => $"(@object{index})"));

        await using var command = new SqlCommand($"""
            SELECT required.ObjectName
            FROM (VALUES {values}) AS required(ObjectName)
            WHERE OBJECT_ID(required.ObjectName) IS NOT NULL
            """, connection);

        for (var index = 0; index < RequiredObjects.Length; index++)
        {
            command.Parameters.AddWithValue($"@object{index}", RequiredObjects[index]);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            foundObjects.Add(Convert.ToString(reader["ObjectName"]) ?? string.Empty);
        }

        return RequiredObjects
            .Where(requiredObject => !foundObjects.Contains(requiredObject))
            .ToArray();
    }

    private async Task<ConsoleCommandResult> LogAndReturnAsync(
        ConsoleCommandResult result,
        CancellationToken cancellationToken)
    {
        if (result.IsSuccess)
        {
            await logService.WriteAsync(new ApiLogEntry
            {
                Level = "Success",
                Service = nameof(GasStationService),
                Message = result.UserMessage,
                RequestBody = result.RequestFrame,
                ResponseBody = result.ResponseFrame
            }, cancellationToken);
        }
        else
        {
            await logService.WriteAsync(new ApiLogEntry
            {
                Level = "Error",
                Service = nameof(GasStationService),
                Message = result.UserMessage,
                RequestBody = result.RequestFrame,
                ResponseBody = result.ResponseFrame,
                Exception = result.TechnicalMessage
            }, cancellationToken);
        }

        return result;
    }

    private static ConsoleCommandResult CreateErrorResult(
        string request,
        string userMessage,
        Exception exception)
    {
        return new ConsoleCommandResult
        {
            IsSuccess = false,
            CommandName = "database",
            RequestFrame = request,
            UserMessage = userMessage,
            TechnicalMessage = exception.ToString()
        };
    }
}
