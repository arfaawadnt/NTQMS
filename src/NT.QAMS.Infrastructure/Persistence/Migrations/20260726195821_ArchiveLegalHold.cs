using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArchiveLegalHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_on_legal_hold",
                schema: "qams",
                table: "archive_entry",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "legal_hold_placed_by",
                schema: "qams",
                table: "archive_entry",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legal_hold_reason",
                schema: "qams",
                table: "archive_entry",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_on_legal_hold",
                schema: "qams",
                table: "archive_entry");

            migrationBuilder.DropColumn(
                name: "legal_hold_placed_by",
                schema: "qams",
                table: "archive_entry");

            migrationBuilder.DropColumn(
                name: "legal_hold_reason",
                schema: "qams",
                table: "archive_entry");
        }
    }
}
