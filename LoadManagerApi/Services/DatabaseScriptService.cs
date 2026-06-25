using System.Data;
using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;
using Microsoft.Data.SqlClient;

namespace LoadManagerApi.Services;

public sealed class DatabaseScriptService(
    IWebHostEnvironment environment,
    IAppLogService logService) : IDatabaseScriptService
{
    // Todos los scripts usan CREATE OR ALTER: se aplican una vez por arranque para
    // garantizar que la BD siempre tenga la version del repositorio.
    private static readonly (string ObjectName, string ScriptPath)[] StoredProcedureScripts =
    [
        ("dbo.sp_folio_app",    "Script/sp_folio_app.sql"),
        ("dbo.sp_bitacora_app", "Script/sp_bitacora_app.sql")
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
                var fullPath = Path.Combine(
                    environment.ContentRootPath,
                    scriptPath.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(fullPath))
                {
                    await logService.WriteAsync(new ApiLogEntry
                    {
                        Level = "Error",
                        Service = nameof(DatabaseScriptService),
                        Message = "No se encontro el script para aplicar el procedimiento.",
                        RequestBody = $"Objeto={objectName} | Script={scriptPath}",
                        ResponseBody = "No aplicado"
                    }, cancellationToken);
                    continue;
                }

                var script = await File.ReadAllTextAsync(fullPath, cancellationToken);
                try
                {
                    await using var command = new SqlCommand(script, connection)
                    {
                        CommandType = CommandType.Text
                    };
                    await command.ExecuteNonQueryAsync(cancellationToken);
                    await logService.WriteAsync(new ApiLogEntry
                    {
                        Level = "Success",
                        Service = nameof(DatabaseScriptService),
                        Message = "Procedimiento almacenado aplicado desde script (CREATE OR ALTER).",
                        RequestBody = $"Objeto={objectName} | Script={scriptPath}",
                        ResponseBody = objectName
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    await logService.WriteAsync(new ApiLogEntry
                    {
                        Level = "Error",
                        Service = nameof(DatabaseScriptService),
                        Message = "No se pudo aplicar el script del procedimiento almacenado.",
                        RequestBody = $"Objeto={objectName} | Script={scriptPath}",
                        Exception = ex.ToString()
                    }, cancellationToken);
                }
            }

            ensured = true;
        }
        finally
        {
            EnsureLock.Release();
        }
    }


}
