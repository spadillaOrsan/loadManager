using LoadManagerApi.Interfaces;
using LoadManagerApi.Helpers;
using LoadManagerApi.Models;
using LoadManagerApi.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace LoadManagerApi.Services;

public sealed class GasStationService(
    IConfiguration configuration,
    IDatabaseScriptService databaseScriptService,
    IAppLogService logService) : IGasStationService
{
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
        "dbo.sp_folio_app",
        "dbo.sp_bitacora_app"
    ];

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
        CancellationToken cancellationToken = default)
    {
        var ip = (ipAddress ?? string.Empty).Trim();

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand(
                "SELECT TOP 1 strNombre, bitAutorizada FROM dbo.tblConexionesAutorizadas WHERE strIP = @ip",
                connection);
            command.Parameters.AddWithValue("@ip", ip);

            string? deviceName = null;
            var authorized = false;

            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (await reader.ReadAsync(cancellationToken))
                {
                    deviceName = SqlDataReaderHelper.GetFirstString(reader, "strNombre");
                    authorized = SqlDataReaderHelper.GetFirstBool(reader, "bitAutorizada");
                }
            }

            var message = authorized
                ? $"Equipo autorizado ({deviceName})."
                : "El equipo no tiene autorizacion.";

            // Se registra el resultado (autorizado o no) en el mismo metodo.
            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = authorized,
                CommandName = "device-authorization",
                RequestFrame = $"SELECT tblConexionesAutorizadas WHERE strIP = {ip}",
                ResponseFrame = $"Autorizado={authorized}; Nombre={deviceName}",
                UserMessage = message
            }, cancellationToken);

            return new DeviceAuthorizationResult
            {
                IsAuthorized = authorized,
                IpAddress = ip,
                DeviceName = deviceName ?? string.Empty,
                Message = message
            };
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateErrorResult(
                $"SELECT tblConexionesAutorizadas WHERE strIP = {ip}",
                "No se pudo validar la autorizacion del equipo.",
                ex), cancellationToken);

            return new DeviceAuthorizationResult
            {
                IsAuthorized = false,
                IpAddress = ip,
                Message = "No se pudo validar la autorizacion del equipo."
            };
        }
    }

    public async Task<int> RegisterAuthorizationAsync(
        FuelAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await databaseScriptService.EnsureRequiredStoredProceduresAsync(connection, cancellationToken);

            // 1) Generar el folio de secuencia (sp_folio_app devuelve @intFolioSecuencia OUTPUT).
            long folio;
            await using (var folioCommand = new SqlCommand("dbo.sp_folio_app", connection)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                var folioParameter = folioCommand.Parameters.Add("@intFolioSecuencia", SqlDbType.BigInt);
                folioParameter.Direction = ParameterDirection.Output;
                await folioCommand.ExecuteNonQueryAsync(cancellationToken);

                if (folioParameter.Value is null || folioParameter.Value is DBNull)
                {
                    throw new InvalidOperationException("sp_folio_app no devolvio el folio generado.");
                }

                folio = Convert.ToInt64(folioParameter.Value);
            }

            // 2) Registrar la bitacora con parametros individuales (compatible con compat 100).
            await using (var command = new SqlCommand("dbo.sp_Bitacora_APP", connection)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                command.Parameters.AddWithValue("@intTPV", request.Tpv);
                command.Parameters.AddWithValue("@intTipoVenta", request.TipoVenta);
                command.Parameters.AddWithValue("@intDispensario", request.Dispensario);
                command.Parameters.AddWithValue("@intManguera", request.Manguera);
                command.Parameters.AddWithValue("@intProducto", request.Producto);
                command.Parameters.AddWithValue("@intUsuario", request.Usuario);
                command.Parameters.AddWithValue("@strTarjeta", request.Tarjeta ?? string.Empty);
                command.Parameters.AddWithValue("@intTipoProgramado", request.TipoProgramado);
                command.Parameters.AddWithValue("@dblProgramado", request.Programado);
                command.Parameters.AddWithValue("@intFolioSecuencia", folio);
                command.Parameters.AddWithValue("@strCliente", request.Cliente ?? string.Empty);
                command.Parameters.AddWithValue("@strBandaMagnetica", request.BandaMagnetica ?? string.Empty);
                command.Parameters.AddWithValue("@strVehiculo", request.Vehiculo ?? string.Empty);
                command.Parameters.AddWithValue("@strOdometro", request.Odometro ?? string.Empty);
                command.Parameters.AddWithValue("@strPie1", "Adm.Cargas");
                command.Parameters.AddWithValue("@strPie2", string.Empty);
                command.Parameters.AddWithValue("@strPie3", string.Empty);
                command.Parameters.AddWithValue("@strPie4", string.Empty);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "database",
                RequestFrame = $"EXEC dbo.sp_Bitacora_APP @intTPV={request.Tpv}, @intDispensario={request.Dispensario}, @intManguera={request.Manguera}, @intProducto={request.Producto}, @intTipoProgramado={request.TipoProgramado}, @dblProgramado={request.Programado}, @intFolioSecuencia={folio}",
                ResponseFrame = $"Folio: {folio}",
                UserMessage = "Folio y bitacora registrados correctamente."
            }, cancellationToken);

            return (int)folio;
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
                    CASE WHEN EXISTS (
                        SELECT 1 FROM dbo.tblTransacciones t WHERE t.intSecuencia = b.intSecuencia
                    ) THEN 1 ELSE 0 END AS EstaCerrada
                FROM dbo.tblBitacora b
                LEFT JOIN dbo.tblProductos tp ON tp.intProducto = b.intProducto
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
                    IsClosed = SqlDataReaderHelper.GetFirstBool(reader, "EstaCerrada")
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
