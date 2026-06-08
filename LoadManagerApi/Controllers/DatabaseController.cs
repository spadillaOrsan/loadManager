using LoadManagerApi.Interfaces;
using LoadManager.Contracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace LoadManagerApi.Controllers;

[ApiController]
[Route("api/database")]
public sealed class DatabaseController(IGasStationService gasStationService) : ControllerBase
{
    [HttpGet("health")]
    public async Task<IActionResult> CheckAsync(CancellationToken cancellationToken)
    {
        var result = await gasStationService.CheckConnectionAsync(cancellationToken);
        return result.IsSuccess
            ? Ok(result)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }
}
