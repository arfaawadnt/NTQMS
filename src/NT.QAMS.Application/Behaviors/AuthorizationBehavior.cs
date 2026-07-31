using MediatR;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Behaviors;

/// <summary>
/// CQRS-003: application-layer authorization, deny-by-default. Every COMMAND
/// must carry a <see cref="CommandPolicyAttribute"/>:
/// <list type="bullet">
/// <item>no policy → AUTHZ-000 (fail closed — an unannotated command is a
/// programming error, not an open door);</item>
/// <item><see cref="AllowUnauthenticatedAttribute"/> → pass (the handler does
/// its own credential checks);</item>
/// <item><see cref="RequireInternalActorAttribute"/> → any authenticated role
/// except the read-only <see cref="UserRole.ExternalAuditor"/>;</item>
/// <item><see cref="RequireRoleAttribute"/> → listed roles only;</item>
/// <item><see cref="RequirePermissionPolicyAttribute"/> → actors whose
/// configured role grants the permission (the tenant decides who that is).</item>
/// </list>
/// Defense-in-depth under the HTTP <c>[Authorize]</c> gates: this layer holds
/// even if a controller forgets its attribute. Queries are not gated here —
/// read authorization stays at the controller (auditors must read).
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUser currentUser, IUserPrivileges privileges)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly CommandPolicyAttribute? Policy = (CommandPolicyAttribute?)Attribute
        .GetCustomAttribute(typeof(TRequest), typeof(CommandPolicyAttribute));

    private static readonly bool IsCommand =
        typeof(TRequest).GetInterfaces().Any(i =>
            i == typeof(ICommand) ||
            (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)));

    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!IsCommand)
        {
            return next();
        }

        switch (Policy)
        {
            case null:
                throw new DomainException("AUTHZ-000",
                    $"Command '{typeof(TRequest).Name}' declares no authorization policy — denied.");
            case AllowUnauthenticatedAttribute:
                return next();
        }

        if (!currentUser.IsAuthenticated || currentUser.Role is not { } role)
        {
            throw new DomainException("AUTHZ-001", "An authenticated actor is required for this action.");
        }

        if (Policy is RequirePermissionPolicyAttribute declared
            && !Domain.Authorization.PermissionCatalog.IsKnown(declared.PermissionKey))
        {
            // A key the catalogue does not know is a programming error; failing
            // loudly on every call beats denying quietly until someone notices.
            throw new DomainException("AUTHZ-008",
                $"Command '{typeof(TRequest).Name}' requires unknown permission '{declared.PermissionKey}'.");
        }

        var permitted = Policy switch
        {
            RequireAuthenticatedActorAttribute => true,
            RequireInternalActorAttribute => role != UserRole.ExternalAuditor,
            RequireRoleAttribute required => required.Roles.Contains(role),
            RequirePermissionPolicyAttribute permission => privileges.Has(permission.PermissionKey),
            _ => false,
        };

        if (!permitted)
        {
            throw new DomainException("AUTHZ-002",
                $"Role '{role}' is not permitted to execute this action.");
        }

        return next();
    }
}
