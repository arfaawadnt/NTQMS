using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelaxAuditRlsWriteCheck : Migration
    {
        // The audit.* ledgers are trusted, append-only, and written only by the
        // interceptors / infrastructure. Pre-auth and platform events legitimately
        // have no tenant (e.g. a failed login modifies the non-tenant-scoped
        // user_account, producing a null-tenant field_change row). The strict
        // WITH CHECK from ActivateForcedTenantRls rejected those, breaking failed
        // logins and other pre-auth user changes. Allow a NULL tenant on write for
        // the audit schema only, while still blocking a request from forging an
        // audit row tagged to a DIFFERENT (non-null) tenant. USING is unchanged, so
        // null-tenant rows remain invisible to ordinary tenant reads (platform-only).
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $rls$
                DECLARE r record;
                BEGIN
                    FOR r IN
                        SELECT schemaname, tablename FROM pg_policies
                        WHERE policyname = 'tenant_isolation' AND schemaname = 'audit'
                    LOOP
                        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', r.schemaname, r.tablename);
                        EXECUTE format($pol$
                            CREATE POLICY tenant_isolation ON %I.%I
                            USING (
                                tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                                OR current_setting('app.bypass_rls', true) = 'on'
                            )
                            WITH CHECK (
                                tenant_id IS NULL
                                OR tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                                OR current_setting('app.bypass_rls', true) = 'on'
                            )
                        $pol$, r.schemaname, r.tablename);
                    END LOOP;
                END
                $rls$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $rls$
                DECLARE r record;
                BEGIN
                    FOR r IN
                        SELECT schemaname, tablename FROM pg_policies
                        WHERE policyname = 'tenant_isolation' AND schemaname = 'audit'
                    LOOP
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
    }
}
