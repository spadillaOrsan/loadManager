using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace LoadManagerApi.Controllers;

[ApiController]
[Route("api/dispatch-types")]
public sealed class DispatchTypesController(IGasStationService gasStationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DispatchTypeOption>>> GetAsync(
        CancellationToken cancellationToken)
    {
        return Ok(await gasStationService.GetDispatchTypesAsync(cancellationToken));
    }
}
