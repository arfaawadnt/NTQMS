using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                schema: "qams",
                table: "supplier",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                schema: "qams",
                table: "supplier",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                schema: "qams",
                table: "risk_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                schema: "qams",
                table: "risk_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                schema: "qams",
                table: "nonconformance",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                schema: "qams",
                table: "nonconformance",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                schema: "qams",
                table: "management_review",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                schema: "qams",
                table: "management_review",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                schema: "qams",
                table: "equipment_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                schema: "qams",
                table: "equipment_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                schema: "qams",
                table: "complaint",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                schema: "qams",
                table: "complaint",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                schema: "qams",
                table: "change_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                schema: "qams",
                table: "change_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                schema: "qams",
                table: "audit",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                schema: "qams",
                table: "audit",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "branch_id",
                schema: "qams",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "department_id",
                schema: "qams",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "branch_id",
                schema: "qams",
                table: "risk_item");

            migrationBuilder.DropColumn(
                name: "department_id",
                schema: "qams",
                table: "risk_item");

            migrationBuilder.DropColumn(
                name: "branch_id",
                schema: "qams",
                table: "nonconformance");

            migrationBuilder.DropColumn(
                name: "department_id",
                schema: "qams",
                table: "nonconformance");

            migrationBuilder.DropColumn(
                name: "branch_id",
                schema: "qams",
                table: "management_review");

            migrationBuilder.DropColumn(
                name: "department_id",
                schema: "qams",
                table: "management_review");

            migrationBuilder.DropColumn(
                name: "branch_id",
                schema: "qams",
                table: "equipment_item");

            migrationBuilder.DropColumn(
                name: "department_id",
                schema: "qams",
                table: "equipment_item");

            migrationBuilder.DropColumn(
                name: "branch_id",
                schema: "qams",
                table: "complaint");

            migrationBuilder.DropColumn(
                name: "department_id",
                schema: "qams",
                table: "complaint");

            migrationBuilder.DropColumn(
                name: "branch_id",
                schema: "qams",
                table: "change_request");

            migrationBuilder.DropColumn(
                name: "department_id",
                schema: "qams",
                table: "change_request");

            migrationBuilder.DropColumn(
                name: "branch_id",
                schema: "qams",
                table: "audit");

            migrationBuilder.DropColumn(
                name: "department_id",
                schema: "qams",
                table: "audit");
        }
    }
}
