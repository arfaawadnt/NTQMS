using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Organization;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Domain.Tenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Tenancy.Commands;

/// <summary>
/// Control-plane command: provision a new tenant AND its initial tenant
/// administrator in one transaction. Returns the new tenant id.
/// </summary>
public sealed record ProvisionTenantCommand(
    string Identifier, string Name,
    string AdminEmail, string AdminDisplayName, string AdminPassword) : ICommand<Guid>;

public sealed class ProvisionTenantValidator : AbstractValidator<ProvisionTenantCommand>
{
    public ProvisionTenantValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(TenantSlug.MaxLength);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Tenant.MaxNameLength);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.AdminDisplayName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AdminPassword).NotEmpty().MinimumLength(12)
            .WithMessage("The initial administrator password must be at least 12 characters.");
    }
}

public sealed class ProvisionTenantHandler(IAppDbContext db, IPasswordHasher hasher)
    : ICommandHandler<ProvisionTenantCommand, Guid>
{
    public async Task<Guid> Handle(ProvisionTenantCommand command, CancellationToken cancellationToken)
    {
        var slug = TenantSlug.Create(command.Identifier);

        var slugTaken = await db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken);
        if (slugTaken)
        {
            throw new DomainException("TENANT-005", $"Tenant identifier '{slug}' is already in use.");
        }

        var tenant = Tenant.Provision(slug, command.Name);

        var admin = UserAccount.Create(
            tenant.Id,
            command.AdminEmail,
            command.AdminDisplayName,
            hasher.Hash(command.AdminPassword),
            UserRole.TenantAdmin);

        db.Tenants.Add(tenant);
        db.Users.Add(admin);

        // Starter list-of-values so every dropdown is usable on day one —
        // seeded in the SAME transaction as the tenant itself.
        await DefaultLovCatalog.SeedMissingAsync(db, tenant.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return tenant.Id;
    }
}
