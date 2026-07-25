using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentReviewCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "next_review_due",
                schema: "qams",
                table: "controlled_document",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "review_cycle_months",
                schema: "qams",
                table: "controlled_document",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "review_due_raised",
                schema: "qams",
                table: "controlled_document",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "next_review_due",
                schema: "qams",
                table: "controlled_document");

            migrationBuilder.DropColumn(
                name: "review_cycle_months",
                schema: "qams",
                table: "controlled_document");

            migrationBuilder.DropColumn(
                name: "review_due_raised",
                schema: "qams",
                table: "controlled_document");
        }
    }
}
