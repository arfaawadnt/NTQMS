using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace NT.QAMS.Architecture.Tests;

/// <summary>
/// Phase-6 finding ARCH-004: the modular-monolith boundary, executable. A
/// domain module may not reference another module's types — cross-module
/// relationships travel by Id (and integration happens via events in the
/// Application layer), exactly as the domain model prescribes. A violation
/// fails the pipeline, not code review.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly Assembly Domain = typeof(NT.QAMS.Domain.Tenancy.Tenant).Assembly;

    /// <summary>
    /// The bounded contexts under NT.QAMS.Domain — laboratory originals plus the
    /// HQMS hospital contexts. <see cref="ModuleListExhaustivenessTests"/> fails
    /// the build if a context exists in the assembly but not here.
    /// </summary>
    internal static readonly string[] Modules =
    [
        "AnalyticalQuality", "AuditManagement", "Competency", "ComplianceLedger",
        "DocumentControl", "Equipment", "Facility", "Files", "IdentityAccess",
        "Improvement", "Notifications", "Organization", "Records", "Reporting",
        "RiskGovernance", "Sla", "SupplierQuality", "Tenancy",
        // HQMS hospital contexts (feature/hqms-hospital-modules)
        "Accreditation", "Committees", "Credentialing", "EnvironmentOfCare",
        "IncidentReporting", "InfectionControl", "Integration", "MortalityReview",
        "PatientExperience", "PatientSafety", "QualityIndicators", "TrainingManagement",
    ];

    public static TheoryData<string> ModuleNames()
    {
        var data = new TheoryData<string>();
        foreach (var module in Modules)
        {
            data.Add(module);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void A_domain_module_references_no_other_domain_module(string module)
    {
        var otherModules = Modules
            .Where(m => m != module)
            .Select(m => $"NT.QAMS.Domain.{m}")
            .ToArray();

        var result = Types.InAssembly(Domain)
            .That().ResideInNamespaceStartingWith($"NT.QAMS.Domain.{module}")
            .ShouldNot().HaveDependencyOnAny(otherModules)
            .GetResult();

        (result.FailingTypeNames ?? []).Should().BeEmpty(
            $"module '{module}' must reference other modules by Id only — " +
            "cross-module coupling breaks the modular monolith");
    }
}
