using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using Ssheet = DocumentFormat.OpenXml.Spreadsheet;

namespace STYS.Raporlar.KonaklamaKisiSayisi.Services;

public class KonaklamaKisiSayisiRaporExcelService : IKonaklamaKisiSayisiRaporExcelService
{
    private const string RenkToplamKolonu = "#DDEBF7";

    private readonly IKonaklamaKisiSayisiRaporService _konaklamaKisiSayisiRaporService;

    public KonaklamaKisiSayisiRaporExcelService(IKonaklamaKisiSayisiRaporService konaklamaKisiSayisiRaporService)
    {
        _konaklamaKisiSayisiRaporService = konaklamaKisiSayisiRaporService;
    }

    public async Task<byte[]> OlusturAsync(
        int tesisId,
        int ay,
        int baslangicYil,
        int bitisYil,
        CancellationToken cancellationToken = default)
    {
        var rapor = await _konaklamaKisiSayisiRaporService.GetRaporAsync(tesisId, ay, baslangicYil, bitisYil, cancellationToken);

        const string sheetName = "Konaklama Kişi Sayısı";
        const int baslikRow = 2;
        const int headerRow = 4;
        const int ilkYilSatiri = 5;
        var sonKolon = 1 + rapor.Odalar.Count;
        var toplamKolon = sonKolon + 1;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(sheetName);

        ws.Range(baslikRow, 1, baslikRow, sonKolon).Merge();
        var baslikCell = ws.Cell(baslikRow, 1);
        baslikCell.Value = rapor.Baslik;
        baslikCell.Style.Font.Bold = true;
        baslikCell.Style.Font.FontSize = 14;
        baslikCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(headerRow, 1).Value = "YIL";
        for (var i = 0; i < rapor.Odalar.Count; i++)
        {
            ws.Cell(headerRow, 2 + i).Value = $"{rapor.Odalar[i].OdaNo} NOLU ODA";
        }
        ws.Cell(headerRow, toplamKolon).Value = "TOPLAM SAYI";

        var headerAraligi = ws.Range(headerRow, 1, headerRow, toplamKolon);
        headerAraligi.Style.Font.Bold = true;
        headerAraligi.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        headerAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Cell(headerRow, toplamKolon).Style.Fill.BackgroundColor = XLColor.FromHtml(RenkToplamKolonu);

        var satir = ilkYilSatiri;
        foreach (var yilSatiri in rapor.Yillar)
        {
            var yilCell = ws.Cell(satir, 1);
            yilCell.Value = yilSatiri.Yil;
            yilCell.Style.Font.Bold = true;
            yilCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            for (var i = 0; i < yilSatiri.Hucreler.Count; i++)
            {
                var cell = ws.Cell(satir, 2 + i);
                cell.Value = yilSatiri.Hucreler[i].KisiSayisi;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            var toplamCell = ws.Cell(satir, toplamKolon);
            toplamCell.Value = yilSatiri.ToplamKisiSayisi;
            toplamCell.Style.Font.Bold = true;
            toplamCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            toplamCell.Style.Fill.BackgroundColor = XLColor.FromHtml(RenkToplamKolonu);

            satir++;
        }

        var sonSatir = satir - 1;
        var tabloAraligi = ws.Range(headerRow, 1, sonSatir, toplamKolon);
        tabloAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabloAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Column(1).Width = 12;
        for (var i = 0; i < rapor.Odalar.Count; i++)
        {
            ws.Column(2 + i).Width = 18;
        }
        ws.Column(toplamKolon).Width = 16;

        ws.SheetView.Freeze(headerRow, 1);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        // ClosedXML grafik (chart) uretimini desteklemiyor; bu yuzden ClosedXML'in urettigi paket
        // DocumentFormat.OpenXml (Microsoft'un resmi OpenXML SDK'si, ClosedXML'in zaten dolayli
        // bagimliligi) ile yeniden acilip iki grafik gomuluyor.
        EmbedGrafikler(ms, sheetName, headerRow, ilkYilSatiri, sonSatir, sonKolon, toplamKolon);

        return ms.ToArray();
    }

    private static void EmbedGrafikler(
        MemoryStream ms,
        string sheetName,
        int headerRow,
        int ilkYilSatiri,
        int sonSatir,
        int sonKolon,
        int toplamKolon)
    {
        ms.Position = 0;

        using var document = SpreadsheetDocument.Open(ms, true);
        var workbookPart = document.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Ssheet.Sheet>().First(x => x.Name == sheetName);
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);

        var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
        var worksheetDrawing = new Xdr.WorksheetDrawing();

        // ── Grafik 1: Oda Bazli Konaklayan Kisi Sayisi Karsilastirmasi (kategoriler = odalar, seriler = yillar) ──
        var odaBazliChartPart = drawingsPart.AddNewPart<ChartPart>();
        var odaKategorileriFormulu = $"'{sheetName}'!$B${headerRow}:${ColumnLetter(sonKolon)}${headerRow}";
        var odaSeriler = new List<(string AdFormulu, string DegerFormulu)>();
        for (var satir = ilkYilSatiri; satir <= sonSatir; satir++)
        {
            odaSeriler.Add((
                $"'{sheetName}'!$A${satir}",
                $"'{sheetName}'!$B${satir}:${ColumnLetter(sonKolon)}${satir}"));
        }
        odaBazliChartPart.ChartSpace = BuildClusteredColumnChartSpace(
            "Oda Bazlı Konaklayan Kişi Sayısı Karşılaştırması",
            odaKategorileriFormulu,
            odaSeriler);

        var genislikOdaGrafik = Math.Max(8, sonKolon);
        var chart1SatirBaslangic = sonSatir + 3;
        AddGraphicFrameAnchor(
            worksheetDrawing,
            drawingsPart.GetIdOfPart(odaBazliChartPart),
            drawingId: 2,
            drawingName: "OdaBazliGrafik",
            fromCol: 0,
            fromRow: chart1SatirBaslangic - 1,
            toCol: genislikOdaGrafik,
            toRow: chart1SatirBaslangic - 1 + 16);

        // ── Grafik 2 (opsiyonel): Yillara Gore Toplam Konaklayan Kisi Sayisi (kategoriler = yillar, tek seri = TOPLAM SAYI) ──
        var toplamChartPart = drawingsPart.AddNewPart<ChartPart>();
        var yilKategorileriFormulu = $"'{sheetName}'!$A${ilkYilSatiri}:$A${sonSatir}";
        var toplamSeri = new List<(string AdFormulu, string DegerFormulu)>
        {
            (
                $"'{sheetName}'!${ColumnLetter(toplamKolon)}${headerRow}",
                $"'{sheetName}'!${ColumnLetter(toplamKolon)}${ilkYilSatiri}:${ColumnLetter(toplamKolon)}${sonSatir}"
            )
        };
        toplamChartPart.ChartSpace = BuildClusteredColumnChartSpace(
            "Yıllara Göre Toplam Konaklayan Kişi Sayısı",
            yilKategorileriFormulu,
            toplamSeri);

        AddGraphicFrameAnchor(
            worksheetDrawing,
            drawingsPart.GetIdOfPart(toplamChartPart),
            drawingId: 3,
            drawingName: "ToplamGrafik",
            fromCol: genislikOdaGrafik + 1,
            fromRow: chart1SatirBaslangic - 1,
            toCol: genislikOdaGrafik + 7,
            toRow: chart1SatirBaslangic - 1 + 16);

        drawingsPart.WorksheetDrawing = worksheetDrawing;

        AddOrReplaceWorksheetDrawing(worksheetPart, drawingsPart);
        worksheetPart.Worksheet.Save();

        document.Save();
    }

