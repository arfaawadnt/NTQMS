using System.Globalization;
using ClosedXML.Excel;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NT.QAMS.Infrastructure.Exports;

/// <summary>
/// The comprehensive Quality Analytics report renderer (PDF + XLSX). Kept in its
/// own partial so the generic register/page exporters stay small. Draws the
/// composite Quality Health Score as a gauge, its weighted components as progress
/// bars, and each sub-system as KPI tiles, Pareto bars and a 5×5 risk heat-matrix —
/// all in the brand palette shared with the SPA, with the Part 11 §11.10(b)
/// provenance stamp on every page. Every figure is the one the dashboard computed;
/// this is a copy of the analytics, not a second computation.
/// </summary>
public sealed partial class ExportService
{
    private const string ReportTitle = "Quality Analytics Report";

    /// <summary>Human labels for the nine composite-score categories (enum names off the wire).</summary>
    private static string CategoryLabel(string category) => category switch
    {
        "DocumentControl" => "Document Control",
        "NonconformanceCapa" => "NC & CAPA",
        "Complaints" => "Complaints",
        "InternalAudit" => "Internal Audit",
        "Equipment" => "Equipment & Calibration",
        "Competency" => "Competency",
        "ProficiencyTesting" => "Proficiency Testing",
        "SupplierQuality" => "Supplier Quality",
        "Risk" => "Risk",
        _ => category,
    };

    /// <summary>The band a 0–100 score falls in: colour is paired with a word, never the sole carrier of meaning.</summary>
    private static (string Color, string Label) ScoreBand(decimal? score) => score switch
    {
        null => (InkFor(null), "No data"),
        >= 90 => (InkFor("green"), "Strong"),
        >= 75 => (InkFor("teal"), "Healthy"),
        >= 50 => (InkFor("gold"), "Watch"),
        _ => (InkFor("red"), "At risk"),
    };

    private static string Pct(decimal? v) =>
        v is null ? "—" : v.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%";

    private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

    // ── PDF ───────────────────────────────────────────────────────────────────

