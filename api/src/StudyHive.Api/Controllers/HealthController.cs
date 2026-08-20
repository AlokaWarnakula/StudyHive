using Microsoft.AspNetCore.Mvc;

namespace StudyHive.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new { status = "healthy", timestampUtc = DateTimeOffset.UtcNow });
}
