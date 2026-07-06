using ClosedXML.Excel;
using STYS.Raporlar.OdaTipiDoluluk.Dto;

namespace STYS.Raporlar.OdaTipiDoluluk.Services;

public class OdaTipiDolulukRaporExcelService : IOdaTipiDolulukRaporExcelService
{
    private const string RenkHeader = "#DDEBF7";
    private const string RenkYuksekDoluluk = "#F8CBAD";

    // DTO'daki oran degerleri 0-100 araliginda hesaplanip donduruluyor (orn. 42.86), 0-1 araliginda degil.
    // Bu yuzden standart Excel yuzde formati "0.00%" (deger * 100 gosterir) kullanilmamali; literal "%" sonekiyle
    // "0.00\"%\"" formati deger uzerinde ek carpma yapmadan dogru gosterim saglar.
    private const string YuzdeFormati = "0.00\"%\"";

    private const decimal YuksekDolulukEsigi = 80m;

    private readonly IOdaTipiDolulukRaporService _odaTipiDolulukRaporService;

    public OdaTipiDolulukRaporExcelService(IOdaTipiDolulukRaporService odaTipiDolulukRaporService)
    {
        _odaTipiDolulukRaporService = odaTipiDolulukRaporService;
    }

    public async Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? odaTipiId = null,
        CancellationToken cancellationToken = default)
    {
        var rapor = await _odaTipiDolulukRaporService.GetRaporAsync(tesisId, baslangic, bitis, odaTipiId, cancellationToken);

        using var workbook = new XLWorkbook();
        YazOzetSayfasi(workbook, rapor);
        YazOdaTipiDolulukSayfasi(workbook, rapor);
        YazOdaDetaylariSayfasi(workbook, rapor);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void YazOzetSayfasi(XLWorkbook workbook, OdaTipiDolulukRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Özet");

        ws.Cell(1, 1).Value = "ODA TİPİ BAZLI DOLULUK RAPORU";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 2).Merge();

        var satir = 3;
        YazEtiketDeger(ws, ref satir, "Tesis", rapor.TesisAdi ?? string.Empty);
        YazEtiketDeger(ws, ref satir, "Tarih Aralığı", $"{rapor.Baslangic:dd.MM.yyyy} - {rapor.Bitis:dd.MM.yyyy}");
        YazEtiketDeger(ws, ref satir, "Oda Tipi", rapor.OdaTipiId.HasValue ? (rapor.OdaTipiAdi ?? $"ID: {rapor.OdaTipiId}") : "Tümü");

        satir++;
        YazEtiketDeger(ws, ref satir, "Toplam Oda Tipi", rapor.Ozet.ToplamOdaTipiSayisi);
        YazEtiketDeger(ws, ref satir, "Toplam Oda", rapor.Ozet.ToplamOdaSayisi);
        YazEtiketDeger(ws, ref satir, "Toplam Kapasite", rapor.Ozet.ToplamKapasite);
        YazEtiketDeger(ws, ref satir, "Toplam Gün", rapor.Ozet.ToplamGunSayisi);
        YazEtiketDeger(ws, ref satir, "Toplam Oda/Gün", rapor.Ozet.ToplamOdaGunSayisi);
        YazEtiketDeger(ws, ref satir, "Dolu Oda/Gün", rapor.Ozet.DoluOdaGunSayisi);
        YazEtiketDeger(ws, ref satir, "Boş Oda/Gün", rapor.Ozet.BosOdaGunSayisi);

        var dolulukCell = ws.Cell(satir, 2);
        ws.Cell(satir, 1).Value = "Doluluk Oranı";
        ws.Cell(satir, 1).Style.Font.Bold = true;
        dolulukCell.Value = rapor.Ozet.DolulukOrani;
        dolulukCell.Style.NumberFormat.Format = YuzdeFormati;
        satir++;

        var musaitlikCell = ws.Cell(satir, 2);
        ws.Cell(satir, 1).Value = "Müsaitlik Oranı";
        ws.Cell(satir, 1).Style.Font.Bold = true;
        musaitlikCell.Value = rapor.Ozet.MusaitlikOrani;
        musaitlikCell.Style.NumberFormat.Format = YuzdeFormati;
        satir++;

        satir++;
        var notCell = ws.Cell(satir, 1);
        notCell.Value = "Not: Kişi-Gece değeri oda tipi kullanım yoğunluğu için yaklaşık metrik olarak hesaplanır.";
        notCell.Style.Font.Italic = true;
        notCell.Style.Font.FontColor = XLColor.FromHtml("#808080");
        ws.Range(satir, 1, satir, 2).Merge();

        ws.Column(1).Width = 24;
        ws.Column(2).Width = 24;
    }

    private static void YazEtiketDeger(IXLWorksheet ws, ref int satir, string etiket, object deger)
    {
        ws.Cell(satir, 1).Value = etiket;
        ws.Cell(satir, 1).Style.Font.Bold = true;
        ws.Cell(satir, 2).Value = XLCellValue.FromObject(deger);
        satir++;
    }

    private static void YazOdaTipiDolulukSayfasi(XLWorkbook workbook, OdaTipiDolulukRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Oda Tipi Doluluk");

        string[] basliklar =
        [
            "Oda Tipi",
            "Oda Sayısı",
            "Toplam Kapasite",
            "Toplam Oda/Gün",
            "Dolu Oda/Gün",
            "Boş Oda/Gün",
            "Doluluk Oranı",
            "Müsaitlik Oranı",
            "Rezervasyon Sayısı",
            "Konaklayan Kişi Sayısı",
            "Kişi-Gece"
        ];

        for (var i = 0; i < basliklar.Length; i++)
        {
            ws.Cell(1, i + 1).Value = basliklar[i];
        }

        var headerAraligi = ws.Range(1, 1, 1, basliklar.Length);
        headerAraligi.Style.Font.Bold = true;
        headerAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml(RenkHeader);
        headerAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var satir = 2;
        foreach (var odaTipi in rapor.OdaTipleri)
        {
            ws.Cell(satir, 1).Value = odaTipi.OdaTipiAdi;
            ws.Cell(satir, 2).Value = odaTipi.OdaSayisi;
            ws.Cell(satir, 3).Value = odaTipi.ToplamKapasite;
            ws.Cell(satir, 4).Value = odaTipi.ToplamOdaGunSayisi;
            ws.Cell(satir, 5).Value = odaTipi.DoluOdaGunSayisi;
            ws.Cell(satir, 6).Value = odaTipi.BosOdaGunSayisi;

            var dolulukCell = ws.Cell(satir, 7);
            dolulukCell.Value = odaTipi.DolulukOrani;
            dolulukCell.Style.NumberFormat.Format = YuzdeFormati;

            var musaitlikCell = ws.Cell(satir, 8);
            musaitlikCell.Value = odaTipi.MusaitlikOrani;
            musaitlikCell.Style.NumberFormat.Format = YuzdeFormati;

            ws.Cell(satir, 9).Value = odaTipi.ToplamRezervasyonSayisi;
            ws.Cell(satir, 10).Value = odaTipi.ToplamKonaklayanKisiSayisi;
            ws.Cell(satir, 11).Value = odaTipi.ToplamKisiGeceSayisi;

            if (odaTipi.DolulukOrani >= YuksekDolulukEsigi)
            {
                var satirAraligi = ws.Range(satir, 1, satir, basliklar.Length);
                satirAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml(RenkYuksekDoluluk);
            }

            satir++;
        }

        var sonSatir = Math.Max(1, satir - 1);
        var tabloAraligi = ws.Range(1, 1, sonSatir, basliklar.Length);
        tabloAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabloAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        if (sonSatir > 1)
        {
            tabloAraligi.SetAutoFilter();
        }

        ws.SheetView.Freeze(1, 0);
        ws.Columns().AdjustToContents();
    }

    private static void YazOdaDetaylariSayfasi(XLWorkbook workbook, OdaTipiDolulukRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Oda Detayları");

        string[] basliklar =
        [
            "Oda Tipi",
            "Oda No",
            "Bina",
            "Kapasite",
            "Toplam Gün",
            "Dolu Gün",
            "Boş Gün",
            "Doluluk Oranı"
        ];

        for (var i = 0; i < basliklar.Length; i++)
        {
            ws.Cell(1, i + 1).Value = basliklar[i];
        }

        var headerAraligi = ws.Range(1, 1, 1, basliklar.Length);
        headerAraligi.Style.Font.Bold = true;
        headerAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml(RenkHeader);
        headerAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var satir = 2;
        foreach (var odaTipi in rapor.OdaTipleri)
        {
            foreach (var oda in odaTipi.Odalar)
            {
                ws.Cell(satir, 1).Value = odaTipi.OdaTipiAdi;
                ws.Cell(satir, 2).Value = oda.OdaNo;
                ws.Cell(satir, 3).Value = oda.BinaAdi ?? string.Empty;
                ws.Cell(satir, 4).Value = oda.Kapasite;
                ws.Cell(satir, 5).Value = oda.ToplamGunSayisi;
                ws.Cell(satir, 6).Value = oda.DoluGunSayisi;
                ws.Cell(satir, 7).Value = oda.BosGunSayisi;

                var oranCell = ws.Cell(satir, 8);
                oranCell.Value = oda.DolulukOrani;
                oranCell.Style.NumberFormat.Format = YuzdeFormati;

                satir++;
            }
        }

        var sonSatir = Math.Max(1, satir - 1);
        var tabloAraligi = ws.Range(1, 1, sonSatir, basliklar.Length);
        tabloAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabloAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        if (sonSatir > 1)
        {
            tabloAraligi.SetAutoFilter();
        }

        ws.SheetView.Freeze(1, 0);
        ws.Columns().AdjustToContents();
    }
}
