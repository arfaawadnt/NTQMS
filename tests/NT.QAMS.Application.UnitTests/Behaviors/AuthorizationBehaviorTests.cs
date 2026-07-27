using FluentAssertions;
using MediatR;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Behaviors;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Behaviors;

/// <summary>
/// Phase-4 finding CQRS-003: the authorization behavior is deny-by-default —
/// a command without a policy marker is refused outright, the read-only
/// external auditor cannot execute write commands, and role-listed commands
/// admit exactly their listed roles.
/// </summary>
public class AuthorizationBehaviorTests
{
    private sealed record UnannotatedCommand : ICommand;

    [RequireInternalActor]
    private sealed record WriteCommand : ICommand;

    [RequireAuthenticatedActor]
    private sealed record SelfServiceCommand : ICommand;

    [RequireRole(UserRole.PlatformAdmin)]
    private sealed record PlatformCommand : ICommand;

    [AllowUnauthenticated]
    private sealed record OpenCommand : ICommand;

    private static Task<Unit> RunAsync<TCommand>(FakeCurrentUser user)
        where TCommand : ICommand, new() =>
        new AuthorizationBehavior<TCommand, Unit>(user)
            .Handle(new TCommand(), () => Task.FromResult(Unit.Value), CancellationToken.None);

    private static FakeCurrentUser Actor(UserRole role) => new() { Role = role };

    private static FakeCurrentUser Anonymous() => new() { UserId = null, Role = null };

    [Fact]
    public async Task A_command_without_a_policy_is_denied_outright()
    {
        var refusal = await Assert.ThrowsAsync<DomainException>(
            () => RunAsync<UnannotatedCommand>(Actor(UserRole.TenantAdmin)));

        refusal.Code.Should().Be("AUTHZ-000", "fail closed: no policy means no execution, even for admins");
    }

    [Fact]
    public async Task The_external_auditor_cannot_execute_write_commands()
    {
        var refusal = await Assert.ThrowsAsync<DomainException>(
            () => RunAsync<WriteCommand>(Actor(UserRole.ExternalAuditor)));

        refusal.Code.Should().Be("AUTHZ-002", "auditors read the quality ledger, they never mutate it");
    }

    [Theory]
    [InlineData(UserRole.TenantAdmin)]
    [InlineData(UserRole.QualityManager)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Analyst)]
    [InlineData(UserRole.PlatformAdmin)]
    public async Task Internal_actors_execute_write_commands(UserRole role)
    {
        var act = () => RunAsync<WriteCommand>(Actor(role));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused_before_any_role_check()
    {
        var refusal = await Assert.ThrowsAsync<DomainException>(
            () => RunAsync<WriteCommand>(Anonymous()));

        refusal.Code.Should().Be("AUTHZ-001");
    }

    [Fact]
    public async Task Self_service_commands_admit_every_authenticated_role_including_the_auditor()
    {
        var act = () => RunAsync<SelfServiceCommand>(Actor(UserRole.ExternalAuditor));

        await act.Should().NotThrowAsync("auditors must be able to enroll MFA / set their PIN");
    }

    [Fact]
    public async Task Role_listed_commands_admit_exactly_their_roles()
    {
        await ((Func<Task>)(() => RunAsync<PlatformCommand>(Actor(UserRole.PlatformAdmin))))
            .Should().NotThrowAsync();

        var refusal = await Assert.ThrowsAsync<DomainException>(
            () => RunAsync<PlatformCommand>(Actor(UserRole.TenantAdmin)));
        refusal.Code.Should().Be("AUTHZ-002");
    }

    [Fact]
    public async Task Open_commands_run_without_an_actor()
    {
        var act = () => RunAsync<OpenCommand>(Anonymous());

        await act.Should().NotThrowAsync("login must run before anyone is authenticated");
    }
}
