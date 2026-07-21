using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.DocumentControl;
using NT.QAMS.Domain.Files;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Upload/download for controlled-document files and evidence attachments.
/// Content-addressed, immutable, tenant-scoped (query filter + storage path).
/// </summary>
[ApiController]
[Route("api/files")]
[Authorize]
public sealed class FilesController(IAppDbContext db, IFileStorage storage, ICurrentTenant tenant)
    : ControllerBase
{
    public const long MaxUploadBytes = 50 * 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(FileUploadedDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return ValidationProblem("A non-empty file is required.");
        }

        var tenantId = tenant.TenantId
            ?? throw new SharedKernel.Primitives.DomainException("TENANT-000", "A tenant context is required.");

        await using var content = file.OpenReadStream();
        var (sha, size, key) = await storage.SaveAsync(tenantId, content, ct);

        var reference = FileReference.Register(file.FileName, file.ContentType, sha, size, key);
        db.Files.Add(reference);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Download), new { id = reference.Id },
            new FileUploadedDto(reference.Id, reference.FileName, reference.Sha256, reference.SizeBytes));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var reference = await db.Files.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id, ct);
        if (reference is null)
        {
            return NotFound();
        }

        var stream = await storage.OpenReadAsync(reference.StorageKey, ct);
        return File(stream, reference.ContentType, reference.FileName);
    }
}
