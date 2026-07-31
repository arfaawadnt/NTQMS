using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Authorization;

/// <summary>
/// Bridges the tier-based user-administration contract onto configurable roles:
/// when a caller names a fixed tier instead of a role, the user is placed on the
/// seeded role that reproduces that tier. Kept as its own seam so both
/// registration and tier changes behave identically.
/// </summary>
internal static class SeededRoleDefault
{
    /// <summary>Assigns the seeded role equivalent to <paramref name="tier"/>, or throws if the tenant lost it.</summary>
    public static async Task AssignAsync(IAppDbContext db, UserAccount user, UserRole tier, CancellationToken ct)
    {
        var name = SystemRoleCatalog.RoleNameFor(tier);
        var normalized = name.ToUpperInvariant();
        var role = await db.Roles
            .Where(r => r.NormalizedName == normalized && r.IsActive)
            .Select(r => new { r.Id })
            .SingleOrDefaultAsync(ct)
            ?? throw new DomainException("ROLE-009",
                $"The seeded role '{name}' is not available in this workspace; assign a role explicitly.");

        user.AssignRole(role.Id);
    }
}
