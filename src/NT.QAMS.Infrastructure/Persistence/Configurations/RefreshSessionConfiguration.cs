using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NT.QAMS.Domain.IdentityAccess;

namespace NT.QAMS.Infrastructure.Persistence.Configurations;

public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        // Like user_account: deliberately not tenant-scoped — the session is
        // bound to the user; possession of the token is the access control.
        builder.ToTable("refresh_session", "qams");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TokenHash).HasMaxLength(64); // SHA-256 hex

        builder.HasIndex(s => s.FamilyId).HasDatabaseName("ix_refresh_session_family");
        builder.HasIndex(s => s.UserId).HasDatabaseName("ix_refresh_session_user");
        // The retention purge's scan path.
        builder.HasIndex(s => s.ExpiresAtUtc).HasDatabaseName("ix_refresh_session_expires");
    }
}
