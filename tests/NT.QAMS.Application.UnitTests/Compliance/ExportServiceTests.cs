using FluentAssertions;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Common;
using NT.QAMS.Contracts.Reporting;
using NT.QAMS.Infrastructure.Exports;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Compliance;

/// <summary>
/// The exports must be REAL formats (Part 11 §11.10(b)) — a genuine zip-based
/// XLSX and a genuine PDF, not HTML wearing a file extension.
/// </summary>
public sealed class ExportServiceTests
{
    private static readonly ExportPack Pack = new(
        "Nonconformance Register", "Demo Laboratory", "QM Tester",
        new DateTimeOffset(2026, 7, 25, 9, 0, 0, TimeSpan.Zero),
        [
            new ExportTable(
                "Nonconformances",
                ["Reference", "Title", "Status"],
                [["NC-2026-0001", "Balance drift", "Closed"], ["NC-2026-0002", "Late report", "Raised"]]),
            new ExportTable("Decisions", ["Description"], [["Recalibrate quarterly"]]),
        ]);

    [Fact]
    public void Xlsx_output_is_a_genuine_zip_based_workbook()
    {
        var bytes = new ExportService().ToXlsx(Pack);

        bytes.Length.Should().BeGreaterThan(1000);
        // XLSX is a zip container: PK\x03\x04 magic.
        bytes[..4].Should().Equal(0x50, 0x4B, 0x03, 0x04);
    }