    public byte[] ToQualityAnalyticsReportPdf(QualityAnalyticsReportPack pack)
    {
        var a = pack.Analytics;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(t => t.FontSize(9).FontColor(Slate));

            page.Header().Column(header =>
            {
                header.Item().BorderBottom(3).BorderColor(Blue).PaddingBottom(8).Row(band =>
                {
                    band.RelativeItem().Column(left =>
                    {
                        left.Item().Text(ReportTitle).Bold().FontSize(18).FontColor(Navy);
                        left.Item().PaddingTop(2).Text(pack.TenantName).FontSize(10).FontColor(Teal).Bold();
                    });
                    band.ConstantItem(210).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text($"{pack.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC")
                            .FontSize(8.5f).FontColor("#666666");
                        right.Item().AlignRight().Text(pack.GeneratedBy).FontSize(8.5f).FontColor("#666666");
                        right.Item().AlignRight().Text($"Computed {a.ComputedAtUtc:yyyy-MM-dd HH:mm} UTC")
                            .FontSize(7.5f).FontColor("#999999");
                    });
                });
                if (!string.IsNullOrWhiteSpace(pack.FiltersSummary))
                {
                    header.Item().PaddingTop(6).Text($"Scope: {pack.FiltersSummary}")
                        .FontSize(8.5f).Italic().FontColor("#666666");
                }
            });

            page.Content().PaddingTop(12).Column(content =>
            {
                HealthScoreBlock(content.Item(), a.Health);

                if (a.Scope.UnscopedSections.Count > 0)
                {
                    content.Item().PaddingTop(6).Text(
                            "Sections not narrowed by the branch/department filter (records carry no organisational attribution): "
                            + string.Join(", ", a.Scope.UnscopedSections))
                        .FontSize(7.5f).Italic().FontColor("#8A5A00");
                }

                if (a.DocumentControl is { } dc)
                {
                    Section(content.Item(), "Document Control", "ISO 17025 §8.3", new[]
                    {
                        ("value", Pct(dc.PercentCurrent), "Current", "teal"),
                        ("value", $"{Num(dc.Current)}/{Num(dc.TotalActive)}", "Current / active", "blue"),
                        ("value", Num(dc.OverdueReviews), "Overdue reviews", dc.OverdueReviews > 0 ? "red" : "green"),
                        ("value", Num(dc.AcknowledgementsRecorded), "Acknowledgements", "slate"),
                    });
                    Bars(content.Item(), "Review-due horizon", new[]
                    {
                        new CategoryCountDto("≤30 days", dc.DueWithin30),
                        new CategoryCountDto("31–60 days", dc.Due31To60),
                        new CategoryCountDto("61–90 days", dc.Due61To90),
                    });
                }

                if (a.NcCapa is { } nc)
                {
                    Section(content.Item(), "Nonconformances & CAPA", "ISO 17025 §8.7", new[]
                    {
                        ("value", Num(nc.OpenNcs), "Open NCs", nc.OpenNcs > 0 ? "orange" : "green"),
                        ("value", Num(nc.OverdueCapa), "Overdue CAPA", nc.OverdueCapa > 0 ? "red" : "green"),
                        ("value", Pct(nc.CapaOnSchedulePercent), "CAPA on schedule", "teal"),
                        ("value", Pct(nc.CapaOnTimePercent), "CAPA on-time (closed)", "blue"),
                    });
                    Bars(content.Item(), "By source", nc.BySource);
                }

                if (a.Complaints is { } cp)
                {
                    Section(content.Item(), "Complaints", "ISO 17025 §7.9", new[]
                    {
                        ("value", Num(cp.Open), "Open", cp.Open > 0 ? "orange" : "green"),
                        ("value", Pct(cp.PercentWithinSla), "Resolved within SLA", "teal"),
                        ("value", cp.AverageResolutionDays is null ? "—" : cp.AverageResolutionDays.Value.ToString("0.0", CultureInfo.InvariantCulture) + "d", "Avg resolution", "blue"),
                        ("value", Num(cp.Total), "Total", "slate"),
                    });
                    Bars(content.Item(), "By channel", cp.ByChannel);
                }

                if (a.Audits is { } au)
                {
                    Section(content.Item(), "Internal Audit", "ISO 17025 §8.8", new[]
                    {
                        ("value", Pct(au.PlanCompletionPercent), "Plan completion", "teal"),
                        ("value", Num(au.MajorFindings), "Major findings", au.MajorFindings > 0 ? "red" : "green"),
                        ("value", Num(au.MinorFindings), "Minor findings", au.MinorFindings > 0 ? "gold" : "green"),
                        ("value", Num(au.Observations), "Observations", "slate"),
                    });
                }

                if (a.Equipment is { } eq)
                {
                    Section(content.Item(), "Equipment & Calibration", "ISO 17025 §6.4", new[]
                    {
                        ("value", Pct(eq.CalibrationCompliancePercent), "Calibration current", "teal"),
                        ("value", Pct(eq.AvailabilityPercent), "Availability", "blue"),
                        ("value", Num(eq.OverdueCalibration), "Overdue calibration", eq.OverdueCalibration > 0 ? "red" : "green"),
                        ("value", Num(eq.OutOfService), "Out of service", eq.OutOfService > 0 ? "orange" : "green"),
                    });
                    Bars(content.Item(), "By status", eq.ByStatus);
                }

                if (a.Competency is { } co)
                {
                    Section(content.Item(), "Competency", "ISO 17025 §6.2", new[]
                    {
                        ("value", Pct(co.PercentCompetent), "Authorized", "teal"),
                        ("value", Num(co.ExpiringWithin90), "Expiring ≤90d", co.ExpiringWithin90 > 0 ? "gold" : "green"),
                        ("value", Num(co.PendingTraining), "Pending training", "blue"),
                        ("value", Num(co.Revoked), "Revoked", co.Revoked > 0 ? "orange" : "green"),
                    });
                }

                if (a.ProficiencyTesting is { } pt)
                {
                    Section(content.Item(), "Proficiency Testing", "ISO 17025 §7.7", new[]
                    {
                        ("value", Pct(pt.SatisfactionRatePercent), "Satisfaction rate", "teal"),
                        ("value", Num(pt.Unsatisfactory), "Unsatisfactory", pt.Unsatisfactory > 0 ? "red" : "green"),
                        ("value", Num(pt.Questionable), "Questionable", pt.Questionable > 0 ? "gold" : "green"),
                        ("value", Num(pt.Pending), "Pending", "slate"),
                    });
                    Bars(content.Item(), "Outcomes", new[]
                    {
                        new CategoryCountDto("Satisfactory", pt.Satisfactory),
                        new CategoryCountDto("Questionable", pt.Questionable),
                        new CategoryCountDto("Unsatisfactory", pt.Unsatisfactory),
                    });
                }

                if (a.Suppliers is { } su)
                {
                    Section(content.Item(), "Supplier Quality", "ISO 17025 §6.6", new[]
                    {
                        ("value", Pct(su.ApprovedPercent), "Approved", "teal"),
                        ("value", Num(su.Suspended), "Suspended", su.Suspended > 0 ? "red" : "green"),
                        ("value", su.AverageEvaluationScore is null ? "—" : su.AverageEvaluationScore.Value.ToString("0.0", CultureInfo.InvariantCulture), "Avg score", "blue"),
                        ("value", Num(su.Total), "Total", "slate"),
                    });
                }

                if (a.Risk is { } rk)
                {
                    Section(content.Item(), "Risk & Opportunity", "ISO 17025 §8.5", new[]
                    {
                        ("value", Num(rk.HighOrExtreme), "High / extreme", rk.HighOrExtreme > 0 ? "orange" : "green"),
                        ("value", Pct(rk.HighMitigatedPercent), "High mitigated", "teal"),
                        ("value", Num(rk.OverdueTreatments), "Overdue treatments", rk.OverdueTreatments > 0 ? "red" : "green"),
                        ("value", Num(rk.Total), "Total", "slate"),
                    });
                    RiskMatrix(content.Item(), rk.Matrix);
                }
            });

            page.Footer().BorderTop(0.8f).BorderColor(GreyBorder).PaddingTop(5).Row(footer =>
            {
                footer.RelativeItem().Text(
                        $"Accurate and complete copy of the quality analytics as visible to {pack.GeneratedBy} · " +
                        "21 CFR Part 11 §11.10(b) · NT.QAMS")
                    .FontSize(7.5f).FontColor("#797979");
                footer.ConstantItem(80).AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor("#797979"));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    /// <summary>The composite score: a large figure, a coloured band gauge, and every weighted component as a progress bar.</summary>
    private static void HealthScoreBlock(IContainer container, QualityHealthScoreDto health)
    {
        var (color, label) = ScoreBand(health.Score);
        container.Border(0.8f).BorderColor(GreyBorder).Background(GreyBg).Padding(12).Column(col =>
        {
            col.Item().Text("Quality Health Score").Bold().FontSize(12).FontColor(Navy);
            col.Item().PaddingTop(6).Row(row =>
            {
                row.ConstantItem(120).Column(score =>
                {
                    score.Item().Text(health.Score is null ? "—" : health.Score.Value.ToString("0.0", CultureInfo.InvariantCulture))
                        .Bold().FontSize(34).FontColor(color);
                    score.Item().Text($"out of 100 · {label}").FontSize(8).FontColor("#666666");
                    score.Item().Text($"{health.ContributingCategories} of {health.TotalCategories} categories contributing")
                        .FontSize(7.5f).FontColor("#999999");
                });
                row.RelativeItem().PaddingLeft(10).AlignMiddle().Column(g =>
                {
                    Gauge(g.Item(), health.Score, color);
                    g.Item().PaddingTop(3).Row(ticks =>
                    {
                        ticks.RelativeItem().Text("0").FontSize(6.5f).FontColor("#999999");
                        ticks.RelativeItem().AlignCenter().Text("50").FontSize(6.5f).FontColor("#999999");
                        ticks.RelativeItem().AlignRight().Text("100").FontSize(6.5f).FontColor("#999999");
                    });
                });
            });

            col.Item().PaddingTop(10).Text("Weighted components").Bold().FontSize(9.5f).FontColor(Slate);
            col.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(150);
                    c.ConstantColumn(42);
                    c.RelativeColumn();
                    c.ConstantColumn(120);
                });
                t.Header(h =>
                {
                    h.Cell().Element(HeaderCell).Text("Category");
                    h.Cell().Element(HeaderCell).Text("Weight");
                    h.Cell().Element(HeaderCell).Text("Achieved");
                    h.Cell().Element(HeaderCell).Text("Status");
                });
                foreach (var c in health.Components)
                {
                    t.Cell().Element(BodyCell).Text(CategoryLabel(c.Category));
                    t.Cell().Element(BodyCell).Text(c.Weight.ToString(CultureInfo.InvariantCulture));
                    t.Cell().Element(BodyCell).PaddingVertical(3).Column(bar =>
                    {
                        Gauge(bar.Item(), c.Contributed ? c.AchievedScore : null,
                            c.Contributed ? InkFor("blue") : GreyBorder);
                    });
                    t.Cell().Element(BodyCell).Text(
                        c.Contributed ? Pct(c.AchievedScore)
                        : c.ExcludedReason switch
                        {
                            "notPermitted" => "Not permitted",
                            "noData" => "No data",
                            "zeroWeight" => "Zero weight",
                            _ => "Excluded",
                        }).FontColor(c.Contributed ? Slate : "#999999");
                }
            });
        });
    }

    /// <summary>A 0–100 proportional band. A null score renders as an empty track (never as zero).</summary>
    private static void Gauge(IContainer container, decimal? score, string color)
    {
        var pct = (int)Math.Round(Math.Clamp(score ?? 0m, 0m, 100m), MidpointRounding.AwayFromZero);
        container.Height(14).Background(GreyBg).Border(0.5f).BorderColor(GreyBorder).Row(r =>
        {
            if (pct > 0) { r.RelativeItem(pct).Background(color); }
            if (pct < 100) { r.RelativeItem(100 - pct); }
        });
    }

    /// <summary>A section header, then its KPI tiles (value in readable ink over a caption).</summary>
    private static void Section(
        IContainer container, string title, string clause,
        (string _, string Value, string Label, string Tone)[] tiles)
    {
        container.PaddingTop(14).Column(col =>
        {
            col.Item().BorderBottom(1.5f).BorderColor(Teal).PaddingBottom(3).Row(h =>
            {
                h.RelativeItem().Text(title).Bold().FontSize(12).FontColor(Navy);
                h.ConstantItem(120).AlignRight().Text(clause).FontSize(8).Italic().FontColor("#999999");
            });
            col.Item().PaddingTop(6).Row(row =>
            {
                foreach (var (_, value, label, tone) in tiles)
                {
                    row.RelativeItem().Padding(2).Border(0.8f).BorderColor(GreyBorder)
                        .Background(GreyBg).Padding(7).Column(tile =>
                        {
                            tile.Item().Text(value).Bold().FontSize(15).FontColor(InkFor(tone));
                            tile.Item().PaddingTop(1).Text(label).FontSize(7.2f).FontColor("#666666");
                        });
                }
            });
        });
    }

    /// <summary>A labelled horizontal bar chart (Pareto-style), each bar proportional to the row's share of the max.</summary>
    private static void Bars(IContainer container, string title, IReadOnlyList<CategoryCountDto> rows)
    {
        if (rows.Count == 0) { return; }
        var max = Math.Max(1, rows.Max(r => r.Count));
        container.PaddingTop(6).Column(col =>
        {
            col.Item().Text(title).FontSize(8.5f).Bold().FontColor(Slate);
            foreach (var r in rows.OrderByDescending(x => x.Count))
            {
                var pct = (int)Math.Round(100.0 * r.Count / max, MidpointRounding.AwayFromZero);
                col.Item().PaddingTop(3).Row(row =>
                {
                    row.ConstantItem(120).Text(r.Label).FontSize(8);
                    row.RelativeItem().AlignMiddle().Height(11).Row(bar =>
                    {
                        if (pct > 0) { bar.RelativeItem(pct).Background(InkFor("blue")); }
                        if (pct < 100) { bar.RelativeItem(100 - pct).Background(GreyBg); }
                    });
                    row.ConstantItem(38).AlignRight().Text(Num(r.Count)).FontSize(8).Bold();
                });
            }
        });
    }

    /// <summary>The 5×5 likelihood × impact heat-matrix: impact rises up the rows, likelihood across the columns; the count sits in each cell over a heat tint that is always paired with the number.</summary>
    private static void RiskMatrix(IContainer container, IReadOnlyList<RiskMatrixCellDto> cells)
    {
        int CountAt(int likelihood, int impact) =>
            cells.FirstOrDefault(c => c.Likelihood == likelihood && c.Impact == impact)?.Count ?? 0;
        var max = Math.Max(1, cells.Count == 0 ? 1 : cells.Max(c => c.Count));

        string Heat(int count)
        {
            if (count == 0) { return "#FFFFFF"; }
            var ratio = (double)count / max;
            return ratio switch { >= 0.66 => "#F3C9C1", >= 0.33 => "#FBE3C4", _ => "#E7F0DC" };
        }

        container.PaddingTop(8).Column(col =>
        {
            col.Item().Text("Risk matrix (likelihood × impact)").FontSize(8.5f).Bold().FontColor(Slate);
            col.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(46);
                    for (var i = 0; i < 5; i++) { c.RelativeColumn(); }
                });
                // Header row: likelihood 1..5.
                t.Cell().Element(MatrixAxis).Text("I \\ L");
                for (var l = 1; l <= 5; l++) { t.Cell().Element(MatrixAxis).Text($"L{l}"); }
                // Impact rows, top = 5.
                for (var impact = 5; impact >= 1; impact--)
                {
                    t.Cell().Element(MatrixAxis).Text($"I{impact}");
                    for (var l = 1; l <= 5; l++)
                    {
                        var count = CountAt(l, impact);
                        t.Cell().Background(Heat(count)).Border(0.5f).BorderColor(GreyBorder)
                            .Padding(6).AlignCenter().AlignMiddle()
                            .Text(count == 0 ? "·" : Num(count))
                            .FontSize(9).Bold().FontColor(count == 0 ? "#CCCCCC" : Slate);
                    }
                }
            });
        });
    }

    private static IContainer HeaderCell(IContainer c) =>
        c.Background(Slate).Padding(4).DefaultTextStyle(t => t.Bold().FontColor("#FFFFFF").FontSize(8.5f));

    private static IContainer BodyCell(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(GreyBorder).Padding(4).DefaultTextStyle(t => t.FontSize(8.5f));

    private static IContainer MatrixAxis(IContainer c) =>
        c.Background(GreyBg).Border(0.5f).BorderColor(GreyBorder).Padding(5).AlignCenter().AlignMiddle()
            .DefaultTextStyle(t => t.Bold().FontSize(8).FontColor("#666666"));

    // ── XLSX ────────────────────────────────────────────────────────────────────

    public byte[] ToQualityAnalyticsReportXlsx(QualityAnalyticsReportPack pack)
    {
        var a = pack.Analytics;
        using var workbook = new XLWorkbook();
        var sheetIndex = 1;

        // Sheet 01 — Health Score summary + weighted components.
        var summary = workbook.Worksheets.Add(SheetName("Health Score", sheetIndex++));
        var row = ProvenanceBand(summary, ReportTitle, pack);
        summary.Cell(row, 1).Value = "Quality Health Score";
        summary.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(13).Font.SetFontColor(XLColor.FromHtml(Navy));
        summary.Cell(row, 2).Value = a.Health.Score is null ? "—" : (double)a.Health.Score.Value;
        summary.Cell(row, 3).Value =
            $"{ScoreBand(a.Health.Score).Label} · {a.Health.ContributingCategories} of {a.Health.TotalCategories} categories contributing";
        row += 2;

        WriteHeader(summary, row, ["Category", "Weight", "Achieved %", "Contributed", "Excluded reason"]);
        row++;
        foreach (var c in a.Health.Components)
        {
            summary.Cell(row, 1).Value = CategoryLabel(c.Category);
            summary.Cell(row, 2).Value = c.Weight;
            summary.Cell(row, 3).Value = c.AchievedScore is null ? "—" : (double)c.AchievedScore.Value;
            summary.Cell(row, 4).Value = c.Contributed ? "Yes" : "No";
            summary.Cell(row, 5).Value = c.ExcludedReason ?? "";
            row++;
        }
        summary.Columns().AdjustToContents();

        // One sheet per sub-system present.
        if (a.DocumentControl is { } dc)
        {
            SectionSheet(workbook, ref sheetIndex, pack, "Document Control",
                [
                    ("% current", Pct(dc.PercentCurrent)), ("Current", Num(dc.Current)), ("Total active", Num(dc.TotalActive)),
                    ("Overdue reviews", Num(dc.OverdueReviews)), ("Due ≤30d", Num(dc.DueWithin30)),
                    ("Due 31–60d", Num(dc.Due31To60)), ("Due 61–90d", Num(dc.Due61To90)),
                    ("Acknowledgements recorded", Num(dc.AcknowledgementsRecorded)),
                ],
                null, dc.UpcomingReviews);
        }
        if (a.NcCapa is { } nc)
        {
            SectionSheet(workbook, ref sheetIndex, pack, "NC & CAPA",
                [
                    ("Open NCs", Num(nc.OpenNcs)), ("Total NCs", Num(nc.TotalNcs)), ("Overdue CAPA", Num(nc.OverdueCapa)),
                    ("Total CAPA", Num(nc.TotalCapa)), ("CAPA on schedule", Pct(nc.CapaOnSchedulePercent)),
                    ("CAPA on-time (closed)", Pct(nc.CapaOnTimePercent)),
                ],
                nc.BySource, nc.Active);
        }
        if (a.Complaints is { } cp)
        {
            SectionSheet(workbook, ref sheetIndex, pack, "Complaints",
                [
                    ("Open", Num(cp.Open)), ("Total", Num(cp.Total)), ("Within SLA", Pct(cp.PercentWithinSla)),
                    ("Avg resolution (days)", cp.AverageResolutionDays?.ToString("0.0", CultureInfo.InvariantCulture) ?? "—"),
                ],
                cp.ByChannel, cp.Active);
        }
        if (a.Audits is { } au)
        {
            SectionSheet(workbook, ref sheetIndex, pack, "Internal Audit",
                [
                    ("Completed", Num(au.Completed)), ("Total planned", Num(au.TotalPlanned)),
                    ("Plan completion", Pct(au.PlanCompletionPercent)), ("Major findings", Num(au.MajorFindings)),
                    ("Minor findings", Num(au.MinorFindings)), ("Observations", Num(au.Observations)),
                ],
                null, au.Recent);
        }
        if (a.Equipment is { } eq)
        {
            SectionSheet(workbook, ref sheetIndex, pack, "Equipment",
                [
                    ("Calibration current", Pct(eq.CalibrationCompliancePercent)), ("Availability", Pct(eq.AvailabilityPercent)),
                    ("Overdue calibration", Num(eq.OverdueCalibration)), ("Out of service", Num(eq.OutOfService)),
                    ("Total", Num(eq.Total)),
                ],
                eq.ByStatus, eq.UpcomingCalibrations);
        }
        if (a.Competency is { } co)
        {
            SectionSheet(workbook, ref sheetIndex, pack, "Competency",
                [
                    ("Authorized", Num(co.Authorized)), ("Total", Num(co.Total)), ("% competent", Pct(co.PercentCompetent)),
                    ("Expiring ≤90d", Num(co.ExpiringWithin90)), ("Pending training", Num(co.PendingTraining)),
                    ("Revoked", Num(co.Revoked)),
                ],
                null, co.Recent);
        }
        if (a.ProficiencyTesting is { } pt)
        {
            SectionSheet(workbook, ref sheetIndex, pack, "Proficiency Testing",
                [
                    ("Satisfaction rate", Pct(pt.SatisfactionRatePercent)), ("Satisfactory", Num(pt.Satisfactory)),
                    ("Questionable", Num(pt.Questionable)), ("Unsatisfactory", Num(pt.Unsatisfactory)),
                    ("Pending", Num(pt.Pending)), ("Total", Num(pt.Total)),
                ],
                null, pt.Recent);
        }
        if (a.Suppliers is { } su)
        {
            SectionSheet(workbook, ref sheetIndex, pack, "Supplier Quality",
                [
                    ("Approved", Num(su.Approved)), ("Total", Num(su.Total)), ("% approved", Pct(su.ApprovedPercent)),
                    ("Suspended", Num(su.Suspended)),
                    ("Avg evaluation score", su.AverageEvaluationScore?.ToString("0.0", CultureInfo.InvariantCulture) ?? "—"),
                ],
                null, su.Recent);
        }
        if (a.Risk is { } rk)
        {
            var riskSheet = workbook.Worksheets.Add(SheetName("Risk", sheetIndex++));
            var r2 = ProvenanceBand(riskSheet, "Risk & Opportunity", pack);
            r2 = WriteKeyValues(riskSheet, r2,
            [
                ("High / extreme", Num(rk.HighOrExtreme)), ("Total", Num(rk.Total)),
                ("High mitigated", Num(rk.HighMitigated)), ("% high mitigated", Pct(rk.HighMitigatedPercent)),
                ("Overdue treatments", Num(rk.OverdueTreatments)),
            ]);
            r2++;
            riskSheet.Cell(r2, 1).Value = "Risk matrix (rows = impact, columns = likelihood)";
            riskSheet.Cell(r2, 1).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml(Slate));
            r2++;
            for (var l = 1; l <= 5; l++) { riskSheet.Cell(r2, l + 1).Value = $"L{l}"; riskSheet.Cell(r2, l + 1).Style.Font.SetBold(); }
            r2++;
            for (var impact = 5; impact >= 1; impact--)
            {
                riskSheet.Cell(r2, 1).Value = $"I{impact}";
                riskSheet.Cell(r2, 1).Style.Font.SetBold();
                for (var l = 1; l <= 5; l++)
                {
                    var count = rk.Matrix.FirstOrDefault(c => c.Likelihood == l && c.Impact == impact)?.Count ?? 0;
                    riskSheet.Cell(r2, l + 1).Value = count;
                }
                r2++;
            }
            r2++;
            WriteRowsTable(riskSheet, r2, "Top risks", rk.Top);
            riskSheet.Columns().AdjustToContents();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static int ProvenanceBand(IXLWorksheet sheet, string title, QualityAnalyticsReportPack pack)
    {
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.FromHtml(Navy));
        sheet.Cell(2, 1).Value =
            $"Tenant: {pack.TenantName} · Generated by {pack.GeneratedBy} at {pack.GeneratedAtUtc:u} · Computed {pack.Analytics.ComputedAtUtc:u}";
        sheet.Cell(2, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.DimGray);
        var row = 3;
        if (!string.IsNullOrWhiteSpace(pack.FiltersSummary))
        {
            sheet.Cell(row, 1).Value = $"Scope: {pack.FiltersSummary}";
            sheet.Cell(row, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.DimGray);
            row++;
        }
        return row + 1;
    }

    private static void SectionSheet(
        XLWorkbook workbook, ref int index, QualityAnalyticsReportPack pack, string title,
        (string Label, string Value)[] kpis,
        IReadOnlyList<CategoryCountDto>? breakdown,
        IReadOnlyList<AnalyticsRowDto> rows)
    {
        var sheet = workbook.Worksheets.Add(SheetName(title, index++));
        var row = ProvenanceBand(sheet, title, pack);
        row = WriteKeyValues(sheet, row, kpis);
        if (breakdown is { Count: > 0 })
        {
            row++;
            WriteHeader(sheet, row, ["Breakdown", "Count"]);
            row++;
            foreach (var b in breakdown)
            {
                sheet.Cell(row, 1).Value = b.Label;
                sheet.Cell(row, 2).Value = b.Count;
                row++;
            }
        }
        row++;
        WriteRowsTable(sheet, row, "Records", rows);
        sheet.Columns().AdjustToContents();
    }

    private static int WriteKeyValues(IXLWorksheet sheet, int row, (string Label, string Value)[] kpis)
    {
        foreach (var (label, value) in kpis)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#666666"));
            var cell = sheet.Cell(row, 2);
            cell.Value = value;
            GuardFormulaLike(cell, value);
            row++;
        }
        return row;
    }

    private static void WriteRowsTable(IXLWorksheet sheet, int row, string caption, IReadOnlyList<AnalyticsRowDto> rows)
    {
        sheet.Cell(row, 1).Value = caption;
        sheet.Cell(row, 1).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml(Slate));
        row++;
        WriteHeader(sheet, row, ["Reference", "Title", "Detail", "Status"]);
        row++;
        foreach (var r in rows)
        {
            sheet.Cell(row, 1).Value = r.Reference; GuardFormulaLike(sheet.Cell(row, 1), r.Reference);
            sheet.Cell(row, 2).Value = r.Title; GuardFormulaLike(sheet.Cell(row, 2), r.Title);
            sheet.Cell(row, 3).Value = r.Detail ?? ""; GuardFormulaLike(sheet.Cell(row, 3), r.Detail ?? "");
            sheet.Cell(row, 4).Value = r.Status;
            row++;
        }
    }

    private static void WriteHeader(IXLWorksheet sheet, int row, string[] headers)
    {
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = sheet.Cell(row, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml(Slate));
            cell.Style.Font.SetFontColor(XLColor.White);
        }
    }
}