    // Drawing elementi, OpenXML worksheet semasinda belirli bir sirada olmak zorunda (SpreadsheetML CT_Worksheet).
    // Worksheet.Append(...) ile en sona eklemek tableParts/extLst gibi elemanlardan sonraya dusurebilir ve
    // Excel'in dosyayi "bozuk/onarilsin mi" uyarisiyla acmasina yol acar. Bu yuzden dogru pozisyona InsertBefore/
    // InsertAfter ile ekleniyor.
    private static void AddOrReplaceWorksheetDrawing(
        WorksheetPart worksheetPart,
        DrawingsPart drawingsPart)
    {
        var worksheet = worksheetPart.Worksheet;
        var relationshipId = worksheetPart.GetIdOfPart(drawingsPart);

        var existingDrawing = worksheet.Elements<Ssheet.Drawing>().FirstOrDefault();
        if (existingDrawing is not null)
        {
            existingDrawing.Id = relationshipId;
            return;
        }

        var drawing = new Ssheet.Drawing { Id = relationshipId };

        // drawing, tableParts'tan once gelmeli.
        var tableParts = worksheet.Elements<Ssheet.TableParts>().FirstOrDefault();
        if (tableParts is not null)
        {
            worksheet.InsertBefore(drawing, tableParts);
            return;
        }

        // drawing, legacyDrawing / legacyDrawingHeaderFooter / picture / oleObjects / controls / webPublishItems / extLst oncesinde olmali.
        var sonrakiElemanTipleri = new[]
        {
            typeof(Ssheet.LegacyDrawing),
            typeof(Ssheet.LegacyDrawingHeaderFooter),
            typeof(Ssheet.Picture),
            typeof(Ssheet.OleObjects),
            typeof(Ssheet.Controls),
            typeof(Ssheet.WebPublishItems),
            typeof(Ssheet.ExtensionList)
        };
        var anchor = worksheet.ChildElements.FirstOrDefault(x => sonrakiElemanTipleri.Contains(x.GetType()));

        if (anchor is not null)
        {
            worksheet.InsertBefore(drawing, anchor);
            return;
        }

        // En guvenli fallback: HeaderFooter varsa ondan sonra ekle.
        var headerFooter = worksheet.Elements<Ssheet.HeaderFooter>().FirstOrDefault();
        if (headerFooter is not null)
        {
            worksheet.InsertAfter(drawing, headerFooter);
            return;
        }

        // PageSetup varsa ondan sonra ekle.
        var pageSetup = worksheet.Elements<Ssheet.PageSetup>().FirstOrDefault();
        if (pageSetup is not null)
        {
            worksheet.InsertAfter(drawing, pageSetup);
            return;
        }

        worksheet.Append(drawing);
    }

