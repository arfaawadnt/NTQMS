using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QcTargetEffectiveDating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_target_change_reason",
                schema: "qams",
                table: "qc_profile",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "target_effective_from_utc",
                schema: "qams",
                table: "qc_profile",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_target_change_reason",
                schema: "qams",
                table: "qc_profile");

            migrationBuilder.DropColumn(
                name: "target_effective_from_utc",
                schema: "qams",
                table: "qc_profile");
        }
    }
}
