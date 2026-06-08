using LoadManagerApi.Interfaces;
using LoadManager.Contracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace LoadManagerApi.Controllers;

[ApiController]
[Route("api/history")]
public sealed class HistoryController(IGasStationService gasStationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DispatchHistoryRecord>>> GetAsync(
        [FromQuery] string? folio,
        CancellationToken cancellationToken)
    {
        return Ok(await gasStationService.GetDispatchHistoryAsync(folio, cancellationToken));
    }
}
