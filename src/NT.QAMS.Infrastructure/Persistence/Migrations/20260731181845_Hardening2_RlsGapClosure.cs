using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Hardening2_RlsGapClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // audit.security_event - the known gap (design doc "honest register"):
            // append-only trigger, no RLS. Same policy family as the three sibling
            // ledgers after RelaxAuditRlsWriteCheck: reads are tenant-scoped;
            // writes allow the pre-authentication null-tenant case (failed logins).
            migrationBuilder.Sql("""
ALTER TABLE audit.security_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit.security_event FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON audit.security_event;
CREATE POLICY tenant_isolation ON audit.security_event
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id IS NULL
         OR tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
""");

            // qams.ref_counter - discovered parity violation: tenant_id NOT NULL,
            // written by raw SQL on the tenant connection (GUCs set), no RLS.
            // Standard tenant policy; no null allowance (the column is NOT NULL).
            migrationBuilder.Sql("""
ALTER TABLE qams.ref_counter ENABLE ROW LEVEL SECURITY;
ALTER TABLE qams.ref_counter FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.ref_counter;
CREATE POLICY tenant_isolation ON qams.ref_counter
  FOR ALL
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on')
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
         OR current_setting('app.bypass_rls', true) = 'on');
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DROP POLICY IF EXISTS tenant_isolation ON audit.security_event;
ALTER TABLE audit.security_event NO FORCE ROW LEVEL SECURITY;
ALTER TABLE audit.security_event DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON qams.ref_counter;
ALTER TABLE qams.ref_counter NO FORCE ROW LEVEL SECURITY;
ALTER TABLE qams.ref_counter DISABLE ROW LEVEL SECURITY;
""");
        }
    }
}
