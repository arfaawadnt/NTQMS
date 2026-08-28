using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommitteeIntegrityIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_meeting_attendance_tenant_id_meeting_id",
                schema: "qams",
                table: "meeting_attendance");

            migrationBuilder.DropIndex(
                name: "ix_committee_member_tenant_id_committee_id",
                schema: "qams",
                table: "committee_member");

            migrationBuilder.CreateIndex(
                name: "ux_meeting_attendance_tenant_meeting_user",
                schema: "qams",
                table: "meeting_attendance",
                columns: new[] { "tenant_id", "meeting_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_committee_member_tenant_committee_user",
                schema: "qams",
                table: "committee_member",
                columns: new[] { "tenant_id", "committee_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_meeting_attendance_tenant_meeting_user",
                schema: "qams",
                table: "meeting_attendance");

            migrationBuilder.DropIndex(
                name: "ux_committee_member_tenant_committee_user",
                schema: "qams",
                table: "committee_member");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_attendance_tenant_id_meeting_id",
                schema: "qams",
                table: "meeting_attendance",
                columns: new[] { "tenant_id", "meeting_id" });

            migrationBuilder.CreateIndex(
                name: "ix_committee_member_tenant_id_committee_id",
                schema: "qams",
                table: "committee_member",
                columns: new[] { "tenant_id", "committee_id" });
        }
    }
}
