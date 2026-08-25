using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommittees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "committee",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    terms_of_reference = table.Column<string>(type: "text", nullable: false),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quorum_size = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_committee", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "meeting",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    committee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scheduled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    minutes = table.Column<string>(type: "text", nullable: true),
                    minutes_approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "committee_member",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    committee_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_committee_member", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_committee_member_committee_tenant_id_committee_id",
                        columns: x => new { x.tenant_id, x.committee_id },
                        principalSchema: "qams",
                        principalTable: "committee",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_agenda_item",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_ref = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    carried_forward = table.Column<bool>(type: "boolean", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_agenda_item", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_meeting_agenda_item_meeting_tenant_id_meeting_id",
                        columns: x => new { x.tenant_id, x.meeting_id },
                        principalSchema: "qams",
                        principalTable: "meeting",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_attendance",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    present = table.Column<bool>(type: "boolean", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_attendance", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_meeting_attendance_meeting_tenant_id_meeting_id",
                        columns: x => new { x.tenant_id, x.meeting_id },
                        principalSchema: "qams",
                        principalTable: "meeting",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_decision",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    closure_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_decision", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_meeting_decision_meeting_tenant_id_meeting_id",
                        columns: x => new { x.tenant_id, x.meeting_id },
                        principalSchema: "qams",
                        principalTable: "meeting",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_committee_tenant_id_status",
                schema: "qams",
                table: "committee",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_committee_member_tenant_id_committee_id",
                schema: "qams",
                table: "committee_member",
                columns: new[] { "tenant_id", "committee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_tenant_id_committee_id",
                schema: "qams",
                table: "meeting",
                columns: new[] { "tenant_id", "committee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_tenant_id_meeting_ref",
                schema: "qams",
                table: "meeting",
                columns: new[] { "tenant_id", "meeting_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_agenda_item_tenant_id_meeting_id",
                schema: "qams",
                table: "meeting_agenda_item",
                columns: new[] { "tenant_id", "meeting_id" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_attendance_tenant_id_meeting_id",
                schema: "qams",
                table: "meeting_attendance",
                columns: new[] { "tenant_id", "meeting_id" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_decision_tenant_id_meeting_id",
                schema: "qams",
                table: "meeting_decision",
                columns: new[] { "tenant_id", "meeting_id" });

            // Mandatory FORCE RLS + tenant_isolation policy on every tenant table.
            foreach (var t in new[]
            {
                "committee", "committee_member", "meeting",
                "meeting_agenda_item", "meeting_attendance", "meeting_decision",
            })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE qams.{t} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE qams.{t} FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation ON qams.{t};
                    CREATE POLICY tenant_isolation ON qams.{t}
                      FOR ALL
                      USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                             OR current_setting('app.bypass_rls', true) = 'on')
                      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
                             OR current_setting('app.bypass_rls', true) = 'on');
                    """);
            }

            // Value domains, derived from the C# enums (never guessed).
            migrationBuilder.Sql("""
                ALTER TABLE qams.committee ADD CONSTRAINT ck_committee_frequency_domain
                  CHECK (frequency IN ('Weekly','Monthly','Quarterly','Biannual','Annual','AdHoc')) NOT VALID;
                ALTER TABLE qams.committee VALIDATE CONSTRAINT ck_committee_frequency_domain;

                ALTER TABLE qams.committee ADD CONSTRAINT ck_committee_status_domain
                  CHECK (status IN ('Active','Disbanded')) NOT VALID;
                ALTER TABLE qams.committee VALIDATE CONSTRAINT ck_committee_status_domain;

                ALTER TABLE qams.meeting ADD CONSTRAINT ck_meeting_status_domain
                  CHECK (status IN ('Scheduled','Held','MinutesApproved','Cancelled')) NOT VALID;
                ALTER TABLE qams.meeting VALIDATE CONSTRAINT ck_meeting_status_domain;

                ALTER TABLE qams.meeting_decision ADD CONSTRAINT ck_meeting_decision_status_domain
                  CHECK (status IN ('Open','Closed')) NOT VALID;
                ALTER TABLE qams.meeting_decision VALIDATE CONSTRAINT ck_meeting_decision_status_domain;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "committee_member",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "meeting_agenda_item",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "meeting_attendance",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "meeting_decision",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "committee",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "meeting",
                schema: "qams");
        }
    }
}
