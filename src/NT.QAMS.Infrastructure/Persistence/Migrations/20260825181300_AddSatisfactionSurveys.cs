using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSatisfactionSurveys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "satisfaction_survey",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_satisfaction_survey", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "survey_response",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_line = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_response", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "survey_question",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    domain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    survey_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_question", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_survey_question_satisfaction_survey_tenant_id_survey_id",
                        columns: x => new { x.tenant_id, x.survey_id },
                        principalSchema: "qams",
                        principalTable: "satisfaction_survey",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_answer",
                schema: "qams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    survey_response_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_answer", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_survey_answer_survey_response_tenant_id_survey_response_id",
                        columns: x => new { x.tenant_id, x.survey_response_id },
                        principalSchema: "qams",
                        principalTable: "survey_response",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_satisfaction_survey_tenant_id_status",
                schema: "qams",
                table: "satisfaction_survey",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_answer_tenant_id_survey_response_id",
                schema: "qams",
                table: "survey_answer",
                columns: new[] { "tenant_id", "survey_response_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_question_tenant_id_survey_id",
                schema: "qams",
                table: "survey_question",
                columns: new[] { "tenant_id", "survey_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_response_tenant_id_survey_id",
                schema: "qams",
                table: "survey_response",
                columns: new[] { "tenant_id", "survey_id" });

            // Mandatory FORCE RLS + tenant_isolation policy on every tenant table.
            foreach (var t in new[] { "satisfaction_survey", "survey_question", "survey_response", "survey_answer" })
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

            // Value domain for the survey status, plus the 1–5 Likert bound on answer scores.
            migrationBuilder.Sql("""
                ALTER TABLE qams.satisfaction_survey ADD CONSTRAINT ck_satisfaction_survey_status_domain
                  CHECK (status IN ('Draft','Open','Closed')) NOT VALID;
                ALTER TABLE qams.satisfaction_survey VALIDATE CONSTRAINT ck_satisfaction_survey_status_domain;

                ALTER TABLE qams.survey_answer ADD CONSTRAINT ck_survey_answer_score_range
                  CHECK (score BETWEEN 1 AND 5) NOT VALID;
                ALTER TABLE qams.survey_answer VALIDATE CONSTRAINT ck_survey_answer_score_range;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "survey_answer",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "survey_question",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "survey_response",
                schema: "qams");

            migrationBuilder.DropTable(
                name: "satisfaction_survey",
                schema: "qams");
        }
    }
}
