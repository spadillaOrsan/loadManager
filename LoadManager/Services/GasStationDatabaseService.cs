using System.Data;
using LoadManager.Models;
using LoadManager.Services.Interfaces;
using Microsoft.Data.SqlClient;

namespace LoadManager.Services;

public sealed class GasStationDatabaseService(
    IAppSettingsProvider settingsProvider,
    IConsoleLogService consoleLogService) : IGasStationDatabaseService
{
    private static readonly string[] RequiredObjects =
    [
        "dbo.tblMangueras",
        "dbo.tblProductos",
        "dbo.tblTipoDespacho",
        "dbo.tblDispensarios",
        "dbo.sp_UA_foliosecuencia_app",
        "dbo.sp_UA_Bitacora_APP"
    ];

    public async Task<ConsoleCommandResult> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
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

    public async Task<int> GetNextFolioAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand("sp_UA_foliosecuencia_app", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var value = await command.ExecuteScalarAsync(cancellationToken);
            var folio = Convert.ToInt32(value);

            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "database",
                RequestFrame = "EXEC sp_UA_foliosecuencia_app",
                ResponseFrame = folio.ToString(),
                UserMessage = "Folio generado correctamente."
            }, cancellationToken);

            return folio;
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateErrorResult(
                "EXEC sp_UA_foliosecuencia_app",
                "No se pudo obtener el folio de autorizacion.",
                ex), cancellationToken);
            throw;
        }
    }

    public async Task RegisterAuthorizationAsync(FuelAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = new SqlCommand("sp_UA_Bitacora_APP", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@intTPV", request.Tpv);
            command.Parameters.AddWithValue("@intTipoVenta", request.TipoVenta);
            command.Parameters.AddWithValue("@intDispensario", request.Dispensario);
            command.Parameters.AddWithValue("@intManguera", request.Manguera);
            command.Parameters.AddWithValue("@intProducto", request.Producto);
            command.Parameters.AddWithValue("@intUsuario", request.Usuario);
            command.Parameters.AddWithValue("@strTarjeta", request.Tarjeta);
            command.Parameters.AddWithValue("@intTipoProgramado", request.TipoProgramado);
            command.Parameters.AddWithValue("@dblProgramado", request.Programado);
            command.Parameters.AddWithValue("@intFolioSecuencia", request.FolioSecuencia);
            command.Parameters.AddWithValue("@strCliente", request.Cliente);
            command.Parameters.AddWithValue("@strBandaMagnetica", request.BandaMagnetica);
            command.Parameters.AddWithValue("@strVehiculo", request.Vehiculo);
            command.Parameters.AddWithValue("@strOdometro", request.Odometro);
            command.Parameters.AddWithValue("@strPie1", string.Empty);
            command.Parameters.AddWithValue("@strPie2", string.Empty);
            command.Parameters.AddWithValue("@strPie3", string.Empty);
            command.Parameters.AddWithValue("@strPie4", string.Empty);

            await command.ExecuteNonQueryAsync(cancellationToken);

            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "database",
                RequestFrame = $"EXEC sp_UA_Bitacora_APP @intTPV={request.Tpv}, @intDispensario={request.Dispensario}, @intManguera={request.Manguera}, @intProducto={request.Producto}, @dblProgramado={request.Programado}, @intFolioSecuencia={request.FolioSecuencia}",
                ResponseFrame = "OK",
                UserMessage = "Bitacora registrada correctamente."
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await LogAndReturnAsync(CreateErrorResult(
                "EXEC sp_UA_Bitacora_APP",
                "No se pudo registrar la bitacora de autorizacion.",
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
                var id = GetFirstInt(
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

                var description = GetFirstString(
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
                SELECT TOP 150
                    b.*,
                    tp.strDescripcion AS ProductoDescripcion,
                    tp.dblPrecioU AS ProductoPrecio
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
                    Sequence = GetFirstInt(reader, "intFolioSecuencia", "intSecuencia", "Folio"),
                    Tpv = GetFirstInt(reader, "intTPV"),
                    Dispenser = GetFirstInt(reader, "intDispensario"),
                    Hose = GetFirstInt(reader, "intManguera"),
                    Product = GetFirstInt(reader, "intProducto"),
                    ProductDescription = GetFirstString(reader, "ProductoDescripcion", "strProducto"),
                    DispatchTypeId = GetFirstInt(reader, "intTipoProgramado", "intTipoDespacho", "intTipoCarga"),
                    ProgrammedAmount = GetFirstDecimal(reader, "dblProgramado", "dblCantidadProgramada"),
                    Amount = GetFirstDecimal(reader, "dblImporte", "dblMonto", "dblVendido", "dblVenta"),
                    Liters = GetFirstDecimal(reader, "dblLitros", "dblVolumen", "dblCantidad"),
                    Price = GetFirstDecimal(reader, "dblPrecioU", "ProductoPrecio", "dblPrecio"),
                    CreatedAt = GetFirstDateTime(reader, "dtmFecha", "dtFecha", "Fecha", "fecFecha", "dteFecha"),
                    IsCanceled = GetFirstBool(reader, "bitCancelada", "bitCancelado", "Cancelada", "Cancelado"),
                    IsClosed = GetFirstBool(reader, "bitCerrada", "bitCerrado", "Cerrada", "Cerrado")
                });
            }

            await LogAndReturnAsync(new ConsoleCommandResult
            {
                IsSuccess = true,
                CommandName = "database",
                RequestFrame = normalizedFolio is null
                    ? "SELECT TOP 150 historial FROM dbo.tblBitacora"
                    : $"SELECT historial FROM dbo.tblBitacora WHERE folio LIKE {normalizedFolio}",
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

    private static int GetFirstInt(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            if (HasColumn(reader, name) && reader[name] is not DBNull)
            {
                return Convert.ToInt32(reader[name]);
            }
        }

        return 0;
    }

    private static string GetFirstString(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            if (HasColumn(reader, name) && reader[name] is not DBNull)
            {
                return Convert.ToString(reader[name]) ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static decimal GetFirstDecimal(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            if (HasColumn(reader, name) && reader[name] is not DBNull)
            {
                return Convert.ToDecimal(reader[name]);
            }
        }

        return 0m;
    }

    private static bool GetFirstBool(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            if (HasColumn(reader, name) && reader[name] is not DBNull)
            {
                return Convert.ToBoolean(reader[name]);
            }
        }

        return false;
    }

    private static DateTime? GetFirstDateTime(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            if (HasColumn(reader, name) && reader[name] is not DBNull)
            {
                return Convert.ToDateTime(reader[name]);
            }
        }

        return null;
    }

    private static bool HasColumn(SqlDataReader reader, string name)
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (string.Equals(reader.GetName(index), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsProvider.GetSettingsAsync(cancellationToken);
        var database = settings.Database;

        if (string.IsNullOrWhiteSpace(database.Server) || string.IsNullOrWhiteSpace(database.Database))
        {
            throw new InvalidOperationException("Capture servidor y base de datos en el modulo de configuracion.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = database.Server,
            InitialCatalog = database.Database,
            UserID = database.UserId,
            Password = database.Password,
            Encrypt = database.Encrypt,
            TrustServerCertificate = database.TrustServerCertificate,
            ConnectTimeout = database.ConnectionTimeoutSeconds
        };

        var connection = new SqlConnection(builder.ConnectionString);
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
        var logPath = await consoleLogService.WriteAsync(result, cancellationToken);

        return new ConsoleCommandResult
        {
            IsSuccess = result.IsSuccess,
            UserMessage = result.UserMessage,
            CommandName = result.CommandName,
            RequestFrame = result.RequestFrame,
            ResponseFrame = result.ResponseFrame,
            TechnicalMessage = result.TechnicalMessage,
            LogPath = logPath
        };
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
