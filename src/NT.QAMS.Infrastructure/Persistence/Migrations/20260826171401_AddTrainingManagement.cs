using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "training_course",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    duration_hours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    validity_months = table.Column<int>(type: "integer", nullable: true),
                    pass_mark = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_training_course", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "training_session",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scheduled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trainer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("pk_training_session", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "training_session_attendance",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trainee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attended = table.Column<bool>(type: "boolean", nullable: false),
                    pre_score = table.Column<int>(type: "integer", nullable: true),
                    post_score = table.Column<int>(type: "integer", nullable: true),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    training_session_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_session_attendance", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_training_session_attendance_training_session_tenant_id_trai",
                        columns: x => new { x.tenant_id, x.training_session_id },
                        principalSchema: "qams",
                        principalTable: "training_session",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_training_course_tenant_id_category_status",
                schema: "qams",
                table: "training_course",
                columns: new[] { "tenant_id", "category", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_training_course_tenant_id_course_ref",
                schema: "qams",
                table: "training_course",
                columns: new[] { "tenant_id", "course_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_training_session_tenant_id_course_id",
                schema: "qams",
                table: "training_session",
                columns: new[] { "tenant_id", "course_id" });

            migrationBuilder.CreateIndex(
                name: "ix_training_session_tenant_id_session_ref",
                schema: "qams",
                table: "training_session",
                columns: new[] { "tenant_id", "session_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_training_session_tenant_id_status",
                schema: "qams",
                table: "training_session",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_training_session_attendance_tenant_id_training_session_id",
                schema: "qams",
                table: "training_session_attendance",
                columns: new[] { "tenant_id", "training_session_id" });

            // Mandatory FORCE RLS + tenant_isolation policy on all three tables (incl. the owned child).
            foreach (var t in new[] { "training_course", "training_session", "training_session_attendance" })
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

            // Value domains, derived from the C# enums (never guessed), plus score ranges.
            migrationBuilder.Sql("""
                ALTER TABLE qams.training_course ADD CONSTRAINT ck_training_course_category_domain
                  CHECK (category IN ('Mandatory','Clinical','Safety','Orientation','Cme')) NOT VALID;
                ALTER TABLE qams.training_course VALIDATE CONSTRAINT ck_training_course_category_domain;

                ALTER TABLE qams.training_course ADD CONSTRAINT ck_training_course_status_domain
                  CHECK (status IN ('Draft','Active','Retired')) NOT VALID;
                ALTER TABLE qams.training_course VALIDATE CONSTRAINT ck_training_course_status_domain;

                ALTER TABLE qams.training_course ADD CONSTRAINT ck_training_course_pass_mark_range
                  CHECK (pass_mark BETWEEN 0 AND 100) NOT VALID;
                ALTER TABLE qams.training_course VALIDATE CONSTRAINT ck_training_course_pass_mark_range;

                ALTER TABLE qams.training_session ADD CONSTRAINT ck_training_session_status_domain
                  CHECK (status IN ('Scheduled','Held','Closed','Cancelled')) NOT VALID;
                ALTER TABLE qams.training_session VALIDATE CONSTRAINT ck_training_session_status_domain;

                ALTER TABLE qams.training_session_attendance ADD CONSTRAINT ck_training_session_attendance_score_range
                  CHECK ((pre_score IS NULL OR pre_score BETWEEN 0 AND 100)
                         AND (post_score IS NULL OR post_score BETWEEN 0 AND 100)) NOT VALID;
                ALTER TABLE qams.training_session_attendance VALIDATE CONSTRAINT ck_training_session_attendance_score_range;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "training_course",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "training_session_attendance",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "training_session",
                schema: "qams");
        }
    }
}
