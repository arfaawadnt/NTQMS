using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeControlEmergencyPathway : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "qams",
                table: "change_request",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "impact_level",
                schema: "qams",
                table: "change_request",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.AddColumn<bool>(
                name: "is_emergency",
                schema: "qams",
                table: "change_request",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ratified_at_utc",
                schema: "qams",
                table: "change_request",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ratified_by",
                schema: "qams",
                table: "change_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "retrospective_deadline",
                schema: "qams",
                table: "change_request",
                type: "date",
                nullable: true);

            // Widen the status CHECK domain for the emergency pathway's new state, and fence the
            // new impact_level column to its enum values. (Existing rows default to 'Medium'.)
            migrationBuilder.Sql("""
                ALTER TABLE qams.change_request DROP CONSTRAINT IF EXISTS ck_change_request_status_domain;
                ALTER TABLE qams.change_request ADD CONSTRAINT ck_change_request_status_domain
                  CHECK (status IN ('Proposed','Approved','Rejected','Closed','Reviewed','ImplementedPendingRatification')) NOT VALID;
                ALTER TABLE qams.change_request VALIDATE CONSTRAINT ck_change_request_status_domain;

                ALTER TABLE qams.change_request ADD CONSTRAINT ck_change_request_impact_level_domain
                  CHECK (impact_level IN ('Low','Medium','High')) NOT VALID;
                ALTER TABLE qams.change_request VALIDATE CONSTRAINT ck_change_request_impact_level_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the original status CHECK domain and drop the impact_level domain before the
            // columns are removed.
            migrationBuilder.Sql("""
                ALTER TABLE qams.change_request DROP CONSTRAINT IF EXISTS ck_change_request_impact_level_domain;
                ALTER TABLE qams.change_request DROP CONSTRAINT IF EXISTS ck_change_request_status_domain;
                ALTER TABLE qams.change_request ADD CONSTRAINT ck_change_request_status_domain
                  CHECK (status IN ('Proposed','Approved','Rejected','Closed','Reviewed')) NOT VALID;
                ALTER TABLE qams.change_request VALIDATE CONSTRAINT ck_change_request_status_domain;
                """);

            migrationBuilder.DropColumn(
                name: "impact_level",
                schema: "qams",
                table: "change_request");

            migrationBuilder.DropColumn(
                name: "is_emergency",
                schema: "qams",
                table: "change_request");

            migrationBuilder.DropColumn(
                name: "ratified_at_utc",
                schema: "qams",
                table: "change_request");

            migrationBuilder.DropColumn(
                name: "ratified_by",
                schema: "qams",
                table: "change_request");

            migrationBuilder.DropColumn(
                name: "retrospective_deadline",
                schema: "qams",
                table: "change_request");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "qams",
                table: "change_request",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);
        }
    }
}
