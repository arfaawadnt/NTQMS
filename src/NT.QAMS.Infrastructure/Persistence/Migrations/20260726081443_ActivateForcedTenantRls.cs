using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivateForcedTenantRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Layer-2 tenant isolation, activated. Until now the tenant_isolation
            // policies existed but were dormant (the runtime owns the tables, so
            // RLS did not apply, and the app never set app.current_tenant). This
            // migration makes them ENFORCING for EVERY role via FORCE ROW LEVEL
            // SECURITY, and rewrites each policy to read the per-connection GUCs
            // that TenantConnectionInterceptor now stamps on every open:
            //   * app.current_tenant — the request's JWT tenant (nil => matches nothing, fail-closed)
            //   * app.bypass_rls     — 'on' only for trusted cross-tenant infrastructure
            // The set of tables is derived from the existing tenant_isolation
            // policies, so it tracks ITenantScoped exactly and can never touch the
            // deliberately-unscoped login table (user_account) or the control plane.
            migrationBuilder.Sql(@"
                DO $rls$
                DECLARE r record;
                BEGIN
                    FOR r IN
                        SELECT schemaname, tablename FROM pg_policies WHERE policyname = 'tenant_isolation'
                    LOOP
                        EXECUTE format('ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY', r.schemaname, r.tablename);
                        EXECUTE format('ALTER TABLE %I.%I FORCE ROW LEVEL SECURITY', r.schemaname, r.tablename);
                        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', r.schemaname, r.tablename);
                        EXECUTE format($pol$
                            CREATE POLICY tenant_isolation ON %I.%I
                            USING (
                                tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                                OR current_setting('app.bypass_rls', true) = 'on'
                            )
                            WITH CHECK (
                                tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                                OR current_setting('app.bypass_rls', true) = 'on'
                            )
                        $pol$, r.schemaname, r.tablename);
                    END LOOP;
                END
                $rls$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to the dormant state: drop FORCE and restore the original
            // read-only, USING-only policy without the bypass clause.
            migrationBuilder.Sql(@"
                DO $rls$
                DECLARE r record;
                BEGIN
                    FOR r IN
                        SELECT schemaname, tablename FROM pg_policies WHERE policyname = 'tenant_isolation'
                    LOOP
                        EXECUTE format('ALTER TABLE %I.%I NO FORCE ROW LEVEL SECURITY', r.schemaname, r.tablename);
                        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', r.schemaname, r.tablename);
                        EXECUTE format($pol$
                            CREATE POLICY tenant_isolation ON %I.%I
                            USING (tenant_id = current_setting('app.current_tenant', true)::uuid)
                        $pol$, r.schemaname, r.tablename);
                    END LOOP;
                END
                $rls$;
            ");
        }
    }
}
