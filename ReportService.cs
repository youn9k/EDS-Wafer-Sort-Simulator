using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MColor = MigraDoc.DocumentObjectModel.Color;

namespace RecipeTestProject;

public sealed class ReportService
{
    public Task<string> GenerateAsync(InspectionJob job, JobRunResult result, string outputPath)
    {
        if (result.Status != JobStatus.Completed || result.CompletedCount != 25)
            throw new InvalidOperationException("25장 검사가 완료된 Run만 PDF 보고서를 생성할 수 있습니다.");

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
        document.Info.Title = $"Lot Inspection Report - {job.LotId}";
        document.Info.Subject = "Wafer inspection lot result";
        document.Info.Author = "WAFER INSPECT TEST CENTER";
        DefineStyles(document);

        var section = document.AddSection();
        ConfigureSection(section);
        AddHeaderFooter(section, job.JobId);
        AddTitle(section, "웨이퍼 Lot 검사 결과 보고서");
        section.AddParagraph($"보고서 생성일: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}", "Caption");
        AddMetadata(section, job, result);
        AddSummary(section, result);
        AddWaferTable(section, result);

        foreach (var wafer in result.Wafers.Where(x => x.Status == WaferExecutionStatus.Ng))
        {
            section.AddPageBreak();
            var appendix = section;
            AddTitle(appendix, $"{wafer.WaferId} NG 상세");
            appendix.AddParagraph(
                $"수율 {wafer.YieldPercent:0.00}%  |  결함 수준 {LevelText(wafer.DefectLevel)}  |  결함 {wafer.Defects.Count}개",
                "Subheading");
            AddDefectSummary(appendix, wafer);
            appendix.AddParagraph("웨이퍼 맵", "Subheading");
            AddWaferMap(appendix, wafer);
            appendix.AddParagraph("범례: 초록 = 정상 다이, 빨강 = 불량 다이, 흰색 = 웨이퍼 외부", "Caption");
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
        title.Font.Size = Unit.FromPoint(22);
        title.Font.Bold = true;
        title.Font.Color = MColor.FromRgb(31, 78, 121);
        title.ParagraphFormat.SpaceAfter = Unit.FromCentimeter(.5);

        var heading = document.Styles.AddStyle("Subheading", StyleNames.Normal);
        heading.Font.Name = "Malgun Gothic";
        heading.Font.Size = Unit.FromPoint(12);
        heading.Font.Bold = true;
        heading.Font.Color = MColor.FromRgb(35, 45, 55);
        heading.ParagraphFormat.SpaceBefore = Unit.FromCentimeter(.45);
        heading.ParagraphFormat.SpaceAfter = Unit.FromCentimeter(.2);

        var caption = document.Styles.AddStyle("Caption", StyleNames.Normal);
        caption.Font.Name = "Malgun Gothic";
        caption.Font.Size = Unit.FromPoint(8);
        caption.Font.Color = MColor.FromRgb(100, 110, 120);
    }

    private static void ConfigureSection(Section section)
    {
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.7);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.6);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.6);
    }

    private static void AddHeaderFooter(Section section, string jobId)
    {
        var header = section.Headers.Primary.AddParagraph("WAFER INSPECT TEST CENTER");
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

    private static void AddTitle(Section section, string text) => section.AddParagraph(text, "ReportTitle");

    private static void AddMetadata(Section section, InspectionJob job, JobRunResult result)
    {
        section.AddParagraph("검사 정보", "Subheading");
        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(.4);
        table.Borders.Color = MColor.FromRgb(205, 215, 225);
        table.AddColumn(Unit.FromCentimeter(3.2));
        table.AddColumn(Unit.FromCentimeter(5.3));
        table.AddColumn(Unit.FromCentimeter(3.2));
        table.AddColumn(Unit.FromCentimeter(5.3));
        AddMetaRow(table, "고객명", job.CustomerName, "의뢰번호", job.RequestNumber);
        AddMetaRow(table, "Lot ID", job.LotId, "Job ID", job.JobId);
        AddMetaRow(table, "제품", $"{job.ProductSnapshot.Name} ({job.ProductSnapshot.ProductId})",
            "합격 수율", $"{job.ProductSnapshot.AcceptanceYieldPercent:0.##}%");
        AddMetaRow(table, "레시피", $"{job.RecipeSnapshot.Name} v{job.RecipeSnapshot.Version}",
            "장비", $"{result.EquipmentName} ({result.EquipmentId})");
        AddMetaRow(table, "시작", result.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            "종료", result.FinishedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-");
    }

    private static void AddMetaRow(Table table, string leftLabel, string leftValue, string rightLabel, string rightValue)
    {
        var row = table.AddRow();
        row.Height = Unit.FromCentimeter(.72);
        SetCell(row.Cells[0], leftLabel, true);
        SetCell(row.Cells[1], leftValue, false);
        SetCell(row.Cells[2], rightLabel, true);
        SetCell(row.Cells[3], rightValue, false);
    }

    private static void SetCell(Cell cell, string text, bool label)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        if (label) cell.Shading.Color = MColor.FromRgb(238, 243, 248);
        var paragraph = cell.AddParagraph(text);
        paragraph.Format.Font.Name = "Malgun Gothic";
        paragraph.Format.Font.Size = Unit.FromPoint(8.5);
        paragraph.Format.Font.Bold = label;
    }

    private static void AddSummary(Section section, JobRunResult result)
    {
        section.AddParagraph("Lot 요약", "Subheading");
        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(.5);
        table.Borders.Color = MColor.FromRgb(205, 215, 225);
        for (var index = 0; index < 4; index++) table.AddColumn(Unit.FromCentimeter(4.25));
        var labels = table.AddRow();
        var values = table.AddRow();
        var items = new[]
        {
            ("Lot 수율", $"{result.LotYieldPercent:0.00}%"),
            ("정상 Wafer", $"{result.NormalCount} / 25"),
            ("NG Wafer", $"{result.NgCount} / 25"),
            ("총 결함", $"{result.TotalDefectCount}개")
        };
        for (var index = 0; index < items.Length; index++)
        {
            SetCell(labels.Cells[index], items[index].Item1, true);
            SetCell(values.Cells[index], items[index].Item2, false);
            values.Cells[index].Format.Alignment = ParagraphAlignment.Center;
        }
    }

    private static void AddWaferTable(Section section, JobRunResult result)
    {
        section.AddParagraph("Wafer별 결과", "Subheading");
        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(.35);
        table.Borders.Color = MColor.FromRgb(210, 218, 226);
        var widths = new[] { 2.5, 2.3, 3.0, 2.8, 3.2, 3.2 };
        foreach (var width in widths) table.AddColumn(Unit.FromCentimeter(width));
        var header = table.AddRow();
        header.HeadingFormat = true;
        var titles = new[] { "Wafer", "판정", "수율", "결함 수준", "불량 다이", "결함 수" };
        for (var index = 0; index < titles.Length; index++) SetCell(header.Cells[index], titles[index], true);

        foreach (var wafer in result.Wafers)
        {
            var row = table.AddRow();
            var values = new[]
            {
                wafer.WaferId,
                wafer.Status == WaferExecutionStatus.Normal ? "정상" : "NG",
                $"{wafer.YieldPercent:0.00}%",
                LevelText(wafer.DefectLevel),
                $"{wafer.ValidDieCount - wafer.PassDieCount}",
                $"{wafer.Defects.Count}"
            };
            for (var index = 0; index < values.Length; index++) SetCell(row.Cells[index], values[index], false);
            if (wafer.Status == WaferExecutionStatus.Ng)
                row.Shading.Color = MColor.FromRgb(255, 239, 239);
        }
    }

    private static void AddDefectSummary(Section section, WaferResult wafer)
    {
        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(.4);
        foreach (var _ in Enumerable.Range(0, 5)) table.AddColumn(Unit.FromCentimeter(3.4));
        var labels = table.AddRow();
        var values = table.AddRow();
        var types = new[] { "Particle", "Scratch", "Pattern", "Edge", "Contamination" };
        for (var index = 0; index < types.Length; index++)
        {
            SetCell(labels.Cells[index], types[index], true);
            SetCell(values.Cells[index], wafer.Defects.Count(x => x.Type == types[index]).ToString(), false);
        }
    }

    private static void AddWaferMap(Section section, WaferResult wafer)
    {
        var lookup = wafer.Dies.ToDictionary(x => (x.Row, x.Column));
        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(.2);
        table.Borders.Color = MColor.FromRgb(225, 230, 235);
        table.TopPadding = Unit.Zero;
        table.BottomPadding = Unit.Zero;
        table.LeftPadding = Unit.Zero;
        table.RightPadding = Unit.Zero;
        for (var column = 0; column < LotTestRunner.GridSize; column++)
            table.AddColumn(Unit.FromCentimeter(.255));

        for (var rowIndex = 0; rowIndex < LotTestRunner.GridSize; rowIndex++)
        {
            var row = table.AddRow();
            row.Height = Unit.FromCentimeter(.255);
            row.HeightRule = RowHeightRule.Exactly;
            for (var column = 0; column < LotTestRunner.GridSize; column++)
            {
                var die = lookup[(rowIndex, column)];
                var cell = row.Cells[column];
                cell.Shading.Color = !die.IsValid
                    ? Colors.White
                    : die.IsPass ? MColor.FromRgb(119, 201, 146) : MColor.FromRgb(224, 96, 96);
            }
        }
    }

    private static string LevelText(DefectLevel? level) => level switch
    {
        DefectLevel.Low => "낮음",
        DefectLevel.Medium => "중간",
        DefectLevel.High => "높음",
        _ => "-"
    };
}
