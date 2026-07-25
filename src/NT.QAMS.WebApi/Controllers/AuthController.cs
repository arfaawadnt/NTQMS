using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.IdentityAccess.Commands;
using NT.QAMS.Contracts.IdentityAccess;

namespace NT.QAMS.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await sender.Send(new LoginCommand(
            request.TenantIdentifier, request.Email, request.Password, request.MfaCode), ct));

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
