using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlightManagement.Api.Controllers;

[ApiController]
[Route("api/security")]
[Authorize]
public sealed class SecurityController : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery) =>
        Ok(new { requestToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
}
