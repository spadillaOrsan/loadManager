using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace LoadManagerApi.Controllers;

[ApiController]
[Route("api/dispensers")]
public sealed class DispensersController(IGasStationService gasStationService) : ControllerBase
{
    [HttpGet("{dispenser:int}/products")]
    public async Task<ActionResult<IReadOnlyList<DispenserProductOption>>> GetProductsAsync(
        int dispenser,
        CancellationToken cancellationToken)
    {
        return Ok(await gasStationService.GetDispenserProductsAsync(dispenser, cancellationToken));
    }

    [HttpPut("{dispenser:int}/status")]
    public async Task<IActionResult> UpdateStatusAsync(
        int dispenser,
        DispenserStatusRequest request,
        CancellationToken cancellationToken)
    {
        await gasStationService.UpdateDispenserStatusAsync(dispenser, request.Status, cancellationToken);
        return NoContent();
    }
}
