using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.QualityIndicators.Commands;
using NT.QAMS.Application.QualityIndicators.Queries;
using NT.QAMS.Contracts.QualityIndicators;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.QualityIndicators;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Quality Indicators &amp; KPIs API (HQMS M06). Governs indicator definitions (the data
/// dictionary that makes a number defensible), their targets/thresholds, the period
/// measurements collected against them, and the statistical-process-control view. An
/// action-threshold breach opens an analysis task server-side.
/// </summary>
[ApiController]
[Route("api/indicators")]
[Authorize]
public sealed class IndicatorsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCatalog.Indicators, PermissionAction.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await sender.Send(new GetIndicatorsQuery(status, search, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCatalog.Indicators, PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetIndicatorByIdQuery(id), ct));

    /// <summary>Statistical process control over the measurement series (control limits + special-cause flags).</summary>
    [HttpGet("{id:guid}/control-chart")]
    [RequirePermission(PermissionCatalog.Indicators, PermissionAction.View)]
    public async Task<IActionResult> ControlChart(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetIndicatorControlChartQuery(id), ct));

    [HttpPost]
    [RequirePermission(PermissionCatalog.Indicators, PermissionAction.Create)]
    public async Task<IActionResult> Define(DefineIndicatorRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new DefineIndicatorCommand(
            request.Code, request.Name, request.Description,
            request.Numerator, request.Denominator, request.Unit, request.RateFactor,
            RequestEnum.Parse<IndicatorFrequency>(request.Frequency),
            RequestEnum.Parse<IndicatorDirection>(request.Direction),
            request.Inclusions, request.Exclusions, request.DataSource), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCatalog.Indicators, PermissionAction.Edit)]
    public async Task<IActionResult> Update(Guid id, UpdateIndicatorDefinitionRequest request, CancellationToken ct)
    {
        await sender.Send(new UpdateIndicatorDefinitionCommand(
            id, request.Name, request.Description,
            request.Numerator, request.Denominator, request.Unit, request.RateFactor,
            RequestEnum.Parse<IndicatorFrequency>(request.Frequency),
            RequestEnum.Parse<IndicatorDirection>(request.Direction),
            request.Inclusions, request.Exclusions, request.DataSource), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/targets")]
    [RequirePermission(PermissionCatalog.Indicators, PermissionAction.Edit)]
    public async Task<IActionResult> SetTargets(Guid id, SetIndicatorTargetsRequest request, CancellationToken ct)
    {
        await sender.Send(new SetIndicatorTargetsCommand(
            id, request.Target, request.WarningThreshold, request.ActionThreshold), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/measurements")]
    [RequirePermission(PermissionCatalog.Indicators, PermissionAction.Create)]
    public async Task<IActionResult> RecordMeasurement(Guid id, RecordMeasurementRequest request, CancellationToken ct)
    {
        var measurementId = await sender.Send(new RecordMeasurementCommand(
            id, request.Period, request.Numerator, request.Denominator, request.Note), ct);
        return Ok(new { measurementId });
    }

    [HttpPost("{id:guid}/retire")]
    [RequirePermission(PermissionCatalog.Indicators, PermissionAction.Void)]
    public async Task<IActionResult> Retire(Guid id, CancellationToken ct)
    {
        await sender.Send(new RetireIndicatorCommand(id), ct);
        return NoContent();
    }
}
