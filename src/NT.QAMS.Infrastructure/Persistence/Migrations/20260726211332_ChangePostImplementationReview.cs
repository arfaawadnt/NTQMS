using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangePostImplementationReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "change_effective",
                schema: "qams",
                table: "change_request",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "post_implementation_review_notes",
                schema: "qams",
                table: "change_request",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "post_implementation_reviewed_at_utc",
                schema: "qams",
                table: "change_request",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "post_implementation_reviewed_by",
                schema: "qams",
                table: "change_request",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "change_effective",
                schema: "qams",
                table: "change_request");

            migrationBuilder.DropColumn(
                name: "post_implementation_review_notes",
                schema: "qams",
                table: "change_request");

            migrationBuilder.DropColumn(
                name: "post_implementation_reviewed_at_utc",
                schema: "qams",
                table: "change_request");

            migrationBuilder.DropColumn(
                name: "post_implementation_reviewed_by",
                schema: "qams",
                table: "change_request");
        }
    }
}
