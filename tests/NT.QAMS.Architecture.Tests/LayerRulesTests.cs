using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace NT.QAMS.Architecture.Tests;

/// <summary>
/// The dependency rules from the application architecture, executable.
/// A boundary violation is a build/CI failure, not a code-review remark.
/// </summary>
public class LayerRulesTests
{
    private static readonly Assembly SharedKernel = typeof(SharedKernel.Primitives.Entity).Assembly;
    private static readonly Assembly Domain = typeof(Domain.Tenancy.Tenant).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly Contracts = typeof(Contracts.Tenancy.TenantDto).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void SharedKernel_references_nothing_of_ours_and_no_frameworks()
    {
        var forbidden = new[]
        {
            "NT.QAMS.Domain", "NT.QAMS.Application", "NT.QAMS.Contracts",
            "NT.QAMS.Infrastructure", "NT.QAMS.WebApi",
            "MediatR", "FluentValidation", "Microsoft.EntityFrameworkCore",
        };

        SharedKernel.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Should().NotContain(name => forbidden.Contains(name));
    }

    [Fact]
    public void Domain_depends_only_on_SharedKernel()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "NT.QAMS.Application", "NT.QAMS.Infrastructure", "NT.QAMS.WebApi",
                "NT.QAMS.Contracts",
                "MediatR", "FluentValidation", "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Application_never_references_Infrastructure_or_AspNetCore()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("NT.QAMS.Infrastructure", "NT.QAMS.WebApi", "Microsoft.AspNetCore")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Contracts_carry_no_domain_types()
    {
        var result = Types.InAssembly(Contracts)
            .ShouldNot()
            .HaveDependencyOnAny("NT.QAMS.Domain", "NT.QAMS.Application", "NT.QAMS.Infrastructure")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Infrastructure_never_references_WebApi()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny("NT.QAMS.WebApi")
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }
}
