using ClosedXML.Excel;
using STYS.Muhasebe.SarfRaporlari.Dtos;

namespace STYS.Muhasebe.SarfRaporlari.Services;

public sealed class SarfTuketimRaporExcelService : ISarfTuketimRaporExcelService
{
    private const string HeaderColor = "#DDEBF7";
    private readonly ISarfTuketimRaporService _raporService;

    public SarfTuketimRaporExcelService(ISarfTuketimRaporService raporService)
    {
        _raporService = raporService;
    }

    public async Task<byte[]> ExportDetayAsync(SarfTuketimRaporFilterDto filter, CancellationToken cancellationToken = default)
    {
        var rows = await _raporService.GetDetayListAsync(filter, cancellationToken);
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Detay");

        string[] headers =
        [
            "Tarih",
            "Fiş No",
            "Depo",
            "İşletme Alanı",
            "Oda",
            "Sarf Nedeni",
            "Stok Kodu",
            "Malzeme",
            "Birim",
            "Miktar",
            "Lot No",
            "Seri No",
            "Durum",
            "Birim Maliyet",
            "Toplam Maliyet"
        ];

        WriteHeaders(ws, headers);

        var rowIndex = 2;
        foreach (var row in rows)
        {
            ws.Cell(rowIndex, 1).Value = row.Tarih;
            ws.Cell(rowIndex, 1).Style.DateFormat.Format = "dd.MM.yyyy";
            ws.Cell(rowIndex, 2).Value = row.FisNo;
            ws.Cell(rowIndex, 3).Value = $"{row.DepoKod} - {row.DepoAd}";
            ws.Cell(rowIndex, 4).Value = row.IsletmeAlaniAd ?? string.Empty;
            ws.Cell(rowIndex, 5).Value = row.OdaAd ?? string.Empty;
            ws.Cell(rowIndex, 6).Value = row.SarfNedeni ?? string.Empty;
            ws.Cell(rowIndex, 7).Value = row.StokKodu;
            ws.Cell(rowIndex, 8).Value = row.MalzemeAd;
            ws.Cell(rowIndex, 9).Value = row.Birim;
            ws.Cell(rowIndex, 10).Value = row.Miktar;
            ws.Cell(rowIndex, 11).Value = row.LotNo ?? string.Empty;
            ws.Cell(rowIndex, 12).Value = row.SeriNo ?? string.Empty;
            ws.Cell(rowIndex, 13).Value = row.Durum;
            ws.Cell(rowIndex, 14).Value = row.MaliyetBirimFiyat ?? 0m;
            ws.Cell(rowIndex, 15).Value = row.ToplamMaliyet ?? 0m;
            rowIndex++;
        }

        FinalizeSheet(ws, headers.Length, rowIndex);
        return Save(workbook);
    }

    public async Task<byte[]> ExportMalzemeOzetAsync(SarfTuketimRaporFilterDto filter, CancellationToken cancellationToken = default)
    {
        var rows = await _raporService.GetMalzemeOzetAsync(filter, cancellationToken);
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Malzeme Bazlı");

        string[] headers =
        [
            "Stok Kodu",
            "Malzeme",
            "Birim",
            "Toplam Tüketim",
            "Sarf Fişi Sayısı",
            "Toplam Tüketim Maliyeti"
        ];

        WriteHeaders(ws, headers);

        var rowIndex = 2;
        foreach (var row in rows)
        {
            ws.Cell(rowIndex, 1).Value = row.StokKodu;
            ws.Cell(rowIndex, 2).Value = row.MalzemeAd;
            ws.Cell(rowIndex, 3).Value = row.Birim;
            ws.Cell(rowIndex, 4).Value = row.ToplamTuketimMiktari;
            ws.Cell(rowIndex, 5).Value = row.SarfFisiSayisi;
            ws.Cell(rowIndex, 6).Value = row.ToplamTuketimMaliyeti;
            rowIndex++;
        }

        FinalizeSheet(ws, headers.Length, rowIndex);
        return Save(workbook);
    }

    public async Task<byte[]> ExportKullanimYeriOzetAsync(SarfTuketimRaporFilterDto filter, CancellationToken cancellationToken = default)
    {
        var rows = await _raporService.GetKullanimYeriOzetAsync(filter, cancellationToken);
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Kullanım Yeri");

        string[] headers =
        [
            "İşletme Alanı",
            "Oda",
            "Farklı Malzeme Sayısı",
            "Toplam Sarf Satırı",
            "Toplam Miktar",
            "Toplam Tüketim Maliyeti"
        ];

        WriteHeaders(ws, headers);

        var rowIndex = 2;
        foreach (var row in rows)
        {
            ws.Cell(rowIndex, 1).Value = row.IsletmeAlaniAd ?? string.Empty;
            ws.Cell(rowIndex, 2).Value = row.OdaAd ?? string.Empty;
            ws.Cell(rowIndex, 3).Value = row.FarkliMalzemeSayisi;
            ws.Cell(rowIndex, 4).Value = row.ToplamSarfSatiriSayisi;
            ws.Cell(rowIndex, 5).Value = row.ToplamMiktarOzeti;
            ws.Cell(rowIndex, 6).Value = row.ToplamTuketimMaliyeti;
            rowIndex++;
        }

        FinalizeSheet(ws, headers.Length, rowIndex);
        return Save(workbook);
    }

    private static void WriteHeaders(IXLWorksheet ws, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }

        var range = ws.Range(1, 1, 1, headers.Count);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderColor);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void FinalizeSheet(IXLWorksheet ws, int columnCount, int nextRowIndex)
    {
        var lastRow = Math.Max(1, nextRowIndex - 1);
        var range = ws.Range(1, 1, lastRow, columnCount);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        if (lastRow > 1)
        {
            range.SetAutoFilter();
        }

        ws.SheetView.Freeze(1, 0);
        ws.Columns().AdjustToContents();
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
