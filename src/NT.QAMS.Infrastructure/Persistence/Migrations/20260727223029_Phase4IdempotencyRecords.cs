using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4IdempotencyRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idempotency_record",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    request_type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_record", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_created",
                schema: "qams",
                table: "idempotency_record",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ux_idempotency_actor_key",
                schema: "qams",
                table: "idempotency_record",
                columns: new[] { "actor_id", "idempotency_key", "request_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_record",
                schema: "qams");
        }
    }
}
