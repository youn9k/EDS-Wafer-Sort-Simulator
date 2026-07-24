using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MColor = MigraDoc.DocumentObjectModel.Color;

namespace RecipeTestProject;

public sealed class ReportService
{
    public Task<string> GenerateAsync(
        InspectionJob job,
        JobRunResult result,
        string outputPath)
    {
        if (result.Status != JobStatus.Completed || result.CompletedCount != 25)
            throw new InvalidOperationException("25장 EDS가 완료된 Run만 PDF 보고서를 생성할 수 있습니다.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var document = BuildDocument(job, result);
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(outputPath);
        return Task.FromResult(outputPath);
    }

    private static Document BuildDocument(InspectionJob job, JobRunResult result)
    {
        var document = new Document();
        document.Info.Title = $"EDS Lot Report - {job.LotId}";
        document.Info.Subject = "Electrical Die Sorting lot result";
        document.Info.Author = "EDS Wafer Sort Simulator";
        DefineStyles(document);

        var section = document.AddSection();
        ConfigureSection(section);
        AddHeaderFooter(section, job.JobId);
        AddTitle(section, "EDS Wafer Sort Lot 결과 보고서");
        section.AddParagraph(
            $"보고서 생성일: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}",
            "Caption");
        AddMetadata(section, job, result);
        AddSummary(section, result);
        AddTestSummary(section, result);
        AddWaferTable(section, result);

        foreach (var wafer in result.Wafers.Where(x =>
                     x.Status == WaferExecutionStatus.Completed &&
                     x.Disposition == WaferDisposition.LowYield))
        {
            section.AddPageBreak();
            AddTitle(section, $"{wafer.WaferId} Low Yield 상세");
            section.AddParagraph(
                $"수율 {wafer.YieldPercent:0.00}%  |  PASS {wafer.PassDieCount:N0}  |  FAIL {wafer.ValidDieCount - wafer.PassDieCount:N0}",
                "Subheading");
            AddBinSummary(section, result.RecipeSnapshot, wafer);
            section.AddParagraph("Final Bin Wafer Map", "Subheading");
            AddWaferMap(section, result.RecipeSnapshot, wafer);
            AddLegend(section, result.RecipeSnapshot);
        }
        return document;
    }

    private static void DefineStyles(Document document)
    {
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Malgun Gothic";
        normal.Font.Size = Unit.FromPoint(9);

        var title = document.Styles.AddStyle("ReportTitle", StyleNames.Normal);
        title.Font.Name = "Malgun Gothic";
        title.Font.Size = Unit.FromPoint(21);
        title.Font.Bold = true;
        title.Font.Color = MColor.FromRgb(31, 78, 121);
        title.ParagraphFormat.SpaceAfter = Unit.FromCentimeter(.45);

        var heading = document.Styles.AddStyle("Subheading", StyleNames.Normal);
        heading.Font.Name = "Malgun Gothic";
        heading.Font.Size = Unit.FromPoint(11.5);
        heading.Font.Bold = true;
        heading.Font.Color = MColor.FromRgb(35, 45, 55);
        heading.ParagraphFormat.SpaceBefore = Unit.FromCentimeter(.4);
        heading.ParagraphFormat.SpaceAfter = Unit.FromCentimeter(.18);

        var caption = document.Styles.AddStyle("Caption", StyleNames.Normal);
        caption.Font.Name = "Malgun Gothic";
        caption.Font.Size = Unit.FromPoint(7.5);
        caption.Font.Color = MColor.FromRgb(100, 110, 120);
    }

    private static void ConfigureSection(Section section)
    {
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.55);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.4);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.45);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.45);
    }

    private static void AddHeaderFooter(Section section, string jobId)
    {
        var header = section.Headers.Primary.AddParagraph("EDS WAFER SORT SIMULATOR");
        header.Format.Font.Name = "Malgun Gothic";
        header.Format.Font.Size = Unit.FromPoint(8);
        header.Format.Font.Color = MColor.FromRgb(90, 105, 120);
        header.Format.Alignment = ParagraphAlignment.Right;

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Name = "Malgun Gothic";
        footer.Format.Font.Size = Unit.FromPoint(8);
        footer.Format.Font.Color = MColor.FromRgb(110, 120, 130);
        footer.AddText($"{jobId}  |  ");
        footer.AddPageField();
        footer.AddText(" / ");
        footer.AddNumPagesField();
        footer.Format.Alignment = ParagraphAlignment.Center;
    }

    private static void AddTitle(Section section, string text) =>
        section.AddParagraph(text, "ReportTitle");

    private static void AddMetadata(
        Section section,
        InspectionJob job,
        JobRunResult result)
    {
        section.AddParagraph("Job / Lot 정보", "Subheading");
        var table = section.AddTable();
        ConfigureTable(table);
        table.AddColumn(Unit.FromCentimeter(2.7));
        table.AddColumn(Unit.FromCentimeter(6.05));
        table.AddColumn(Unit.FromCentimeter(2.7));
        table.AddColumn(Unit.FromCentimeter(6.05));
        AddMetaRow(table, "고객명", job.CustomerName, "의뢰번호", job.RequestNumber);
        AddMetaRow(table, "Lot ID", job.LotId, "Job ID", job.JobId);
        AddMetaRow(
            table,
            "Product",
            $"{job.ProductSnapshot.Name} ({job.ProductSnapshot.ProductId})",
            "Wafer / Die",
            $"{job.ProductSnapshot.WaferDiameterMm} mm / {job.ProductSnapshot.DieWidthMm:0.##}×{job.ProductSnapshot.DieHeightMm:0.##} mm");
        AddMetaRow(
            table,
            "Recipe",
            $"{job.RecipeSnapshot.Name} v{job.RecipeSnapshot.Version}",
            "합격 기준",
            $"{job.ProductSnapshot.AcceptanceYieldPercent:0.##}%");
        AddMetaRow(
            table,
            "Test Cell",
            $"{result.TestCellSnapshot.Name} ({result.TestCellSnapshot.Id})",
            "Line",
            result.TestCellSnapshot.Line);
        AddMetaRow(
            table,
            "Tester",
            $"{result.TestCellSnapshot.Tester.Manufacturer} {result.TestCellSnapshot.Tester.Model}",
            "Prober",
            $"{result.TestCellSnapshot.Prober.Manufacturer} {result.TestCellSnapshot.Prober.Model}");
        AddMetaRow(
            table,
            "Probe Card",
            result.TestCellSnapshot.ProbeCard.Name,
            "실행 시간",
            $"{result.StartedAt:yyyy-MM-dd HH:mm:ss} ~ {result.FinishedAt:yyyy-MM-dd HH:mm:ss}");
    }

    private static void AddMetaRow(
        Table table,
        string leftLabel,
        string leftValue,
        string rightLabel,
        string rightValue)
    {
        var row = table.AddRow();
        row.Height = Unit.FromCentimeter(.66);
        SetCell(row.Cells[0], leftLabel, true);
        SetCell(row.Cells[1], leftValue, false);
        SetCell(row.Cells[2], rightLabel, true);
        SetCell(row.Cells[3], rightValue, false);
    }

    private static void AddSummary(Section section, JobRunResult result)
    {
        section.AddParagraph("Lot 요약", "Subheading");
        var table = section.AddTable();
        ConfigureTable(table);
        for (var index = 0; index < 5; index++)
            table.AddColumn(Unit.FromCentimeter(3.5));
        var labels = table.AddRow();
        var values = table.AddRow();
        var items = new[]
        {
            ("Lot 수율", $"{result.LotYieldPercent:0.00}%"),
            ("Passed Wafer", $"{result.PassedWaferCount} / 25"),
            ("Low Yield", $"{result.LowYieldWaferCount} / 25"),
            ("PASS die", $"{result.PassDieCount:N0}"),
            ("FAIL die", $"{result.FailDieCount:N0}")
        };
        for (var index = 0; index < items.Length; index++)
        {
            SetCell(labels.Cells[index], items[index].Item1, true);
            SetCell(values.Cells[index], items[index].Item2, false);
            values.Cells[index].Format.Alignment = ParagraphAlignment.Center;
        }
    }

    private static void AddTestSummary(Section section, JobRunResult result)
    {
        section.AddParagraph("Test 요약", "Subheading");
        var table = section.AddTable();
        ConfigureTable(table);
        var widths = new[] { 1.0, 6.0, 4.3, 2.2, 2.2, 1.8 };
        foreach (var width in widths) table.AddColumn(Unit.FromCentimeter(width));
        var header = table.AddRow();
        header.HeadingFormat = true;
        var titles = new[] { "#", "Recipe 단계", "Final Bin", "실패 die", "실패율", "Wafer" };
        for (var index = 0; index < titles.Length; index++)
            SetCell(header.Cells[index], titles[index], true);

        var total = Math.Max(1, result.Wafers.Sum(x => x.ValidDieCount));
        foreach (var step in result.RecipeSnapshot.Steps)
        {
            var bins = result.RecipeSnapshot.FailBins
                .Where(x => string.Equals(x.RelatedStepId, step.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var codes = bins.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fail = result.Wafers.Sum(w =>
                w.Dies.Count(d => d.IsValid && codes.Contains(d.FinalBinCode)));
            var wafers = result.Wafers.Count(w =>
                w.Dies.Any(d => d.IsValid && codes.Contains(d.FinalBinCode)));
            var row = table.AddRow();
            var values = new[]
            {
                step.Sequence.ToString(),
                step.Name,
                bins.Count == 0 ? "-" : string.Join(", ", bins.Select(x => x.Code)),
                fail.ToString("N0"),
                $"{fail * 100d / total:0.000}%",
                wafers.ToString()
            };
            for (var index = 0; index < values.Length; index++)
                SetCell(row.Cells[index], values[index], false);
        }
    }

    private static void AddWaferTable(Section section, JobRunResult result)
    {
        section.AddParagraph("25장 Wafer 결과", "Subheading");
        var table = section.AddTable();
        ConfigureTable(table);
        var widths = new[] { 2.4, 3.1, 2.5, 3.0, 3.0, 3.5 };
        foreach (var width in widths) table.AddColumn(Unit.FromCentimeter(width));
        var header = table.AddRow();
        header.HeadingFormat = true;
        var titles = new[] { "Wafer", "판정", "수율", "PASS die", "FAIL die", "주요 실패 Bin" };
        for (var index = 0; index < titles.Length; index++)
            SetCell(header.Cells[index], titles[index], true);

        foreach (var wafer in result.Wafers)
        {
            var row = table.AddRow();
            var main = wafer.BinCounts
                .Where(x => !string.Equals(
                    x.Key,
                    result.RecipeSnapshot.PassBin.Code,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .FirstOrDefault() ?? "-";
            var values = new[]
            {
                wafer.WaferId,
                wafer.Disposition == WaferDisposition.Passed ? "Passed" : "Low Yield",
                $"{wafer.YieldPercent:0.00}%",
                wafer.PassDieCount.ToString("N0"),
                (wafer.ValidDieCount - wafer.PassDieCount).ToString("N0"),
                main
            };
            for (var index = 0; index < values.Length; index++)
                SetCell(row.Cells[index], values[index], false);
            if (wafer.Disposition == WaferDisposition.LowYield)
                row.Shading.Color = MColor.FromRgb(255, 239, 239);
        }
    }

    private static void AddBinSummary(
        Section section,
        RecipeDocument recipe,
        WaferResult wafer)
    {
        var table = section.AddTable();
        ConfigureTable(table);
        foreach (var _ in recipe.FinalBins)
            table.AddColumn(Unit.FromCentimeter(17.5 / recipe.FinalBins.Count));
        var labels = table.AddRow();
        var values = table.AddRow();
        for (var index = 0; index < recipe.FinalBins.Count; index++)
        {
            var bin = recipe.FinalBins[index];
            SetCell(labels.Cells[index], bin.Code, true);
            SetCell(values.Cells[index], wafer.BinCounts.GetValueOrDefault(bin.Code).ToString("N0"), false);
            labels.Cells[index].Shading.Color = ParseColor(bin.ColorHex);
        }
    }

    private static void AddWaferMap(
        Section section,
        RecipeDocument recipe,
        WaferResult wafer)
    {
        var lookup = wafer.Dies.ToDictionary(x => (x.Row, x.Column));
        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(.12);
        table.Borders.Color = MColor.FromRgb(230, 234, 238);
        table.TopPadding = Unit.Zero;
        table.BottomPadding = Unit.Zero;
        table.LeftPadding = Unit.Zero;
        table.RightPadding = Unit.Zero;
        var cellSize = Math.Min(.48, 16.8 / Math.Max(wafer.GridRows, wafer.GridColumns));
        for (var column = 0; column < wafer.GridColumns; column++)
            table.AddColumn(Unit.FromCentimeter(cellSize));
        var colors = recipe.FinalBins.ToDictionary(
            x => x.Code,
            x => ParseColor(x.ColorHex),
            StringComparer.OrdinalIgnoreCase);
        for (var rowIndex = 0; rowIndex < wafer.GridRows; rowIndex++)
        {
            var row = table.AddRow();
            row.Height = Unit.FromCentimeter(cellSize);
            row.HeightRule = RowHeightRule.Exactly;
            for (var column = 0; column < wafer.GridColumns; column++)
            {
                var die = lookup[(rowIndex, column)];
                row.Cells[column].Shading.Color = !die.IsValid
                    ? Colors.White
                    : colors.GetValueOrDefault(die.FinalBinCode, MColor.FromRgb(160, 160, 160));
            }
        }
    }

    private static void AddLegend(Section section, RecipeDocument recipe)
    {
        var paragraph = section.AddParagraph();
        paragraph.Style = "Caption";
        paragraph.AddText("범례: ");
        paragraph.AddText(string.Join(
            "  |  ",
            recipe.FinalBins.Select(x => $"{x.Code} ({x.DisplayName})")));
    }

    private static void ConfigureTable(Table table)
    {
        table.Borders.Width = Unit.FromPoint(.35);
        table.Borders.Color = MColor.FromRgb(205, 215, 225);
        table.TopPadding = Unit.FromPoint(2);
        table.BottomPadding = Unit.FromPoint(2);
        table.LeftPadding = Unit.FromPoint(3);
        table.RightPadding = Unit.FromPoint(3);
    }

    private static void SetCell(Cell cell, string text, bool label)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        if (label) cell.Shading.Color = MColor.FromRgb(238, 243, 248);
        var paragraph = cell.AddParagraph(text);
        paragraph.Format.Font.Name = "Malgun Gothic";
        paragraph.Format.Font.Size = Unit.FromPoint(7.8);
        paragraph.Format.Font.Bold = label;
    }

    private static MColor ParseColor(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#') return MColor.FromRgb(160, 160, 160);
        return MColor.FromRgb(
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16));
    }
}
