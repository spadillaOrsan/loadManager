using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace LoadManagerApi.Controllers;

[ApiController]
[Route("api/impressions")]
public sealed class ImpressionsController(IGasStationService gasStationService) : ControllerBase
{
    // Registra una impresion del ticket y devuelve el numero de ticket (conteo del folio).
    [HttpPost("{transaccion:int}")]
    public async Task<ActionResult<ImpressionResult>> RegisterAsync(
        int transaccion,
        CancellationToken cancellationToken)
    {
        var ticketNumber = await gasStationService.RegisterImpressionAsync(transaccion, cancellationToken);
        return Ok(new ImpressionResult { TicketNumber = ticketNumber });
    }
}
