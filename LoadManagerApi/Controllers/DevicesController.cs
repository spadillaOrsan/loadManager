using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace LoadManagerApi.Controllers;

[ApiController]
[Route("api/devices")]
public sealed class DevicesController(IGasStationService gasStationService) : ControllerBase
{
    /// <summary>
    /// Valida si el equipo (por su IP) se encuentra autorizado en tblConexionesAutorizadas.
    /// La IP puede enviarse por query (?ip=) o, si se omite, se usa la IP remota de la llamada.
    /// </summary>
    [HttpGet("authorization")]
    public async Task<ActionResult<DeviceAuthorizationResult>> CheckAuthorizationAsync(
        [FromQuery] string? ip,
        CancellationToken cancellationToken)
    {
        var ipAddress = string.IsNullOrWhiteSpace(ip)
            ? HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? string.Empty
            : ip.Trim();

        var result = await gasStationService.CheckDeviceAuthorizationAsync(ipAddress, cancellationToken);
        return Ok(result);
    }
}
