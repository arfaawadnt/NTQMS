using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FieldChangeLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "field_change",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    property = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    old_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_change", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_field_change_occurred_at_utc",
                schema: "audit",
                table: "field_change",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_field_change_tenant_id_entity_id",
                schema: "audit",
                table: "field_change",
                columns: new[] { "tenant_id", "entity_id" });

            // Append-only + RLS, matching the other audit ledgers.
            migrationBuilder.Sql(@"
                ALTER TABLE audit.field_change ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON audit.field_change
                    USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
                CREATE TRIGGER field_change_append_only BEFORE UPDATE OR DELETE ON audit.field_change
                    FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "field_change",
                schema: "audit");
        }
    }
}
