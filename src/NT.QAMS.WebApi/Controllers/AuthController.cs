using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NT.QAMS.Application.IdentityAccess.Commands;
using NT.QAMS.Contracts.IdentityAccess;
using NT.QAMS.WebApi.Security;

namespace NT.QAMS.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
// SEC-013: the credential surface gets the strict per-client budget — a burst
// of attempts here is credential guessing, not a workload.
[EnableRateLimiting(RateLimiting.AuthPolicy)]
public sealed class AuthController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Refresh cookie name and its constant attributes (ADR-0009): httpOnly so
    /// script can never read it, Secure + SameSite=Strict so it is CSRF-inert,
    /// and Path-scoped to the refresh endpoint so it rides no other request.
    /// </summary>
    private const string RefreshCookieName = "qams_rt";
    private const string RefreshCookiePath = "/api/auth";

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new LoginCommand(
            request.TenantIdentifier, request.Email, request.Password, request.MfaCode), ct);
        return AuthResult(result);
    }

    /// <summary>
    /// Silent refresh (ADR-0009): reads the httpOnly refresh cookie, rotates
    /// it, and returns a fresh access token. Anonymous — the cookie IS the
    /// credential; the access token is expected to be expired here.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimiting.RefreshPolicy)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var presented = Request.Cookies[RefreshCookieName];
        var result = await sender.Send(new RefreshTokenCommand(presented), ct);
        return AuthResult(result);
    }

    /// <summary>Revokes the refresh family server-side and clears the cookie.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await sender.Send(new LogoutCommand(Request.Cookies[RefreshCookieName]), ct);
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });
        return NoContent();
    }

    /// <summary>
    /// Emits the access-token body and, when the outcome carries a refresh
    /// grant, sets the hardened cookie. The token secret never enters the body.
    /// </summary>
    private IActionResult AuthResult(LoginResult result)
    {
        if (result.Refresh is { } grant)
        {
            Response.Cookies.Append(RefreshCookieName, grant.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = RefreshCookiePath,
                Expires = grant.ExpiresAtUtc,
                IsEssential = true,
            });
        }

        return Ok(result.Response);
    }

    /// <summary>
    /// Self-service password rotation — anonymous by design so an EXPIRED
    /// password can still be changed; the handler verifies full credentials.
    /// </summary>
    [HttpPost("change-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await sender.Send(new ChangePasswordCommand(
            request.TenantIdentifier, request.Email, request.CurrentPassword, request.NewPassword), ct);
        return NoContent();
    }

    /// <summary>Begin MFA enrollment — returns the secret + otpauth URI for the authenticator app.</summary>
    [HttpPost("mfa/enroll")]
    [Authorize]
    public async Task<IActionResult> EnrollMfa(CancellationToken ct) =>
        Ok(await sender.Send(new EnrollMfaCommand(), ct));

    [HttpPost("mfa/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmMfa(ConfirmMfaRequest request, CancellationToken ct)
    {
        await sender.Send(new ConfirmMfaCommand(request.Code), ct);
        return NoContent();
    }

    /// <summary>Set the caller's 4-digit electronic-signature PIN.</summary>
    [HttpPost("signature-pin")]
    [Authorize]
    public async Task<IActionResult> SetPin(SetPinRequest request, CancellationToken ct)
    {
        await sender.Send(new SetPinCommand(request.Pin), ct);
        return NoContent();
    }
}
