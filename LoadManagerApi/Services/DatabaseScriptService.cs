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
                // Siempre se ejecuta el script (CREATE OR ALTER): si ya existe un SP
                // con ese nombre, se reemplaza por la version de la carpeta Script.
                var script = await ReadScriptAsync(scriptPath, cancellationToken);
                await using var command = new SqlCommand(script, connection)
                {
                    CommandType = CommandType.Text
                };

                await command.ExecuteNonQueryAsync(cancellationToken);
                await logService.WriteAsync(new ApiLogEntry
                {
                    Level = "Success",
                    Service = nameof(DatabaseScriptService),
                    Message = "Procedimiento almacenado aplicado (CREATE OR ALTER).",
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

    private Task<string> ReadScriptAsync(
        string scriptPath,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(
            environment.ContentRootPath,
            scriptPath.Replace('/', Path.DirectorySeparatorChar));

        return File.ReadAllTextAsync(fullPath, cancellationToken);
    }
}