    [Fact]
    public void Pdf_output_is_a_genuine_pdf_document()
    {
        var bytes = new ExportService().ToPdf(Pack);

        bytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes[..5]).Should().Be("%PDF-");
    }

    // ── Quality Analytics report (URS-130) ──────────────────────────────────────
    // QuestPDF validates layout only at GeneratePdf(), so a full render is the
    // real proof that the gauge, progress bars, Pareto bars and 5×5 risk matrix
    // compose without a zero-weight / overflow fault — for both a fully-populated
    // computation and an all-empty one (null score, every component "no data").

    private static readonly DateTimeOffset At = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);

    private static QualityAnalyticsReportPack AnalyticsPack(QualityAnalyticsDto dto) =>
        new("Demo Laboratory", "QM Tester", At, "Branch: HQ — Main Laboratory", dto);

    private static QualityAnalyticsDto FullDto()
    {
        var components = new[]
        {
            new QualityHealthComponentDto("DocumentControl", 10, 92.0m, true, null),
            new QualityHealthComponentDto("NonconformanceCapa", 15, 80.0m, true, null),
            new QualityHealthComponentDto("Complaints", 10, 100.0m, true, null),
            new QualityHealthComponentDto("InternalAudit", 10, 66.7m, true, null),
            new QualityHealthComponentDto("Equipment", 15, 88.0m, true, null),
            new QualityHealthComponentDto("Competency", 10, null, false, "noData"),
            new QualityHealthComponentDto("ProficiencyTesting", 10, 75.0m, true, null),
            new QualityHealthComponentDto("SupplierQuality", 10, null, false, "notPermitted"),
            new QualityHealthComponentDto("Risk", 10, 50.0m, true, null),
        };
        var rows = new[] { new AnalyticsRowDto("NC-2026-0001", "Balance drift", "=danger()", "Open") };
        return new QualityAnalyticsDto(
            new QualityHealthScoreDto(80.5m, components, 7, 9),
            new DocumentControlStatsDto(40, 37, 92.5m, 1, 2, 1, 0, 55, rows),
            new NcCapaStatsDto(3, 20, 1, 12, 9, 10, 90.0m, 83.3m,
                [new CategoryCountDto("Open", 3), new CategoryCountDto("Closed", 17)],
                [new CategoryCountDto("Internal", 12), new CategoryCountDto("Audit", 8)],
                [new CategoryCountDto("Chemistry", 11)], rows),
            new ComplaintsStatsDto(2, 15, 12, 13, 92.3m, 4.5m,
                [new CategoryCountDto("Email", 9), new CategoryCountDto("Phone", 6)], rows),
            new AuditStatsDto(4, 6, 66.7m, 1, 3, 5, rows),
            new EquipmentStatsDto(30, 27, 90.0m, 1, 96.7m, 3,
                [new CategoryCountDto("In service", 27), new CategoryCountDto("OOS", 1)], rows),
            new CompetencyStatsDto(18, 22, 81.8m, 3, 1, 2, rows),
            new PtStatsDto(9, 2, 1, 1, 13, 75.0m, rows),
            new SupplierStatsDto(14, 18, 77.8m, 1, 4.2m, rows),
            new RiskStatsDto(5, 40, 3, 60.0m, 2,
                [new RiskMatrixCellDto(5, 5, 2), new RiskMatrixCellDto(3, 4, 5), new RiskMatrixCellDto(1, 1, 1)],
                rows),
            new QualityAnalyticsScopeDto(Guid.NewGuid(), null, true, ["documentControl"], []),
            At);
    }

    private static QualityAnalyticsDto EmptyDto()
    {
        var components = new[] { "DocumentControl", "NonconformanceCapa", "Complaints", "InternalAudit",
            "Equipment", "Competency", "ProficiencyTesting", "SupplierQuality", "Risk" }
            .Select(c => new QualityHealthComponentDto(c, 10, null, false, "noData")).ToArray();
        return new QualityAnalyticsDto(
            new QualityHealthScoreDto(null, components, 0, 9),
            null, null, null, null, null, null, null, null, null,
            new QualityAnalyticsScopeDto(null, null, false, [], []),
            At);
    }

    [Theory]
    [MemberData(nameof(AnalyticsCases))]
    public void Analytics_report_pdf_is_a_genuine_pdf(QualityAnalyticsDto dto)
    {
        var bytes = new ExportService().ToQualityAnalyticsReportPdf(AnalyticsPack(dto));

        bytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes[..5]).Should().Be("%PDF-");
    }

    [Theory]
    [MemberData(nameof(AnalyticsCases))]
    public void Analytics_report_xlsx_is_a_genuine_workbook(QualityAnalyticsDto dto)
    {
        var bytes = new ExportService().ToQualityAnalyticsReportXlsx(AnalyticsPack(dto));

        bytes.Length.Should().BeGreaterThan(1000);
        bytes[..4].Should().Equal(0x50, 0x4B, 0x03, 0x04);
    }

    public static TheoryData<QualityAnalyticsDto> AnalyticsCases() => new() { FullDto(), EmptyDto() };

    // ── User Manual report (URS-131) ────────────────────────────────────────────
    // The manual PDF uses section cross-references (a linked TOC + page numbers),
    // which QuestPDF only resolves at GeneratePdf(); a full render is the proof
    // they resolve. Exercised with topics that have steps and topics that have none.

    [Fact]
    public void Manual_pdf_is_a_genuine_pdf_document()
    {
        var pack = new ManualExportPack(
            "Demo Laboratory", "QM Tester", At, "en",
            [
                new ManualGroupDto("Overview",
                [
                    new ManualTopicDto("/dashboard", "Dashboard", "Live quality KPIs at a glance.",
                        [], ["Open the dashboard from the sidebar.", "Review the KPI strip."]),
                    new ManualTopicDto("/nonconformances", "NC & CAPA", "Raise and work nonconformances.",
                    [
                        new ManualStepDto("Raise", "Capture the event and assess severity."),
                        new ManualStepDto("Investigate", "Record the root-cause analysis."),
                        new ManualStepDto("Verify", "Sign off the corrective action."),
                    ],
                    ["Click Raise NC.", "Complete the CAPA actions.", "Verify effectiveness."]),
                ]),
                new ManualGroupDto("Administration",
                [
                    new ManualTopicDto("/users", "Users", "Manage tenant users and roles.", [], []),
                ]),
            ]);

        var bytes = new ExportService().ToManualPdf(pack);

        bytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes[..5]).Should().Be("%PDF-");
    }
}
