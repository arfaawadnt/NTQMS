using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PinTruncatedForeignKeyNames : Migration
    {
        // Audit finding M-14: these five HQMS foreign keys shipped with EF's
        // silent 62-char mid-word truncation ("…tenant_id_trai"). The
        // configurations now pin readable names via HasConstraintName using the
        // CLAUDE.md §5 abbreviation map; this migration renames the existing
        // constraints in place. RENAME CONSTRAINT is metadata-only — no FK
        // revalidation, no table scan — where the scaffolded drop/re-add would
        // rescan every child table.
        private static readonly (string Table, string From, string To)[] Renames =
        [
            ("document_audience_department",
                "fk_document_audience_department_controlled_document_tenant_id_",
                "fk_doc_aud_dept_controlled_document_tenant_id_document_id"),
            ("equipment_safety_notice",
                "fk_equipment_safety_notice_equipment_item_tenant_id_equipment_",
                "fk_eq_safety_notice_equipment_item_tenant_id_equipment_id"),
            ("indicator_measurement",
                "fk_indicator_measurement_quality_indicator_tenant_id_indicator",
                "fk_ind_meas_quality_indicator_tenant_id_indicator_id"),
            ("practitioner_privilege",
                "fk_practitioner_privilege_practitioner_tenant_id_practitioner_",
                "fk_prac_priv_practitioner_tenant_id_practitioner_id"),
            ("training_session_attendance",
                "fk_training_session_attendance_training_session_tenant_id_trai",
                "fk_ts_attendance_training_session_tenant_id_session_id"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, from, to) in Renames)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE qams.{table} RENAME CONSTRAINT \"{from}\" TO \"{to}\";");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, from, to) in Renames)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE qams.{table} RENAME CONSTRAINT \"{to}\" TO \"{from}\";");
            }
        }
    }
}
