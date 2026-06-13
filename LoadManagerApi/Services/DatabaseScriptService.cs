using System.Data;
using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;
using Microsoft.Data.SqlClient;

namespace LoadManagerApi.Services;

public sealed class DatabaseScriptService(
    IWebHostEnvironment environment,
    IAppLogService logService) : IDatabaseScriptService
{
    // Solo se asegura sp_folio_app (compatible con compat 100). La bitacora usa el SP
    // existente sp_Bitacora_APP con parametros individuales, porque el script @Json
    // requiere OPENJSON (compatibilidad 130+) y la BD esta en compat 100.
    private static readonly (string ObjectName, string ScriptPath)[] StoredProcedureScripts =
    [
        ("dbo.sp_folio_app", "Script/sp_folio_app.sql")
    ];

    // Los scripts son CREATE OR ALTER: se aplican una sola vez por arranque del
    // proceso para reemplazar la version existente sin penalizar cada llamada.
    private static bool ensured;
    private static readonly SemaphoreSlim EnsureLock = new(1, 1);

    public async Task EnsureRequiredStoredProceduresAsync(
        SqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (ensured)
        {
            return;
        }

        await EnsureLock.WaitAsync(cancellationToken);
        try
        {
            if (ensured)
            {
                return;
            }

            foreach (var (objectName, scriptPath) in StoredProcedureScripts)
            {
                // 1) Validar si el SP YA existe en la base de datos.
                if (await StoredProcedureExistsAsync(connection, objectName, cancellationToken))
                {
                    continue; // ya existe: no hay nada que hacer.
                }

                // 2) No existe: intentar crearlo desde el .sql de la carpeta Script.
                var fullPath = Path.Combine(
                    environment.ContentRootPath,
                    scriptPath.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(fullPath))
                {
                    // No existe el SP ni su script: se registra el error y se continua,
                    // para no tronar la app aqui (en vez de lanzar FileNotFoundException).
                    await logService.WriteAsync(new ApiLogEntry
                    {
                        Level = "Error",
                        Service = nameof(DatabaseScriptService),
                        Message = "El procedimiento no existe en la BD y tampoco se encontro su script para crearlo.",
                        RequestBody = $"Objeto={objectName} | Script={scriptPath}",
                        ResponseBody = "No creado"
                    }, cancellationToken);
                    continue;
                }

                var script = await File.ReadAllTextAsync(fullPath, cancellationToken);
                await using var command = new SqlCommand(script, connection)
                {
                    CommandType = CommandType.Text
                };

                await command.ExecuteNonQueryAsync(cancellationToken);
                await logService.WriteAsync(new ApiLogEntry
                {
                    Level = "Success",
                    Service = nameof(DatabaseScriptService),
                    Message = "Procedimiento almacenado no existia: se creo desde el script.",
                    RequestBody = $"Objeto={objectName} | Script={scriptPath}",
                    ResponseBody = objectName
                }, cancellationToken);
            }

            ensured = true;
        }
        finally
        {
            EnsureLock.Release();
        }
    }

    private static async Task<bool> StoredProcedureExistsAsync(
        SqlConnection connection,
        string objectName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT OBJECT_ID(@name, 'P');", connection);
        command.Parameters.AddWithValue("@name", objectName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && result is not DBNull;
    }
}
