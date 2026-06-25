using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace LoadManagerApi.Controllers;

[ApiController]
[Route("api/history")]
public sealed class HistoryController(
    IGasStationService gasStationService,
    IAppLogService logService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DispatchHistoryRecord>>> GetAsync(
        [FromQuery] string? folio,
        CancellationToken cancellationToken)
    {
        return Ok(await gasStationService.GetDispatchHistoryAsync(folio, cancellationToken));
    }

    [HttpGet("current")]
    public async Task<ActionResult<IReadOnlyList<DispatchHistoryRecord>>> GetCurrentAsync(
        [FromQuery] int dispenser,
        [FromQuery] int product,
        [FromQuery] int top = 3,
        CancellationToken cancellationToken = default)
    {
        await logService.WriteAsync(new ApiLogEntry
        {
            Level    = "Info",
            Service  = "HistoryController.GetCurrentAsync",
            Message  = "Consulta historial recibida",
            Method   = "GET",
            Url      = "api/history/current",
            RequestBody  = $"dispensario={dispenser}; producto={product}; top={top}",
            ResponseBody = "Metodo: SELECT TOP(@top) tblBitacora WHERE disp+prod ORDER BY datFechaHora DESC"
        }, cancellationToken);

        var result = await gasStationService.GetHistorialAsync(dispenser, product, top, cancellationToken);

        await logService.WriteAsync(new ApiLogEntry
        {
            Level   = result.Count > 0 ? "Success" : "Info",
            Service = "HistoryController.GetCurrentAsync",
            Message = $"Historial: {result.Count} registro(s) encontrado(s)",
            RequestBody  = $"dispensario={dispenser}; producto={product}; top={top}",
            ResponseBody = result.Count > 0
                ? string.Join(" | ", result.Select(r =>
                    $"Sec={r.Sequence} {r.CreatedAt:HH:mm} ${r.Amount:N2} {r.Liters:N3}L"))
                : "Sin registros"
        }, cancellationToken);

        return Ok(result);
    }
}
