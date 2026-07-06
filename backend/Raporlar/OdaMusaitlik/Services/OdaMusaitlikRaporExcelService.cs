using ClosedXML.Excel;
using STYS.Raporlar.OdaMusaitlik.Dto;

namespace STYS.Raporlar.OdaMusaitlik.Services;

public class OdaMusaitlikRaporExcelService : IOdaMusaitlikRaporExcelService
{
    private const string RenkBosHucre = "#C6E0B4";
    private const string RenkDoluHucre = "#F8CBAD";
    private const string RenkHeader = "#DDEBF7";
    private const string RenkTamamenBosSatir = "#E2EFDA";
    private const string RenkTamamenDoluSatir = "#FCE4E4";

    // DTO'daki oran degerleri 0-100 araliginda hesaplanip donduruluyor (orn. 42.86), 0-1 araliginda degil.
    // Bu yuzden standart Excel yuzde formati "0.00%" (deger * 100 gosterir) kullanilmamali; literal "%" sonekiyle
    // "0.00\"%\"" formati deger uzerinde ek carpma yapmadan dogru gosterim saglar.
    private const string YuzdeFormati = "0.00\"%\"";

    private const int SabitKolonSayisi = 4;

    private readonly IOdaMusaitlikRaporService _odaMusaitlikRaporService;

    public OdaMusaitlikRaporExcelService(IOdaMusaitlikRaporService odaMusaitlikRaporService)
    {
        _odaMusaitlikRaporService = odaMusaitlikRaporService;
    }

