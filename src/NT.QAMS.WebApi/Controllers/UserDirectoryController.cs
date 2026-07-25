using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.IdentityAccess.Commands;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// The tenant user directory for name pickers: every authenticated tenant user
/// may resolve colleague names (full user administration stays TenantAdmin-only
/// on UsersController).
/// </summary>
[ApiController]
[Route("api/users/directory")]
[Authorize]
public sealed class UserDirectoryController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Directory(CancellationToken ct) =>
        Ok(await sender.Send(new GetUserDirectoryQuery(), ct));
}
