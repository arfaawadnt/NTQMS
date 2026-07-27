using NT.QAMS.Application.Abstractions;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Application.UnitTests;

public sealed class FakeCurrentTenant : ICurrentTenant
{
    public Guid? TenantId { get; set; }
    public bool IsResolved => TenantId.HasValue;
    public bool IsElevated { get; set; }
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; } = Guid.CreateVersion7();
    public string? DisplayName { get; set; } = "test-user";
    public bool IsAuthenticated => UserId.HasValue;
    public NT.QAMS.Domain.IdentityAccess.UserRole? Role { get; set; } =
        NT.QAMS.Domain.IdentityAccess.UserRole.QualityManager;
}

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}

public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";
    public bool Verify(string hash, string password) => hash == $"hashed:{password}";
}

public sealed class FakeRefGenerator : IReferenceNumberGenerator
{
    private long _value;
    public Task<string> NextAsync(Guid tenantId, string refType, CancellationToken ct) =>
        Task.FromResult($"{refType}-2026-{++_value:0000}");
}
