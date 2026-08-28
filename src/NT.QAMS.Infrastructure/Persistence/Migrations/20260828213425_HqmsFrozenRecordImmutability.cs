using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HqmsFrozenRecordImmutability : Migration
    {
        // HQMS frozen-state records (audit finding M-05), same defence-in-depth
        // as SignedRecordImmutability (F-02): once the signed/approved state is
        // reached, the database itself rejects UPDATE/DELETE — the transition
        // INTO the frozen state is still allowed because the OLD row is not yet
        // frozen when it happens.
        // The frozen boundary is the AGGREGATE ROW that the signature/approval
        // covers: the incident record and the meeting record (minutes text,
        // held facts). Meeting DECISIONS deliberately stay live — action items
        // are tracked to closure after the minutes are approved (CloseDecision
        // has no meeting-status guard by design), and they live in their own
        // table, so the meeting-row trigger does not fire for them.
        private static readonly (string Table, string Column, string Frozen)[] Frozen =
        [
            ("incident", "status", "Closed"),
            ("meeting", "status", "MinutesApproved"),
        ];

        // Survey responses are immutable FROM CAPTURE (there is no draft state
        // and no legitimate write path — the aggregate is create-only), so they
        // get an unconditional guard, answers included.
        private static readonly string[] AlwaysFrozen = ["survey_response", "survey_answer"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column, frozen) in Frozen)
            {
                migrationBuilder.Sql($@"
                    DROP TRIGGER IF EXISTS frozen_immutability ON qams.{table};
                    CREATE TRIGGER frozen_immutability
                        BEFORE UPDATE OR DELETE ON qams.{table}
                        FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('{column}', '{frozen}');
                ");
            }

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION qams.reject_any_mutation() RETURNS trigger AS $fn$
                BEGIN
                    RAISE EXCEPTION
                        'record is immutable once captured and cannot be modified or deleted (%.%)',
                        TG_TABLE_SCHEMA, TG_TABLE_NAME
                        USING ERRCODE = 'check_violation';
                END;
                $fn$ LANGUAGE plpgsql;
            ");

            foreach (var table in AlwaysFrozen)
            {
                migrationBuilder.Sql($@"
                    DROP TRIGGER IF EXISTS frozen_immutability ON qams.{table};
                    CREATE TRIGGER frozen_immutability
                        BEFORE UPDATE OR DELETE ON qams.{table}
                        FOR EACH ROW EXECUTE FUNCTION qams.reject_any_mutation();
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, _, _) in Frozen)
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS frozen_immutability ON qams.{table};");
            }

            foreach (var table in AlwaysFrozen)
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS frozen_immutability ON qams.{table};");
            }

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS qams.reject_any_mutation();");
        }
    }
}
