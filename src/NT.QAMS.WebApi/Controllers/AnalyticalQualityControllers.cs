using NT.QAMS.Domain.Authorization;
using NT.QAMS.WebApi.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Contracts.AnalyticalQuality;

namespace NT.QAMS.WebApi.Controllers;

[ApiController]
[Route("api/qc")]
[Authorize]
public sealed class QualityControlController(ISender sender) : ControllerBase
{
    [HttpGet("profiles")]
    public async Task<IActionResult> Profiles(CancellationToken ct) =>
        Ok(await sender.Send(new GetQcProfilesQuery(), ct));

    [HttpPost("profiles")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Manage)]
    public async Task<IActionResult> CreateProfile(CreateQcProfileRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new CreateQcProfileCommand(
            request.Analyte, request.Instrument, request.ControlLot,
            request.TargetMean, request.TargetSd), ct) });

    [HttpPut("profiles/{id:guid}/targets")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Manage)]
    public async Task<IActionResult> UpdateTargets(Guid id, UpdateQcTargetsRequest request, CancellationToken ct)
    {
        await sender.Send(new UpdateQcTargetsCommand(id, request.TargetMean, request.TargetSd, request.Reason), ct);
        return NoContent();
    }

    [HttpGet("profiles/{id:guid}/runs")]
    public async Task<IActionResult> Runs(Guid id, [FromQuery] int take = 60, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetQcRunsQuery(id, take), ct));

    [HttpPost("profiles/{id:guid}/runs")]
    public async Task<IActionResult> RecordRun(Guid id, RecordQcRunRequest request, CancellationToken ct) =>
        Ok(new { runId = await sender.Send(new RecordQcRunCommand(id, request.Value, request.Operator), ct) });

    [HttpPost("runs/{runId:guid}/troubleshoot")]
    public async Task<IActionResult> Troubleshoot(Guid runId, QcTroubleshootRequest request, CancellationToken ct)
    {
        await sender.Send(new LogQcTroubleshootingCommand(runId, request.Note), ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/validation-studies")]
[Authorize]
public sealed class ValidationStudiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct) =>
        Ok(await sender.Send(new GetStudiesQuery(state), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetStudyByIdQuery(id), ct));

    /// <summary>Part 11 §11.50 signature manifest for this validation study, visible to any viewer of the record.</summary>
    [HttpGet("{id:guid}/signatures")]
    public async Task<IActionResult> Signatures(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new NT.QAMS.Application.ComplianceLedger.GetSignaturesForSubjectQuery($"MV:{id:N}"), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Create)]
    public async Task<IActionResult> Configure(ConfigureStudyRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ConfigureStudyCommand(
            request.Analyte, request.Protocol, request.TotalAllowableError), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPost("{id:guid}/replicates")]
    public async Task<IActionResult> EnterReplicate(Guid id, EnterReplicateRequest request, CancellationToken ct)
    {
        await sender.Send(new EnterReplicateCommand(id, request.Level, request.Measured, request.Reference), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken ct)
    {
        await sender.Send(new CalculateStudyCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/sign-off")]
    [RequirePermission(PermissionCatalog.AnalyticalQuality, PermissionAction.Sign)]
    public async Task<IActionResult> SignOff(Guid id, AnalyticalSignOffRequest request, CancellationToken ct)
    {
        await sender.Send(new SignOffStudyCommand(id, request.Password, request.Pin), ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/proficiency-tests")]
[Authorize]
public sealed class ProficiencyTestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? performance, CancellationToken ct) =>
        Ok(await sender.Send(new GetPtEnrollmentsQuery(performance), ct));

    [HttpPost]
    public async Task<IActionResult> Enroll(EnrollPtRequest request, CancellationToken ct) =>
        Ok(new { id = await sender.Send(new EnrollPtCommand(request.Scheme, request.Analyte, request.Cycle), ct) });

    [HttpPost("{id:guid}/result")]
    public async Task<IActionResult> RecordResult(Guid id, RecordPtResultRequest request, CancellationToken ct)
    {
        await sender.Send(new RecordPtResultCommand(id, request.Submitted, request.Assigned, request.StandardDeviation), ct);
        return NoContent();
    }
}
