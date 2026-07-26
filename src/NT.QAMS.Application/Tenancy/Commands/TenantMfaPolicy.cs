using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Tenancy.Commands;

/// <summary>Read the current tenant's privileged-MFA enforcement setting (F-04).</summary>
public sealed record GetTenantMfaPolicyQuery : IQuery<bool>;

/// <summary>Turn enforced MFA for the current tenant's privileged users on or off (F-04).</summary>
public sealed record SetTenantMfaPolicyCommand(bool Require) : ICommand;

public sealed class GetTenantMfaPolicyHandler(IAppDbContext db, ICurrentTenant tenant)
    : IQueryHandler<GetTenantMfaPolicyQuery, bool>
{
    public async Task<bool> Handle(GetTenantMfaPolicyQuery query, CancellationToken ct)
    {
        var id = tenant.TenantId ?? throw new DomainException("TENANT-000", "No tenant in context.");
        var row = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new DomainException("TENANT-404", "Tenant not found.");
        return row.Settings.RequireMfaForPrivilegedRoles;
    }
}

public sealed class SetTenantMfaPolicyHandler(IAppDbContext db, ICurrentTenant tenant)
    : ICommandHandler<SetTenantMfaPolicyCommand>
{
    public async Task Handle(SetTenantMfaPolicyCommand command, CancellationToken ct)
    {
        var id = tenant.TenantId ?? throw new DomainException("TENANT-000", "No tenant in context.");
        var row = await db.Tenants.SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new DomainException("TENANT-404", "Tenant not found.");
        row.SetPrivilegedMfaPolicy(command.Require);
        await db.SaveChangesAsync(ct);
    }
}
