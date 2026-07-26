using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NT.QAMS.Application.Abstractions;

namespace NT.QAMS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Layer-2 tenant isolation: stamps the PostgreSQL session GUCs that the
/// <c>tenant_isolation</c> Row-Level-Security policies read, on <b>every</b>
/// connection open (so a pooled physical connection can never carry another
/// request's tenant). Fail-closed: with no resolved tenant and no elevation the
/// tenant GUC is the nil UUID, which matches no row.
///
/// <para>Normal request: <c>app.current_tenant</c> = the JWT tenant, bypass off.
/// Trusted cross-tenant infrastructure (provisioning, outbox, sweeps) sets
/// <c>ICurrentTenantSetter.Elevate()</c>, which turns <c>app.bypass_rls</c> on
/// for that unit of work only.</para>
/// </summary>
public sealed class TenantConnectionInterceptor(ICurrentTenant currentTenant) : DbConnectionInterceptor
{
    private static readonly string NilTenant = Guid.Empty.ToString();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Apply(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void Apply(DbConnection connection)
    {
        using var cmd = CreateCommand(connection);
        cmd.ExecuteNonQuery();
    }

    private async Task ApplyAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = CreateCommand(connection);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var cmd = connection.CreateCommand();
        // set_config(name, value, is_local=false) — session scope, re-applied on
        // every open. Parameterized: the tenant value is a Guid, never user text.
        cmd.CommandText =
            "SELECT set_config('app.current_tenant', @tenant, false), set_config('app.bypass_rls', @bypass, false)";
        AddParam(cmd, "tenant", currentTenant.TenantId?.ToString() ?? NilTenant);
        AddParam(cmd, "bypass", currentTenant.IsElevated ? "on" : "off");
        return cmd;
    }

    private static void AddParam(DbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
