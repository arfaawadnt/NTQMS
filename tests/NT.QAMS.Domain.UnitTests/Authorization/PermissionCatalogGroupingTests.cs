using System.Linq;
using FluentAssertions;
using NT.QAMS.Domain.Authorization;
using Xunit;

namespace NT.QAMS.Domain.UnitTests.Authorization;

/// <summary>
/// N-10: the hospital clinical-governance modules must group under their own
/// catalogue section so the privilege matrix reads like the shell navigation
/// (Clinical Governance), rather than being scattered inside Quality &amp;
/// improvement. The privilege matrix renders one block per <see cref="PermissionModule.Group"/>,
/// so the grouping is a UI contract, not cosmetic.
/// </summary>
public sealed class PermissionCatalogGroupingTests
{
    private static readonly string[] ClinicalModules =
    [
        PermissionCatalog.PatientSafety,
        PermissionCatalog.InfectionControl,
        PermissionCatalog.MortalityReview,
        PermissionCatalog.Credentialing,
        PermissionCatalog.EnvironmentOfCare,
    ];

    [Fact]
    public void ClinicalModules_group_under_GroupClinical()
    {
        foreach (var key in ClinicalModules)
        {
            var module = PermissionCatalog.Modules.Single(m => m.Key == key);
            module.Group.Should().Be(
                PermissionCatalog.GroupClinical,
                "clinical-governance module '{0}' must render in the Clinical section of the privilege matrix",
                key);
        }
    }

    [Fact]
    public void GroupClinical_holds_exactly_the_clinical_modules()
    {
        var members = PermissionCatalog.Modules
            .Where(m => m.Group == PermissionCatalog.GroupClinical)
            .Select(m => m.Key)
            .OrderBy(k => k);

        members.Should().BeEquivalentTo(ClinicalModules.OrderBy(k => k));
    }
}
