using System.Globalization;
using System.Text;
using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using STYS.Rezervasyonlar.Dto;

namespace STYS.Rezervasyonlar.Reporting;

public static class OdemeRaporExportBuilder
{
    public static byte[] BuildExcel(OdemeRaporDto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\" />");
        sb.AppendLine("<style>");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
        sb.AppendLine("th, td { border: 1px solid #000; padding: 6px; }");
        sb.AppendLine("th { background-color: #f0f0f0; text-align: left; }");
        sb.AppendLine(".num { text-align: right; }");
        sb.AppendLine(".title { font-weight: bold; font-size: 16px; margin-bottom: 8px; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"title\">Odeme Raporu</div>");
        sb.AppendLine($"<div>Tesis Idler: {string.Join(", ", report.TesisIds.OrderBy(x => x))}</div>");
        sb.AppendLine($"<div>Tarih Araligi: {report.BaslangicTarihi:yyyy-MM-dd} - {report.BitisTarihi:yyyy-MM-dd}</div>");
        sb.AppendLine("<br />");
        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr>");
        sb.AppendLine("<th>Tesis</th>");
        sb.AppendLine("<th>Rezervasyon No</th>");
        sb.AppendLine("<th>Odeme Yapan</th>");
        sb.AppendLine("<th>Toplam Baz Ucret</th>");
        sb.AppendLine("<th>Toplam Indirim</th>");
        sb.AppendLine("<th>Toplam Odeme</th>");
        sb.AppendLine("</tr></thead>");
        sb.AppendLine("<tbody>");

        var rowColorMap = BuildTesisColorMap(report.Satirlar);
        foreach (var row in report.Satirlar)
        {
            var rowColor = rowColorMap.TryGetValue(row.TesisId, out var color) ? color : "#ffffff";
            sb.AppendLine($"<tr style=\"background-color:{rowColor};\">");
            sb.AppendLine($"<td>{EscapeHtml(row.TesisAdi)}</td>");
            sb.AppendLine($"<td>{EscapeHtml(row.RezervasyonNo)}</td>");
            sb.AppendLine($"<td>{EscapeHtml(row.OdemeYapan)}</td>");
            sb.AppendLine($"<td class=\"num\">{FormatMoney(row.ToplamBazUcret)}</td>");
            sb.AppendLine($"<td class=\"num\">{FormatMoney(row.ToplamIndirim)}</td>");
            sb.AppendLine($"<td class=\"num\">{FormatMoney(row.ToplamOdeme)}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("<tr>");
        sb.AppendLine("<td colspan=\"5\"><strong>Toplam Gelir</strong></td>");
        sb.AppendLine($"<td class=\"num\"><strong>{FormatMoney(report.ToplamGelir)}</strong></td>");
        sb.AppendLine("</tr>");
        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public static byte[] BuildPdf(OdemeRaporDto report)
    {
        using var stream = new MemoryStream();
        using var writer = new PdfWriter(stream);
        using var pdf = new PdfDocument(writer);
        using var document = new Document(pdf, PageSize.A4.Rotate());

        document.SetMargins(24, 24, 24, 24);
        var regularFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA, "Cp1254");
        var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD, "Cp1254");
        document.SetFont(regularFont);

        document.Add(new Paragraph("Odeme Raporu").SetFontSize(16).SetFont(boldFont));
        document.Add(new Paragraph(NormalizeForPdf($"Tesis Idler: {string.Join(", ", report.TesisIds.OrderBy(x => x))}")).SetFontSize(10));
        document.Add(new Paragraph(NormalizeForPdf($"Tarih Araligi: {report.BaslangicTarihi:yyyy-MM-dd} - {report.BitisTarihi:yyyy-MM-dd}")).SetFontSize(10));
        document.Add(new Paragraph(" "));

        var table = new Table(UnitValue.CreatePercentArray([2.2f, 2.2f, 2.6f, 1.7f, 1.7f, 1.7f]))
            .UseAllAvailableWidth();

        table.AddHeaderCell(CreateHeaderCell("Tesis", boldFont));
        table.AddHeaderCell(CreateHeaderCell("Rezervasyon No", boldFont));
        table.AddHeaderCell(CreateHeaderCell("Odeme Yapan", boldFont));
        table.AddHeaderCell(CreateHeaderCell("Toplam Baz Ucret", boldFont));
        table.AddHeaderCell(CreateHeaderCell("Toplam Indirim", boldFont));
        table.AddHeaderCell(CreateHeaderCell("Toplam Odeme", boldFont));

        var pdfRowColorMap = BuildTesisPdfColorMap(report.Satirlar);
        foreach (var row in report.Satirlar)
        {
            var rowColor = pdfRowColorMap.TryGetValue(row.TesisId, out var color) ? color : ColorConstants.WHITE;

            table.AddCell(CreateBodyCell(NormalizeForPdf(row.TesisAdi), rowColor, TextAlignment.LEFT, regularFont));
            table.AddCell(CreateBodyCell(NormalizeForPdf(row.RezervasyonNo), rowColor, TextAlignment.LEFT, regularFont));
            table.AddCell(CreateBodyCell(NormalizeForPdf(row.OdemeYapan), rowColor, TextAlignment.LEFT, regularFont));
            table.AddCell(CreateBodyCell(NormalizeForPdf(FormatMoney(row.ToplamBazUcret)), rowColor, TextAlignment.RIGHT, regularFont));
            table.AddCell(CreateBodyCell(NormalizeForPdf(FormatMoney(row.ToplamIndirim)), rowColor, TextAlignment.RIGHT, regularFont));
            table.AddCell(CreateBodyCell(NormalizeForPdf(FormatMoney(row.ToplamOdeme)), rowColor, TextAlignment.RIGHT, regularFont));
        }

        var totalLabelCell = new Cell(1, 5)
            .Add(new Paragraph("Toplam Gelir").SetFont(boldFont))
            .SetBackgroundColor(new DeviceRgb(235, 245, 235))
            .SetBorder(new SolidBorder(0.5f))
            .SetPadding(4);
        table.AddCell(totalLabelCell);

        var totalValueCell = new Cell()
            .Add(new Paragraph(NormalizeForPdf(FormatMoney(report.ToplamGelir))).SetFont(boldFont))
            .SetTextAlignment(TextAlignment.RIGHT)
            .SetBackgroundColor(new DeviceRgb(235, 245, 235))
            .SetBorder(new SolidBorder(0.5f))
            .SetPadding(4);
        table.AddCell(totalValueCell);

        document.Add(table);
        document.Close();
        return stream.ToArray();
    }

