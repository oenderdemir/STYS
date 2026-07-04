using ClosedXML.Excel;

namespace STYS.Raporlar.KonaklamaKisiSayisi.Services;

public class KonaklamaKisiSayisiRaporExcelService : IKonaklamaKisiSayisiRaporExcelService
{
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

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Konaklama Kişi Sayısı");

        const int baslikRow = 2;
        const int headerRow = 4;
        const int ilkYilSatiri = 5;
        var sonKolon = 1 + rapor.Odalar.Count;

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
        ws.Cell(headerRow, sonKolon + 1).Value = "TOPLAM SAYI";

        var headerAraligi = ws.Range(headerRow, 1, headerRow, sonKolon + 1);
        headerAraligi.Style.Font.Bold = true;
        headerAraligi.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        headerAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

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

            var toplamCell = ws.Cell(satir, sonKolon + 1);
            toplamCell.Value = yilSatiri.ToplamKisiSayisi;
            toplamCell.Style.Font.Bold = true;
            toplamCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            satir++;
        }

        var sonSatir = satir - 1;
        var tabloAraligi = ws.Range(headerRow, 1, sonSatir, sonKolon + 1);
        tabloAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabloAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Column(1).Width = 12;
        for (var i = 0; i < rapor.Odalar.Count; i++)
        {
            ws.Column(2 + i).Width = 18;
        }
        ws.Column(sonKolon + 1).Width = 16;

        ws.SheetView.Freeze(headerRow, 1);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
