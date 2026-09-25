using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeamGateway.Api.Constants;

namespace TeamGateway.Api.Controllers;

[Route("api/health-check")]
[ApiController]
[EnableRateLimiting(RateLimiterPolicies.Default)]
public class HealthCheckController : ControllerBase
{
    [HttpGet]
    public IActionResult Healthy()
    {
        return Ok("Healthy");
    }
}