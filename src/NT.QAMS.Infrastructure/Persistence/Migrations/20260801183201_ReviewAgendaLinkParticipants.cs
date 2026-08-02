using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewAgendaLinkParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "agenda",
                schema: "qams",
                table: "management_review",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "meeting_link",
                schema: "qams",
                table: "management_review",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "review_participant",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_participant", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_review_participant_management_review_tenant_id_review_id",
                        columns: x => new { x.tenant_id, x.review_id },
                        principalSchema: "qams",
                        principalTable: "management_review",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_review_participant_user",
                schema: "qams",
                table: "review_participant",
                columns: new[] { "tenant_id", "review_id", "user_id" },
                unique: true);

            // Tenant isolation — EF does not generate row-level security.
            migrationBuilder.Sql("""
                ALTER TABLE qams.review_participant ENABLE ROW LEVEL SECURITY;
                ALTER TABLE qams.review_participant FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON qams.review_participant;
                CREATE POLICY tenant_isolation ON qams.review_participant
                  FOR ALL
                  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                         OR current_setting('app.bypass_rls', true) = 'on')
                  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                         OR current_setting('app.bypass_rls', true) = 'on');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_participant",
                schema: "qams");

            migrationBuilder.DropColumn(
                name: "agenda",
                schema: "qams",
                table: "management_review");

            migrationBuilder.DropColumn(
                name: "meeting_link",
                schema: "qams",
                table: "management_review");
        }
    }
}
