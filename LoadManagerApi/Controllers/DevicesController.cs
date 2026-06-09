using LoadManagerApi.Helpers;
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
    /// Siempre se usa la IP REAL del equipo que hace la peticion (IP remota de la conexion).
    /// El parametro ?ip= solo se usa como respaldo cuando la llamada es local (loopback / pruebas).
    /// </summary>
    [HttpGet("authorization")]
    public async Task<ActionResult<DeviceAuthorizationResult>> CheckAuthorizationAsync(
        [FromQuery] string? ip,
        [FromQuery] string? mac,
        [FromQuery] string? device,
        CancellationToken cancellationToken)
    {
        var remoteAddress = HttpContext.Connection.RemoteIpAddress;
        var remoteIp = remoteAddress?.MapToIPv4().ToString();
        var isLoopback = remoteAddress is not null && System.Net.IPAddress.IsLoopback(remoteAddress);

        // IP real del equipo conectado; solo se cae al ?ip= cuando es loopback o no hay IP remota.
        var ipAddress = (!string.IsNullOrWhiteSpace(remoteIp) && !isLoopback)
            ? remoteIp
            : (ip?.Trim() ?? remoteIp ?? string.Empty);

        // MAC y nombre REALES resueltos en el servidor desde la IP (ARP + DNS inverso).
        // Si la red no los resuelve, se usa lo que reporta el propio equipo.
        var resolvedMac = MacAddressResolver.TryResolve(ipAddress);
        var resolvedName = await MacAddressResolver.TryResolveHostNameAsync(ipAddress);

        var macAddress = !string.IsNullOrWhiteSpace(resolvedMac) ? resolvedMac : (mac?.Trim() ?? string.Empty);
        var deviceName = !string.IsNullOrWhiteSpace(resolvedName) ? resolvedName : (device?.Trim() ?? string.Empty);

        var result = await gasStationService.CheckDeviceAuthorizationAsync(
            ipAddress,
            macAddress,
            deviceName,
            cancellationToken);
        return Ok(result);
    }
}
