using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Hardening6_DeferrableTenantFks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 4 added these five FKs to saas.tenant in raw SQL, so EF has no
            // model relationship for them - and therefore no reason to order the
            // tenant INSERT before rows that reference it. Provisioning a tenant
            // writes the tenant, its admin and its outbox events in ONE
            // SaveChanges; EF emitted the outbox insert first and PostgreSQL
            // rejected it (23503), breaking tenant provisioning outright.
            //
            // Deferring the check to COMMIT keeps the integrity guarantee exactly
            // as strong - a transaction still cannot commit a row pointing at a
            // non-existent tenant - while making intra-transaction ordering
            // irrelevant. Preferred over modelling the relationship in EF, which
            // would drag infrastructure tables (outbox, counters, read models)
            // into the domain model.
            migrationBuilder.Sql("""
ALTER TABLE qams.outbox_event DROP CONSTRAINT fk_outbox_event_tenant;
ALTER TABLE qams.outbox_event ADD CONSTRAINT fk_outbox_event_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE qams.ref_counter DROP CONSTRAINT fk_ref_counter_tenant;
ALTER TABLE qams.ref_counter ADD CONSTRAINT fk_ref_counter_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE read.kpi_snapshot DROP CONSTRAINT fk_kpi_snapshot_tenant;
ALTER TABLE read.kpi_snapshot ADD CONSTRAINT fk_kpi_snapshot_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE qams.branch DROP CONSTRAINT fk_branch_tenant;
ALTER TABLE qams.branch ADD CONSTRAINT fk_branch_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE qams.user_account DROP CONSTRAINT fk_user_account_tenant;
ALTER TABLE qams.user_account ADD CONSTRAINT fk_user_account_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
ALTER TABLE qams.outbox_event DROP CONSTRAINT fk_outbox_event_tenant;
ALTER TABLE qams.outbox_event ADD CONSTRAINT fk_outbox_event_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT;
ALTER TABLE qams.ref_counter DROP CONSTRAINT fk_ref_counter_tenant;
ALTER TABLE qams.ref_counter ADD CONSTRAINT fk_ref_counter_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT;
ALTER TABLE read.kpi_snapshot DROP CONSTRAINT fk_kpi_snapshot_tenant;
ALTER TABLE read.kpi_snapshot ADD CONSTRAINT fk_kpi_snapshot_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT;
ALTER TABLE qams.branch DROP CONSTRAINT fk_branch_tenant;
ALTER TABLE qams.branch ADD CONSTRAINT fk_branch_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT;
ALTER TABLE qams.user_account DROP CONSTRAINT fk_user_account_tenant;
ALTER TABLE qams.user_account ADD CONSTRAINT fk_user_account_tenant FOREIGN KEY (tenant_id)
  REFERENCES saas.tenant (id) ON DELETE RESTRICT;
""");
        }
    }
}