    public async Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        string? durum = null,
        int? odaTipiId = null,
        int? kapasite = null,
        CancellationToken cancellationToken = default)
    {
        var rapor = await _odaMusaitlikRaporService.GetRaporAsync(tesisId, baslangic, bitis, durum, odaTipiId, kapasite, cancellationToken);

        using var workbook = new XLWorkbook();
        YazOzetSayfasi(workbook, rapor);
        YazMusaitlikMatrisiSayfasi(workbook, rapor);
        YazOdaListesiSayfasi(workbook, rapor);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void YazOzetSayfasi(XLWorkbook workbook, OdaMusaitlikRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Özet");

        ws.Cell(1, 1).Value = "BOŞ ODA / MÜSAİTLİK RAPORU";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 2).Merge();

        var satir = 3;
        YazEtiketDeger(ws, ref satir, "Tesis", rapor.TesisAdi ?? string.Empty);
        YazEtiketDeger(ws, ref satir, "Tarih Aralığı", $"{rapor.Baslangic:dd.MM.yyyy} - {rapor.Bitis:dd.MM.yyyy}");
        YazEtiketDeger(ws, ref satir, "Durum Filtresi", DurumLabel(rapor.Durum));
        YazEtiketDeger(ws, ref satir, "Oda Tipi", rapor.OdaTipiId.HasValue ? (rapor.OdaTipiAdi ?? $"ID: {rapor.OdaTipiId}") : "Tümü");
        YazEtiketDeger(ws, ref satir, "Kapasite", rapor.Kapasite?.ToString() ?? "Tümü");

        satir++;
        YazEtiketDeger(ws, ref satir, "Toplam Oda", rapor.Ozet.ToplamOdaSayisi);
        YazEtiketDeger(ws, ref satir, "Tamamen Boş", rapor.Ozet.TamamenBosOdaSayisi);
        YazEtiketDeger(ws, ref satir, "Tamamen Dolu", rapor.Ozet.TamamenDoluOdaSayisi);
        YazEtiketDeger(ws, ref satir, "Kısmen Müsait", rapor.Ozet.KismenMusaitOdaSayisi);
        YazEtiketDeger(ws, ref satir, "Toplam Oda/Gün", rapor.Ozet.ToplamOdaGunSayisi);
        YazEtiketDeger(ws, ref satir, "Boş Oda/Gün", rapor.Ozet.BosOdaGunSayisi);
        YazEtiketDeger(ws, ref satir, "Dolu Oda/Gün", rapor.Ozet.DoluOdaGunSayisi);

        var oranCell = ws.Cell(satir, 2);
        ws.Cell(satir, 1).Value = "Müsaitlik Oranı";
        ws.Cell(satir, 1).Style.Font.Bold = true;
        oranCell.Value = rapor.Ozet.MusaitlikOrani;
        oranCell.Style.NumberFormat.Format = YuzdeFormati;
        satir++;

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

    private static void YazMusaitlikMatrisiSayfasi(XLWorkbook workbook, OdaMusaitlikRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Müsaitlik Matrisi");

        ws.Cell(1, 1).Value = "Oda No";
        ws.Cell(1, 2).Value = "Bina";
        ws.Cell(1, 3).Value = "Oda Tipi";
        ws.Cell(1, 4).Value = "Kapasite";

        var gunSayisi = rapor.Odalar.Count > 0 ? rapor.Odalar[0].Gunler.Count : 0;
        for (var i = 0; i < gunSayisi; i++)
        {
            ws.Cell(1, SabitKolonSayisi + 1 + i).Value = rapor.Odalar[0].Gunler[i].Tarih.ToString("dd.MM");
        }

        var sonKolon = SabitKolonSayisi + gunSayisi;
        var headerAraligi = ws.Range(1, 1, 1, sonKolon);
        headerAraligi.Style.Font.Bold = true;
        headerAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml(RenkHeader);
        headerAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var satir = 2;
        foreach (var oda in rapor.Odalar)
        {
            ws.Cell(satir, 1).Value = oda.OdaNo;
            ws.Cell(satir, 2).Value = oda.BinaAdi ?? string.Empty;
            ws.Cell(satir, 3).Value = oda.OdaTipiAdi ?? string.Empty;
            ws.Cell(satir, 4).Value = oda.Kapasite;

            var bilgiAraligi = ws.Range(satir, 1, satir, SabitKolonSayisi);
            var bilgiRengi = oda.MusaitlikDurumu switch
            {
                "tamamen-bos" => RenkTamamenBosSatir,
                "tamamen-dolu" => RenkTamamenDoluSatir,
                _ => (string?)null
            };
            if (bilgiRengi is not null)
            {
                bilgiAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml(bilgiRengi);
            }

            for (var i = 0; i < oda.Gunler.Count; i++)
            {
                var gun = oda.Gunler[i];
                var cell = ws.Cell(satir, SabitKolonSayisi + 1 + i);
                cell.Value = gun.DoluMu ? "DOLU" : "BOŞ";
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(gun.DoluMu ? RenkDoluHucre : RenkBosHucre);

                if (gun.DoluMu)
                {
                    var comment = cell.CreateComment();
                    comment.AddText($"Misafir: {(string.IsNullOrWhiteSpace(gun.MisafirAdiSoyadi) ? "-" : gun.MisafirAdiSoyadi)}");
                    comment.AddNewLine().AddText($"Referans No: {(string.IsNullOrWhiteSpace(gun.ReferansNo) ? "-" : gun.ReferansNo)}");
                    comment.AddNewLine().AddText($"Durum: {(string.IsNullOrWhiteSpace(gun.RezervasyonDurumuLabel) ? "-" : gun.RezervasyonDurumuLabel)}");
                }
            }

            satir++;
        }

        var sonSatir = Math.Max(1, satir - 1);
        var tabloAraligi = ws.Range(1, 1, sonSatir, sonKolon);
        tabloAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabloAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        if (sonSatir > 1)
        {
            tabloAraligi.SetAutoFilter();
        }

        ws.Column(1).Width = 12;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 10;
        for (var i = 0; i < gunSayisi; i++)
        {
            ws.Column(SabitKolonSayisi + 1 + i).Width = 8;
        }

        ws.SheetView.Freeze(1, SabitKolonSayisi);
    }

    private static void YazOdaListesiSayfasi(XLWorkbook workbook, OdaMusaitlikRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Oda Listesi");

        string[] basliklar =
        [
            "Oda No",
            "Bina",
            "Oda Tipi",
            "Kapasite",
            "Durum",
            "Toplam Gün",
            "Boş Gün",
            "Dolu Gün",
            "Müsaitlik Oranı"
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
        foreach (var oda in rapor.Odalar)
        {
            ws.Cell(satir, 1).Value = oda.OdaNo;
            ws.Cell(satir, 2).Value = oda.BinaAdi ?? string.Empty;
            ws.Cell(satir, 3).Value = oda.OdaTipiAdi ?? string.Empty;
            ws.Cell(satir, 4).Value = oda.Kapasite;
            ws.Cell(satir, 5).Value = oda.MusaitlikDurumuLabel;
            ws.Cell(satir, 6).Value = oda.ToplamGunSayisi;
            ws.Cell(satir, 7).Value = oda.BosGunSayisi;
            ws.Cell(satir, 8).Value = oda.DoluGunSayisi;

            var oranCell = ws.Cell(satir, 9);
            oranCell.Value = oda.MusaitlikOrani;
            oranCell.Style.NumberFormat.Format = YuzdeFormati;

            satir++;
        }

        var sonSatir = Math.Max(1, satir - 1);
        var tabloAraligi = ws.Range(1, 1, sonSatir, basliklar.Length);
        tabloAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabloAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        if (sonSatir > 1)
        {
            ws.RangeUsed()?.SetAutoFilter();
        }

        ws.SheetView.Freeze(1, 0);
        ws.Columns().AdjustToContents();
    }

    private static string DurumLabel(string durum) => durum switch
    {
        "tumu" => "Tümü",
        "tamamen-bos" => "Tamamen Boş",
        "tamamen-dolu" => "Tamamen Dolu",
        "kismen-musait" => "Kısmen Müsait",
        _ => durum
    };
}
