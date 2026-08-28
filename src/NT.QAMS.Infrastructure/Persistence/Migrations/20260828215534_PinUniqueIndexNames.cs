using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PinUniqueIndexNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_training_session_tenant_id_session_ref",
                schema: "qams",
                table: "training_session",
                newName: "ux_training_session_tenant_id_session_ref");

            migrationBuilder.RenameIndex(
                name: "ix_training_course_tenant_id_course_ref",
                schema: "qams",
                table: "training_course",
                newName: "ux_training_course_tenant_id_course_ref");

            migrationBuilder.RenameIndex(
                name: "ix_safety_round_tenant_id_round_ref",
                schema: "qams",
                table: "safety_round",
                newName: "ux_safety_round_tenant_id_round_ref");

            migrationBuilder.RenameIndex(
                name: "ix_quality_indicator_tenant_id_code",
                schema: "qams",
                table: "quality_indicator",
                newName: "ux_quality_indicator_tenant_id_code");

            migrationBuilder.RenameIndex(
                name: "ix_practitioner_tenant_id_practitioner_ref",
                schema: "qams",
                table: "practitioner",
                newName: "ux_practitioner_tenant_id_practitioner_ref");

            migrationBuilder.RenameIndex(
                name: "ix_patient_safety_event_tenant_id_event_ref",
                schema: "qams",
                table: "patient_safety_event",
                newName: "ux_patient_safety_event_tenant_id_event_ref");

            migrationBuilder.RenameIndex(
                name: "ix_mortality_review_tenant_id_review_ref",
                schema: "qams",
                table: "mortality_review",
                newName: "ux_mortality_review_tenant_id_review_ref");

            migrationBuilder.RenameIndex(
                name: "ix_meeting_tenant_id_meeting_ref",
                schema: "qams",
                table: "meeting",
                newName: "ux_meeting_tenant_id_meeting_ref");

            migrationBuilder.RenameIndex(
                name: "ix_indicator_measurement_tenant_id_indicator_id_period",
                schema: "qams",
                table: "indicator_measurement",
                newName: "ux_indicator_measurement_tenant_id_indicator_id_period");

            migrationBuilder.RenameIndex(
                name: "ix_incident_tenant_id_incident_ref",
                schema: "qams",
                table: "incident",
                newName: "ux_incident_tenant_id_incident_ref");

            migrationBuilder.RenameIndex(
                name: "ix_hai_case_tenant_id_case_ref",
                schema: "qams",
                table: "hai_case",
                newName: "ux_hai_case_tenant_id_case_ref");

            migrationBuilder.RenameIndex(
                name: "ix_fmea_study_tenant_id_fmea_ref",
                schema: "qams",
                table: "fmea_study",
                newName: "ux_fmea_study_tenant_id_fmea_ref");

            migrationBuilder.RenameIndex(
                name: "ix_drill_tenant_id_drill_ref",
                schema: "qams",
                table: "drill",
                newName: "ux_drill_tenant_id_drill_ref");

            migrationBuilder.RenameIndex(
                name: "ix_complication_case_tenant_id_case_ref",
                schema: "qams",
                table: "complication_case",
                newName: "ux_complication_case_tenant_id_case_ref");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ux_training_session_tenant_id_session_ref",
                schema: "qams",
                table: "training_session",
                newName: "ix_training_session_tenant_id_session_ref");

            migrationBuilder.RenameIndex(
                name: "ux_training_course_tenant_id_course_ref",
                schema: "qams",
                table: "training_course",
                newName: "ix_training_course_tenant_id_course_ref");

            migrationBuilder.RenameIndex(
                name: "ux_safety_round_tenant_id_round_ref",
                schema: "qams",
                table: "safety_round",
                newName: "ix_safety_round_tenant_id_round_ref");

            migrationBuilder.RenameIndex(
                name: "ux_quality_indicator_tenant_id_code",
                schema: "qams",
                table: "quality_indicator",
                newName: "ix_quality_indicator_tenant_id_code");

            migrationBuilder.RenameIndex(
                name: "ux_practitioner_tenant_id_practitioner_ref",
                schema: "qams",
                table: "practitioner",
                newName: "ix_practitioner_tenant_id_practitioner_ref");

            migrationBuilder.RenameIndex(
                name: "ux_patient_safety_event_tenant_id_event_ref",
                schema: "qams",
                table: "patient_safety_event",
                newName: "ix_patient_safety_event_tenant_id_event_ref");

            migrationBuilder.RenameIndex(
                name: "ux_mortality_review_tenant_id_review_ref",
                schema: "qams",
                table: "mortality_review",
                newName: "ix_mortality_review_tenant_id_review_ref");

            migrationBuilder.RenameIndex(
                name: "ux_meeting_tenant_id_meeting_ref",
                schema: "qams",
                table: "meeting",
                newName: "ix_meeting_tenant_id_meeting_ref");

            migrationBuilder.RenameIndex(
                name: "ux_indicator_measurement_tenant_id_indicator_id_period",
                schema: "qams",
                table: "indicator_measurement",
                newName: "ix_indicator_measurement_tenant_id_indicator_id_period");

            migrationBuilder.RenameIndex(
                name: "ux_incident_tenant_id_incident_ref",
                schema: "qams",
                table: "incident",
                newName: "ix_incident_tenant_id_incident_ref");

            migrationBuilder.RenameIndex(
                name: "ux_hai_case_tenant_id_case_ref",
                schema: "qams",
                table: "hai_case",
                newName: "ix_hai_case_tenant_id_case_ref");

            migrationBuilder.RenameIndex(
                name: "ux_fmea_study_tenant_id_fmea_ref",
                schema: "qams",
                table: "fmea_study",
                newName: "ix_fmea_study_tenant_id_fmea_ref");

            migrationBuilder.RenameIndex(
                name: "ux_drill_tenant_id_drill_ref",
                schema: "qams",
                table: "drill",
                newName: "ix_drill_tenant_id_drill_ref");

            migrationBuilder.RenameIndex(
                name: "ux_complication_case_tenant_id_case_ref",
                schema: "qams",
                table: "complication_case",
                newName: "ix_complication_case_tenant_id_case_ref");
        }
    }
}
