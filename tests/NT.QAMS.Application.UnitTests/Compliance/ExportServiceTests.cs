using FluentAssertions;
using NT.QAMS.Application.Abstractions;
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
}
