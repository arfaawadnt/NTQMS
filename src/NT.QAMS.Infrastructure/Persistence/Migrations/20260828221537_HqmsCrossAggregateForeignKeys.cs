using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HqmsCrossAggregateForeignKeys : Migration
    {
        // Audit finding M-08: the HQMS train gave every owned child a
        // tenant-composite FK but left its cross-aggregate references bare
        // columns. Same idiom as Hardening4_ChildTenancy: tenant-composite
        // (tenant_id, ref) → parent (tenant_id, id), so a reference into
        // another tenant's aggregate — or into nothing — is structurally
        // impossible. RESTRICT, not CASCADE: deleting a referenced aggregate
        // out from under a live reference must fail loudly, and none of these
        // parents has a hard-delete path anyway. Two targets (survey_question,
        // standard_element) are owned children EF cannot address with HasOne,
        // which is why this lives as SQL rather than in the configurations.
        // NOT VALID → VALIDATE per the house migration-safety idiom; FK
        // integrity checks are exempt from RLS, and app.bypass_rls is set per
        // the standing migration rule (Trap 1) for consistency.
        private static readonly (string Table, string Name, string Columns, string Parent)[] ForeignKeys =
        [
            ("meeting", "fk_meeting_committee_tenant",
                "tenant_id, committee_id", "qams.committee (tenant_id, id)"),
            ("survey_response", "fk_survey_response_satisfaction_survey_tenant",
                "tenant_id, survey_id", "qams.satisfaction_survey (tenant_id, id)"),
            ("survey_response", "fk_survey_response_department_tenant",
                "tenant_id, department_id", "qams.department (tenant_id, id)"),
            ("survey_answer", "fk_survey_answer_survey_question_tenant",
                "tenant_id, question_id", "qams.survey_question (tenant_id, id)"),
            ("evidence_link", "fk_evidence_link_standard_set_tenant",
                "tenant_id, standard_set_id", "qams.standard_set (tenant_id, id)"),
            ("evidence_link", "fk_evidence_link_standard_element_tenant",
                "tenant_id, element_id", "qams.standard_element (tenant_id, id)"),
            ("planned_audit", "fk_planned_audit_audit_tenant",
                "tenant_id, scheduled_audit_id", "qams.audit (tenant_id, id)"),
            ("integration_message", "fk_integration_message_integration_endpoint_tenant",
                "tenant_id, endpoint_id", "qams.integration_endpoint (tenant_id, id)"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL app.bypass_rls = 'on';");
            foreach (var (table, name, columns, parent) in ForeignKeys)
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE qams.{table} ADD CONSTRAINT {name}
                        FOREIGN KEY ({columns}) REFERENCES {parent}
                        ON DELETE RESTRICT NOT VALID;
                    ALTER TABLE qams.{table} VALIDATE CONSTRAINT {name};
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, name, _, _) in ForeignKeys)
            {
                migrationBuilder.Sql($"ALTER TABLE qams.{table} DROP CONSTRAINT IF EXISTS {name};");
            }
        }
    }
}
