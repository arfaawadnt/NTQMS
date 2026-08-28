using System.Reflection;
using FluentAssertions;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Authorization;
using Xunit;

namespace NT.QAMS.Architecture.Tests;

/// <summary>
/// Audit finding M-09: a workflow command whose controller action demands a
/// catalogue permission must demand the SAME permission at the command tier —
/// otherwise any internal actor dispatching outside the HTTP path (a policy,
/// another handler, a future endpoint) bypasses the tenant's privilege
/// configuration entirely. Each row mirrors the controller's
/// <c>[RequirePermission]</c> onto the command's policy attribute. The two
/// incident REPORT commands stay <c>[RequireInternalActor]</c> by design
/// (open safety-culture intake) and are deliberately absent here.
/// </summary>
public class WorkflowCommandPolicyTests
{
    public static TheoryData<Type, string, PermissionAction> Mirrors()
    {
        return new TheoryData<Type, string, PermissionAction>
        {
            // Incidents (HQMS M02) — mirrors IncidentsController.
            { typeof(Application.IncidentReporting.Commands.TriageIncidentCommand), PermissionCatalog.Incidents, PermissionAction.Approve },
            { typeof(Application.IncidentReporting.Commands.RejectIncidentCommand), PermissionCatalog.Incidents, PermissionAction.Void },
            { typeof(Application.IncidentReporting.Commands.StartIncidentInvestigationCommand), PermissionCatalog.Incidents, PermissionAction.Approve },
            { typeof(Application.IncidentReporting.Commands.AddContributingFactorCommand), PermissionCatalog.Incidents, PermissionAction.Edit },
            { typeof(Application.IncidentReporting.Commands.AddTimelineEntryCommand), PermissionCatalog.Incidents, PermissionAction.Edit },
            { typeof(Application.IncidentReporting.Commands.RecordInvestigationSummaryCommand), PermissionCatalog.Incidents, PermissionAction.Edit },
            { typeof(Application.IncidentReporting.Commands.SubmitIncidentForReviewCommand), PermissionCatalog.Incidents, PermissionAction.Approve },
            // Equipment downtime & recall register (HQMS M14).
            { typeof(Application.Equipment.StartDowntimeCommand), PermissionCatalog.Equipment, PermissionAction.Edit },
            { typeof(Application.Equipment.EndDowntimeCommand), PermissionCatalog.Equipment, PermissionAction.Edit },
            { typeof(Application.Equipment.LogSafetyNoticeCommand), PermissionCatalog.Equipment, PermissionAction.Edit },
            { typeof(Application.Equipment.ActionSafetyNoticeCommand), PermissionCatalog.Equipment, PermissionAction.Edit },
            { typeof(Application.Equipment.CloseSafetyNoticeCommand), PermissionCatalog.Equipment, PermissionAction.Void },
            // Supplier contracts & CAR loop (HQMS M16).
            { typeof(Application.SupplierQuality.AddContractCommand), PermissionCatalog.Suppliers, PermissionAction.Edit },
            { typeof(Application.SupplierQuality.TerminateContractCommand), PermissionCatalog.Suppliers, PermissionAction.Void },
            { typeof(Application.SupplierQuality.RaiseSupplierCarCommand), PermissionCatalog.Suppliers, PermissionAction.Edit },
            { typeof(Application.SupplierQuality.RecordCarResponseCommand), PermissionCatalog.Suppliers, PermissionAction.Edit },
            { typeof(Application.SupplierQuality.CloseSupplierCarCommand), PermissionCatalog.Suppliers, PermissionAction.Approve },
        };
    }

    [Theory]
    [MemberData(nameof(Mirrors))]
    public void A_gated_workflow_command_demands_the_same_permission_as_its_endpoint(
        Type command, string module, PermissionAction action)
    {
        var policy = command.GetCustomAttribute<RequirePermissionPolicyAttribute>(inherit: false);

        policy.Should().NotBeNull(
            $"{command.Name}'s endpoint demands {PermissionCatalog.Key(module, action)}, so the command tier "
            + "must demand it too — [RequireInternalActor] lets any internal actor dispatch it off the HTTP path");
        policy!.PermissionKey.Should().Be(PermissionCatalog.Key(module, action));
    }
}
