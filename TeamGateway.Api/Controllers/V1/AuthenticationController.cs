using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TeamGateway.Api.Authentication;
using TeamGateway.Api.Options;

namespace TeamGateway.Api.Controllers;

[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1.0")]
[ApiController]
public sealed class AuthenticationController : ControllerBase
{
    private readonly IAntiforgery _antiforgery;
    private readonly GatewayAntiforgeryOptions _antiforgeryOptions;
    private readonly IConfiguration _configuration;

    public AuthenticationController(
        IAntiforgery antiforgery,
        IOptions<GatewayAntiforgeryOptions> antiforgeryOptions,
        IConfiguration configuration)
    {
        _antiforgery = antiforgery;
        _antiforgeryOptions = antiforgeryOptions.Value;
        _configuration = configuration;
    }

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = "/")
    {
        var safeReturnUrl = IsLocalUrl(returnUrl) ? returnUrl! : "/";
        var frontendBaseUrl = _configuration["FrontendBaseUrl"]
            ?? throw new InvalidOperationException("FrontendBaseUrl is required.");

        return Challenge(new AuthenticationProperties
        {
            RedirectUri = $"{frontendBaseUrl.TrimEnd('/')}{safeReturnUrl}"
        }, AuthenticationSchemes.Keycloak);
    }

    [HttpGet("access-denied")]
    public IActionResult AccessDenied() => Forbid();

    [Authorize]
    [HttpGet("csrf")]
    public IActionResult GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append(
            _antiforgeryOptions.RequestTokenCookieName,
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,
                Path = _antiforgeryOptions.Path,
                SameSite = Enum.Parse<SameSiteMode>(_antiforgeryOptions.SameSite, true),
                Secure = Enum.Parse<CookieSecurePolicy>(_antiforgeryOptions.SecurePolicy, true)
                    == CookieSecurePolicy.Always
            });

        return NoContent();
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthenticationSchemes.ApplicationCookie);
        return NoContent();
    }

    private static bool IsLocalUrl(string? url) => !string.IsNullOrWhiteSpace(url)
        && url[0] == '/'
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
}
