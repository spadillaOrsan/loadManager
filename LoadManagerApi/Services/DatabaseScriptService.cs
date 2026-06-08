using System.Data;
using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;
using Microsoft.Data.SqlClient;

namespace LoadManagerApi.Services;

public sealed class DatabaseScriptService(
    IWebHostEnvironment environment,
    IAppLogService logService) : IDatabaseScriptService
{
    private static readonly (string ObjectName, string ScriptPath)[] StoredProcedureScripts =
    [
        ("dbo.sp_folio_app", "Script/sp_folio_app.sql"),
        ("dbo.sp_bitacora_app", "Script/sp_bitacora_app.sql")
    ];

    public async Task EnsureRequiredStoredProceduresAsync(
        SqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        foreach (var (objectName, scriptPath) in StoredProcedureScripts)
        {
            await logService.WriteAsync(new ApiLogEntry
            {
                Level = "Info",
                Service = nameof(DatabaseScriptService),
                Message = "Validando procedimiento almacenado requerido.",
                RequestBody = $"Objeto={objectName} | Script={scriptPath}"
            }, cancellationToken);

            if (await DatabaseObjectExistsAsync(connection, objectName, cancellationToken))
            {
                await logService.WriteAsync(new ApiLogEntry
                {
                    Level = "Success",
                    Service = nameof(DatabaseScriptService),
                    Message = "El procedimiento almacenado ya existe.",
                    ResponseBody = objectName
                }, cancellationToken);
                continue;
            }

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
                Message = "Se creo el procedimiento almacenado requerido.",
                RequestBody = script,
                ResponseBody = $"Objeto={objectName} | Script={scriptPath}"
            }, cancellationToken);
        }
    }

    private static async Task<bool> DatabaseObjectExistsAsync(
        SqlConnection connection,
        string objectName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT CASE WHEN OBJECT_ID(@objectName, 'P') IS NULL THEN 0 ELSE 1 END",
            connection);
        command.Parameters.Add("@objectName", SqlDbType.NVarChar, 256).Value = objectName;

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
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
