using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HaiComplicationRejectTransition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rejected_at_utc",
                schema: "qams",
                table: "hai_case",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rejected_by",
                schema: "qams",
                table: "hai_case",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                schema: "qams",
                table: "hai_case",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rejected_at_utc",
                schema: "qams",
                table: "complication_case",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rejected_by",
                schema: "qams",
                table: "complication_case",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                schema: "qams",
                table: "complication_case",
                type: "text",
                nullable: true);

            // M-18: the status CHECK domains gain the Rejected state.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.hai_case DROP CONSTRAINT IF EXISTS ck_hai_case_status_domain;
                ALTER TABLE qams.hai_case ADD CONSTRAINT ck_hai_case_status_domain
                  CHECK (status IN ('Reported','Reviewed','Closed','Rejected')) NOT VALID;
                ALTER TABLE qams.hai_case VALIDATE CONSTRAINT ck_hai_case_status_domain;

                ALTER TABLE qams.complication_case DROP CONSTRAINT IF EXISTS ck_complication_case_status_domain;
                ALTER TABLE qams.complication_case ADD CONSTRAINT ck_complication_case_status_domain
                  CHECK (status IN ('Reported','Reviewed','Closed','Rejected')) NOT VALID;
                ALTER TABLE qams.complication_case VALIDATE CONSTRAINT ck_complication_case_status_domain;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rolling back with Rejected rows present fails the VALIDATE loudly —
            // deliberate: those rows have no legal pre-M-18 state.
            migrationBuilder.Sql(@"
                ALTER TABLE qams.hai_case DROP CONSTRAINT IF EXISTS ck_hai_case_status_domain;
                ALTER TABLE qams.hai_case ADD CONSTRAINT ck_hai_case_status_domain
                  CHECK (status IN ('Reported','Reviewed','Closed')) NOT VALID;
                ALTER TABLE qams.hai_case VALIDATE CONSTRAINT ck_hai_case_status_domain;

                ALTER TABLE qams.complication_case DROP CONSTRAINT IF EXISTS ck_complication_case_status_domain;
                ALTER TABLE qams.complication_case ADD CONSTRAINT ck_complication_case_status_domain
                  CHECK (status IN ('Reported','Reviewed','Closed')) NOT VALID;
                ALTER TABLE qams.complication_case VALIDATE CONSTRAINT ck_complication_case_status_domain;
            ");

            migrationBuilder.DropColumn(
                name: "rejected_at_utc",
                schema: "qams",
                table: "hai_case");

            migrationBuilder.DropColumn(
                name: "rejected_by",
                schema: "qams",
                table: "hai_case");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                schema: "qams",
                table: "hai_case");

            migrationBuilder.DropColumn(
                name: "rejected_at_utc",
                schema: "qams",
                table: "complication_case");

            migrationBuilder.DropColumn(
                name: "rejected_by",
                schema: "qams",
                table: "complication_case");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                schema: "qams",
                table: "complication_case");
        }
    }
}
