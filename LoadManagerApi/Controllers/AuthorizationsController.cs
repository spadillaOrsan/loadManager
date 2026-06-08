using LoadManagerApi.Interfaces;
using LoadManagerApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace LoadManagerApi.Controllers;

[ApiController]
[Route("api/authorizations")]
public sealed class AuthorizationsController(IGasStationService gasStationService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AuthorizationRegistrationResult>> RegisterAsync(
        FuelAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        var folio = await gasStationService.RegisterAuthorizationAsync(request, cancellationToken);
        return Ok(new AuthorizationRegistrationResult { Folio = folio });
    }
}
