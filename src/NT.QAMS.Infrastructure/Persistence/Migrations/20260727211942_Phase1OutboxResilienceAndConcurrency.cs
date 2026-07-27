using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// EA remediation Phase 1 (v1.39): outbox resilience columns (retry
    /// schedule, dead-letter flag, claim lease), the pending-index filter that
    /// excludes dead-lettered rows, and the NC-per-source natural idempotency
    /// key.
    ///
    /// NOTE: the scaffolder also emitted AddColumn/DropColumn operations for
    /// the new <c>xmin</c> concurrency tokens on every aggregate table — those
    /// were removed by hand, exactly as the Npgsql provider documentation
    /// instructs: <c>xmin</c> is a PostgreSQL SYSTEM column that already exists
    /// on every table, so mapping it requires no DDL (and the emitted
    /// ADD COLUMN would fail).
    /// </summary>
    public partial class Phase1OutboxResilienceAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_event_pending",
                schema: "qams",
                table: "outbox_event");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "claimed_until_utc",
                schema: "qams",
                table: "outbox_event",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dead_lettered_at_utc",
                schema: "qams",
                table: "outbox_event",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at_utc",
                schema: "qams",
                table: "outbox_event",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_event_dead_letter",
                schema: "qams",
                table: "outbox_event",
                column: "dead_lettered_at_utc",
                filter: "dead_lettered_at_utc IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_event_pending",
                schema: "qams",
                table: "outbox_event",
                column: "occurred_at_utc",
                filter: "processed_at_utc IS NULL AND dead_lettered_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_nonconformance_source",
                schema: "qams",
                table: "nonconformance",
                columns: new[] { "tenant_id", "source_ref" },
                unique: true,
                filter: "source_ref IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_event_dead_letter",
                schema: "qams",
                table: "outbox_event");

            migrationBuilder.DropIndex(
                name: "ix_outbox_event_pending",
                schema: "qams",
                table: "outbox_event");

            migrationBuilder.DropIndex(
                name: "ux_nonconformance_source",
                schema: "qams",
                table: "nonconformance");

            migrationBuilder.DropColumn(
                name: "claimed_until_utc",
                schema: "qams",
                table: "outbox_event");

            migrationBuilder.DropColumn(
                name: "dead_lettered_at_utc",
                schema: "qams",
                table: "outbox_event");

            migrationBuilder.DropColumn(
                name: "next_attempt_at_utc",
                schema: "qams",
                table: "outbox_event");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_event_pending",
                schema: "qams",
                table: "outbox_event",
                column: "occurred_at_utc",
                filter: "processed_at_utc IS NULL");
        }
    }
}
