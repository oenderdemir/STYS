using ClosedXML.Excel;
using STYS.Raporlar.OrtalamaKonaklamaSuresi.Dto;

namespace STYS.Raporlar.OrtalamaKonaklamaSuresi.Services;

public class OrtalamaKonaklamaSuresiRaporExcelService : IOrtalamaKonaklamaSuresiRaporExcelService
{
    private const string RenkHeader = "#DDEBF7";
    private const string RenkKisaSatir = "#E2EFDA";
    private const string RenkOrtaSatir = "#DDEBF7";
    private const string RenkUzunSatir = "#FCE4D6";
    private const string OrtalamaFormati = "0.00";

    private readonly IOrtalamaKonaklamaSuresiRaporService _ortalamaKonaklamaSuresiRaporService;

    public OrtalamaKonaklamaSuresiRaporExcelService(IOrtalamaKonaklamaSuresiRaporService ortalamaKonaklamaSuresiRaporService)
    {
        _ortalamaKonaklamaSuresiRaporService = ortalamaKonaklamaSuresiRaporService;
    }

    public async Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? odaTipiId = null,
        CancellationToken cancellationToken = default)
    {
        var rapor = await _ortalamaKonaklamaSuresiRaporService.GetRaporAsync(tesisId, baslangic, bitis, odaTipiId, cancellationToken);

        using var workbook = new XLWorkbook();
        YazOzetSayfasi(workbook, rapor);
        YazOdaTipiOzetiSayfasi(workbook, rapor);
        YazRezervasyonlarSayfasi(workbook, rapor);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void YazOzetSayfasi(XLWorkbook workbook, OrtalamaKonaklamaSuresiRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Özet");

        ws.Cell(1, 1).Value = "ORTALAMA KONAKLAMA SÜRESİ RAPORU";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 2).Merge();

        var satir = 3;
        YazEtiketDeger(ws, ref satir, "Tesis", rapor.TesisAdi ?? string.Empty);
        YazEtiketDeger(ws, ref satir, "Tarih Aralığı", $"{rapor.Baslangic:dd.MM.yyyy} - {rapor.Bitis:dd.MM.yyyy}");
        YazEtiketDeger(ws, ref satir, "Oda Tipi", rapor.OdaTipiId.HasValue ? (rapor.OdaTipiAdi ?? $"ID: {rapor.OdaTipiId}") : "Tümü");

        satir++;
        YazEtiketDeger(ws, ref satir, "Toplam Rezervasyon", rapor.Ozet.ToplamRezervasyonSayisi);
        YazEtiketDeger(ws, ref satir, "Toplam Kişi", rapor.Ozet.ToplamKisiSayisi);
        YazEtiketDeger(ws, ref satir, "Toplam Gece", rapor.Ozet.ToplamGeceSayisi);

        var ortalamaCell = ws.Cell(satir, 2);
        ws.Cell(satir, 1).Value = "Ortalama Gece";
        ws.Cell(satir, 1).Style.Font.Bold = true;
        ortalamaCell.Value = rapor.Ozet.OrtalamaGeceSayisi;
        ortalamaCell.Style.NumberFormat.Format = OrtalamaFormati;
        satir++;

        YazEtiketDeger(ws, ref satir, "En Kısa Konaklama", rapor.Ozet.EnKisaKonaklamaGece);
        YazEtiketDeger(ws, ref satir, "En Uzun Konaklama", rapor.Ozet.EnUzunKonaklamaGece);
        YazEtiketDeger(ws, ref satir, "Kısa Konaklama", rapor.Ozet.KisaKonaklamaSayisi);
        YazEtiketDeger(ws, ref satir, "Orta Konaklama", rapor.Ozet.OrtaKonaklamaSayisi);
        YazEtiketDeger(ws, ref satir, "Uzun Konaklama", rapor.Ozet.UzunKonaklamaSayisi);

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

    private static void YazOdaTipiOzetiSayfasi(XLWorkbook workbook, OrtalamaKonaklamaSuresiRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Oda Tipi Özeti");

        string[] basliklar =
        [
            "Oda Tipi",
            "Rezervasyon Sayısı",
            "Toplam Kişi",
            "Toplam Gece",
            "Ortalama Gece",
            "En Kısa Gece",
            "En Uzun Gece"
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
            ws.Cell(satir, 2).Value = odaTipi.RezervasyonSayisi;
            ws.Cell(satir, 3).Value = odaTipi.ToplamKisiSayisi;
            ws.Cell(satir, 4).Value = odaTipi.ToplamGeceSayisi;

            var ortalamaCell = ws.Cell(satir, 5);
            ortalamaCell.Value = odaTipi.OrtalamaGeceSayisi;
            ortalamaCell.Style.NumberFormat.Format = OrtalamaFormati;

            ws.Cell(satir, 6).Value = odaTipi.EnKisaKonaklamaGece;
            ws.Cell(satir, 7).Value = odaTipi.EnUzunKonaklamaGece;

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

    private static void YazRezervasyonlarSayfasi(XLWorkbook workbook, OrtalamaKonaklamaSuresiRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Rezervasyonlar");

        string[] basliklar =
        [
            "Referans No",
            "Misafir Adı Soyadı",
            "Giriş Tarihi",
            "Çıkış Tarihi",
            "Gece Sayısı",
            "Kişi Sayısı",
            "Oda No(ları)",
            "Oda Tipi(leri)",
            "Rezervasyon Durumu",
            "Konaklama Grubu"
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
        foreach (var r in rapor.Rezervasyonlar)
        {
            ws.Cell(satir, 1).Value = r.ReferansNo;
            ws.Cell(satir, 2).Value = r.MisafirAdiSoyadi;

            var girisCell = ws.Cell(satir, 3);
            girisCell.Value = r.GirisTarihi;
            girisCell.Style.DateFormat.Format = "dd.MM.yyyy";

            var cikisCell = ws.Cell(satir, 4);
            cikisCell.Value = r.CikisTarihi;
            cikisCell.Style.DateFormat.Format = "dd.MM.yyyy";

            ws.Cell(satir, 5).Value = r.GeceSayisi;
            ws.Cell(satir, 6).Value = r.KisiSayisi;
            ws.Cell(satir, 7).Value = string.Join(", ", r.OdaNolari);
            ws.Cell(satir, 8).Value = string.Join(", ", r.OdaTipleri);
            ws.Cell(satir, 9).Value = r.RezervasyonDurumuLabel;
            ws.Cell(satir, 10).Value = r.KonaklamaGrubuLabel;

            var satirRengi = r.KonaklamaGrubu switch
            {
                "kisa" => RenkKisaSatir,
                "orta" => RenkOrtaSatir,
                "uzun" => RenkUzunSatir,
                _ => (string?)null
            };
            if (satirRengi is not null)
            {
                var satirAraligi = ws.Range(satir, 1, satir, basliklar.Length);
                satirAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml(satirRengi);
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
}