    private static Cell CreateHeaderCell(string text, PdfFont boldFont)
    {
        return new Cell()
            .Add(new Paragraph(text).SetFont(boldFont))
            .SetBackgroundColor(new DeviceRgb(230, 230, 230))
            .SetBorder(new SolidBorder(0.5f))
            .SetPadding(4)
            .SetFontSize(9);
    }

    private static Cell CreateBodyCell(string text, Color backgroundColor, TextAlignment alignment, PdfFont font)
    {
        return new Cell()
            .Add(new Paragraph(text).SetFont(font))
            .SetBackgroundColor(backgroundColor)
            .SetBorder(new SolidBorder(0.5f))
            .SetPadding(4)
            .SetFontSize(9)
            .SetTextAlignment(alignment);
    }

    private static string EscapeHtml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));
    }

    private static string NormalizeForPdf(string value)
    {
        return value
            .Replace('Ç', 'C').Replace('ç', 'c')
            .Replace('Ğ', 'G').Replace('ğ', 'g')
            .Replace('İ', 'I').Replace('ı', 'i')
            .Replace('Ö', 'O').Replace('ö', 'o')
            .Replace('Ş', 'S').Replace('ş', 's')
            .Replace('Ü', 'U').Replace('ü', 'u');
    }

    private static Dictionary<int, Color> BuildTesisPdfColorMap(List<OdemeRaporSatirDto> rows)
    {
        var palette = new DeviceRgb[]
        {
            new(255, 255, 255),
            new(242, 247, 255),
            new(245, 255, 245),
            new(255, 249, 240),
            new(248, 243, 255),
            new(255, 243, 248),
            new(243, 255, 253)
        };

        return rows
            .Select(x => x.TesisId)
            .Distinct()
            .OrderBy(x => x)
            .Select((tesisId, index) => new
            {
                tesisId,
                color = (Color)palette[index % palette.Length]
            })
            .ToDictionary(x => x.tesisId, x => x.color);
    }

    private static Dictionary<int, string> BuildTesisColorMap(List<OdemeRaporSatirDto> rows)
    {
        var palette = new[]
        {
            "#ffffff",
            "#f3f8ff",
            "#f6fff3",
            "#fff9f0",
            "#f8f3ff",
            "#fff3f8",
            "#f3fffd"
        };

        return rows
            .Select(x => x.TesisId)
            .Distinct()
            .OrderBy(x => x)
            .Select((tesisId, index) => new
            {
                tesisId,
                color = palette[index % palette.Length]
            })
            .ToDictionary(x => x.tesisId, x => x.color);
    }
}
