using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NT.QAMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SignedRecordImmutability : Migration
    {
        // Tables whose signed/approved records must be immutable at the database
        // (column, frozen-state value). The domain already blocks post-sign-off
        // edits; this is defence-in-depth so no raw-SQL or bypass path can alter a
        // frozen regulated record undetectably (audit finding F-02).
        private static readonly (string Table, string Column, string Frozen)[] Frozen =
        [
            ("validation_study", "state", "SignedOff"),
            ("method_comparison_study", "state", "SignedOff"),
            ("precision_study", "state", "SignedOff"),
            ("linearity_study", "state", "SignedOff"),
            ("detection_limit_study", "state", "SignedOff"),
            ("reference_interval_study", "state", "SignedOff"),
            ("sigma_assessment", "state", "SignedOff"),
            ("outlier_screening", "state", "SignedOff"),
            ("carryover_study", "state", "SignedOff"),
            ("lot_comparison_study", "state", "SignedOff"),
            ("interference_study", "state", "SignedOff"),
            ("instrument_comparability_study", "state", "SignedOff"),
            ("uncertainty_budget", "status", "Approved"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Generic guard: reject UPDATE/DELETE when the OLD row is in its frozen
            // state. The sign-off transition itself is allowed because at that point
            // the OLD state is still Calculated/DataEntry, not the frozen value.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION qams.reject_frozen_mutation() RETURNS trigger AS $fn$
                DECLARE
                    frozen_col text := TG_ARGV[0];
                    frozen_val text := TG_ARGV[1];
                    old_val text;
                BEGIN
                    old_val := row_to_json(OLD) ->> frozen_col;
                    IF old_val = frozen_val THEN
                        RAISE EXCEPTION
                            'signed/approved record is immutable and cannot be modified or deleted (%.% is %)',
                            TG_TABLE_SCHEMA, TG_TABLE_NAME, frozen_val
                            USING ERRCODE = 'check_violation';
                    END IF;
                    IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
                END;
                $fn$ LANGUAGE plpgsql;
            ");

            foreach (var (table, column, frozen) in Frozen)
            {
                migrationBuilder.Sql($@"
                    DROP TRIGGER IF EXISTS frozen_immutability ON qams.{table};
                    CREATE TRIGGER frozen_immutability
                        BEFORE UPDATE OR DELETE ON qams.{table}
                        FOR EACH ROW EXECUTE FUNCTION qams.reject_frozen_mutation('{column}', '{frozen}');
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

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS qams.reject_frozen_mutation();");
        }
    }
}