    private static C.ChartSpace BuildClusteredColumnChartSpace(
        string baslik,
        string kategoriFormulu,
        List<(string AdFormulu, string DegerFormulu)> seriler)
    {
        var barChart = new C.BarChart(
            new C.BarDirection { Val = C.BarDirectionValues.Column },
            new C.BarGrouping { Val = C.BarGroupingValues.Clustered },
            new C.VaryColors { Val = false });

        for (var i = 0; i < seriler.Count; i++)
        {
            var (adFormulu, degerFormulu) = seriler[i];
            var seri = new C.BarChartSeries(
                new C.Index { Val = (uint)i },
                new C.Order { Val = (uint)i },
                new C.SeriesText(new C.StringReference(new C.Formula(adFormulu))),
                new C.CategoryAxisData(new C.StringReference(new C.Formula(kategoriFormulu))),
                new C.Values(new C.NumberReference(new C.Formula(degerFormulu))));
            barChart.Append(seri);
        }

        const uint catAxisId = 111111111;
        const uint valAxisId = 222222222;

        barChart.Append(new C.GapWidth { Val = 150 });
        barChart.Append(new C.Overlap { Val = -27 });
        barChart.Append(new C.AxisId { Val = catAxisId });
        barChart.Append(new C.AxisId { Val = valAxisId });

        var catAxis = new C.CategoryAxis(
            new C.AxisId { Val = catAxisId },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = C.AxisPositionValues.Bottom },
            new C.CrossingAxis { Val = valAxisId });

        var valAxis = new C.ValueAxis(
            new C.AxisId { Val = valAxisId },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = C.AxisPositionValues.Left },
            new C.CrossingAxis { Val = catAxisId });

        var plotArea = new C.PlotArea(new C.Layout(), barChart, catAxis, valAxis);

        var title = new C.Title(
            new C.ChartText(
                new C.RichText(
                    new A.BodyProperties(),
                    new A.ListStyle(),
                    new A.Paragraph(new A.Run(new A.Text(baslik))))),
            new C.Overlay { Val = false });

        var chart = new C.Chart(
            title,
            new C.AutoTitleDeleted { Val = false },
            plotArea,
            new C.Legend(
                new C.LegendPosition { Val = C.LegendPositionValues.Bottom },
                new C.Overlay { Val = false }),
            new C.PlotVisibleOnly { Val = true });

        return new C.ChartSpace(chart);
    }

    private static void AddGraphicFrameAnchor(
        Xdr.WorksheetDrawing worksheetDrawing,
        string chartRelationshipId,
        uint drawingId,
        string drawingName,
        int fromCol,
        int fromRow,
        int toCol,
        int toRow)
    {
        var anchor = new Xdr.TwoCellAnchor(
            new Xdr.FromMarker(
                new Xdr.ColumnId(fromCol.ToString()),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId(fromRow.ToString()),
                new Xdr.RowOffset("0")),
            new Xdr.ToMarker(
                new Xdr.ColumnId(toCol.ToString()),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId(toRow.ToString()),
                new Xdr.RowOffset("0")),
            new Xdr.GraphicFrame(
                new Xdr.NonVisualGraphicFrameProperties(
                    new Xdr.NonVisualDrawingProperties { Id = drawingId, Name = drawingName },
                    new Xdr.NonVisualGraphicFrameDrawingProperties()),
                new Xdr.Transform(
                    new A.Offset { X = 0, Y = 0 },
                    new A.Extents { Cx = 0, Cy = 0 }),
                new A.Graphic(
                    new A.GraphicData(
                        new C.ChartReference { Id = chartRelationshipId })
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" }))
            {
                Macro = ""
            },
            new Xdr.ClientData());

        worksheetDrawing.Append(anchor);
    }

    private static string ColumnLetter(int columnIndex)
    {
        var dividend = columnIndex;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }
}
