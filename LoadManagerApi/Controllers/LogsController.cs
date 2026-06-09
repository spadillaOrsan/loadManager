using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace LoadManagerApi.Controllers;

[ApiController]
[Route("api/logs")]
public sealed class LogsController(IAppLogService logService) : ControllerBase
{
    /// <summary>
    /// Almacena un log en la raiz del proyecto (carpeta configurable en appsettings.json),
    /// con la estructura Logs/AÑO/MES/DIA/SUCCESS|ERROR/Log_yyyyMMdd.txt.
    /// Permite centralizar tambien los logs que envia la terminal.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> WriteAsync(
        [FromBody] ApiLogEntry entry,
        CancellationToken cancellationToken)
    {
        if (entry is null)
        {
            return BadRequest("El cuerpo del log es obligatorio.");
        }

        var path = await logService.WriteAsync(entry, cancellationToken);
        return Ok(new { stored = true, path });
    }
}
