using ClosedXML.Excel;
using STYS.Raporlar.RezervasyonDurumDagilimi.Dto;
using STYS.Rezervasyonlar;

namespace STYS.Raporlar.RezervasyonDurumDagilimi.Services;

public class RezervasyonDurumDagilimiRaporExcelService : IRezervasyonDurumDagilimiRaporExcelService
{
    private const string RenkHeader = "#DDEBF7";
    private const string RenkIptalSatir = "#FCE4E4";
    private const string RenkCheckOutSatir = "#E2EFDA";
    private const string RenkCheckInSatir = "#DDEBF7";
    private const string RenkOnayliSatir = "#FFF2CC";
    private const string RenkTaslakSatir = "#F2F2F2";

    // DTO'daki oran degerleri 0-100 araliginda hesaplanip donduruluyor (orn. 42.86), 0-1 araliginda degil.
    // Bu yuzden standart Excel yuzde formati "0.00%" (deger * 100 gosterir) kullanilmamali; literal "%" sonekiyle
    // "0.00\"%\"" formati deger uzerinde ek carpma yapmadan dogru gosterim saglar.
    private const string YuzdeFormati = "0.00\"%\"";

    private readonly IRezervasyonDurumDagilimiRaporService _rezervasyonDurumDagilimiRaporService;

    public RezervasyonDurumDagilimiRaporExcelService(IRezervasyonDurumDagilimiRaporService rezervasyonDurumDagilimiRaporService)
    {
        _rezervasyonDurumDagilimiRaporService = rezervasyonDurumDagilimiRaporService;
    }

    public async Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? odaTipiId = null,
        string? durum = null,
        CancellationToken cancellationToken = default)
    {
        var rapor = await _rezervasyonDurumDagilimiRaporService.GetRaporAsync(tesisId, baslangic, bitis, odaTipiId, durum, cancellationToken);

        using var workbook = new XLWorkbook();
        YazOzetSayfasi(workbook, rapor);
        YazDurumDagilimiSayfasi(workbook, rapor);
        YazOdaTipiDagilimiSayfasi(workbook, rapor);
        YazRezervasyonlarSayfasi(workbook, rapor);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void YazOzetSayfasi(XLWorkbook workbook, RezervasyonDurumDagilimiRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Özet");

        ws.Cell(1, 1).Value = "REZERVASYON DURUM DAĞILIMI RAPORU";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 2).Merge();

        var satir = 3;
        YazEtiketDeger(ws, ref satir, "Tesis", rapor.TesisAdi ?? string.Empty);
        YazEtiketDeger(ws, ref satir, "Tarih Aralığı", $"{rapor.Baslangic:dd.MM.yyyy} - {rapor.Bitis:dd.MM.yyyy}");
        YazEtiketDeger(ws, ref satir, "Oda Tipi", rapor.OdaTipiId.HasValue ? (rapor.OdaTipiAdi ?? $"ID: {rapor.OdaTipiId}") : "Tümü");
        YazEtiketDeger(ws, ref satir, "Durum", rapor.DurumLabel ?? "Tümü");

        satir++;
        YazEtiketDeger(ws, ref satir, "Toplam Rezervasyon", rapor.Ozet.ToplamRezervasyonSayisi);
        YazEtiketDeger(ws, ref satir, "Taslak", rapor.Ozet.TaslakSayisi);
        YazEtiketDeger(ws, ref satir, "Onaylı", rapor.Ozet.OnayliSayisi);
        YazEtiketDeger(ws, ref satir, "Check-in Tamamlandı", rapor.Ozet.CheckInTamamlandiSayisi);
        YazEtiketDeger(ws, ref satir, "Check-out Tamamlandı", rapor.Ozet.CheckOutTamamlandiSayisi);
        YazEtiketDeger(ws, ref satir, "İptal", rapor.Ozet.IptalSayisi);
        YazEtiketDeger(ws, ref satir, "Gerçekleşen Rezervasyon", rapor.Ozet.GerceklesenRezervasyonSayisi);
        YazEtiketDeger(ws, ref satir, "Devam Eden Rezervasyon", rapor.Ozet.DevamEdenRezervasyonSayisi);

        var iptalOraniCell = ws.Cell(satir, 2);
        ws.Cell(satir, 1).Value = "İptal Oranı";
        ws.Cell(satir, 1).Style.Font.Bold = true;
        iptalOraniCell.Value = rapor.Ozet.IptalOrani;
        iptalOraniCell.Style.NumberFormat.Format = YuzdeFormati;
        satir++;

        var gerceklesmeOraniCell = ws.Cell(satir, 2);
        ws.Cell(satir, 1).Value = "Gerçekleşme Oranı";
        ws.Cell(satir, 1).Style.Font.Bold = true;
        gerceklesmeOraniCell.Value = rapor.Ozet.GerceklesmeOrani;
        gerceklesmeOraniCell.Style.NumberFormat.Format = YuzdeFormati;
        satir++;

        YazEtiketDeger(ws, ref satir, "Toplam Kişi", rapor.Ozet.ToplamKisiSayisi);
        YazEtiketDeger(ws, ref satir, "Toplam Gece", rapor.Ozet.ToplamGeceSayisi);

        ws.Column(1).Width = 26;
        ws.Column(2).Width = 24;
    }

    private static void YazEtiketDeger(IXLWorksheet ws, ref int satir, string etiket, object deger)
    {
        ws.Cell(satir, 1).Value = etiket;
        ws.Cell(satir, 1).Style.Font.Bold = true;
        ws.Cell(satir, 2).Value = XLCellValue.FromObject(deger);
        satir++;
    }

    private static void YazDurumDagilimiSayfasi(XLWorkbook workbook, RezervasyonDurumDagilimiRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Durum Dağılımı");

        string[] basliklar = ["Durum", "Rezervasyon Sayısı", "Kişi Sayısı", "Gece Sayısı", "Oran"];

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
        foreach (var durumSatiri in rapor.Durumlar)
        {
            ws.Cell(satir, 1).Value = durumSatiri.DurumLabel;
            ws.Cell(satir, 2).Value = durumSatiri.RezervasyonSayisi;
            ws.Cell(satir, 3).Value = durumSatiri.KisiSayisi;
            ws.Cell(satir, 4).Value = durumSatiri.GeceSayisi;

            var oranCell = ws.Cell(satir, 5);
            oranCell.Value = durumSatiri.Oran;
            oranCell.Style.NumberFormat.Format = YuzdeFormati;

            var satirRengi = DurumSatirRengi(durumSatiri.Durum);
            if (satirRengi is not null)
            {
                ws.Range(satir, 1, satir, basliklar.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(satirRengi);
            }

            satir++;
        }

        TabloCercevesiVeFiltre(ws, satir, basliklar.Length);
    }

    private static void YazOdaTipiDagilimiSayfasi(XLWorkbook workbook, RezervasyonDurumDagilimiRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Oda Tipi Dağılımı");

        string[] basliklar =
        [
            "Oda Tipi",
            "Rezervasyon Sayısı",
            "İptal Sayısı",
            "Gerçekleşen Sayısı",
            "Kişi Sayısı",
            "Gece Sayısı",
            "İptal Oranı",
            "Gerçekleşme Oranı"
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
            ws.Cell(satir, 3).Value = odaTipi.IptalSayisi;
            ws.Cell(satir, 4).Value = odaTipi.GerceklesenSayisi;
            ws.Cell(satir, 5).Value = odaTipi.KisiSayisi;
            ws.Cell(satir, 6).Value = odaTipi.GeceSayisi;

            var iptalOraniCell = ws.Cell(satir, 7);
            iptalOraniCell.Value = odaTipi.IptalOrani;
            iptalOraniCell.Style.NumberFormat.Format = YuzdeFormati;

            var gerceklesmeOraniCell = ws.Cell(satir, 8);
            gerceklesmeOraniCell.Value = odaTipi.GerceklesmeOrani;
            gerceklesmeOraniCell.Style.NumberFormat.Format = YuzdeFormati;

            satir++;
        }

        TabloCercevesiVeFiltre(ws, satir, basliklar.Length);
    }

    private static void YazRezervasyonlarSayfasi(XLWorkbook workbook, RezervasyonDurumDagilimiRaporDto rapor)
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
            "Rezervasyon Durumu",
            "Oda No(ları)",
            "Oda Tipi(leri)"
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
            ws.Cell(satir, 7).Value = r.RezervasyonDurumuLabel;
            ws.Cell(satir, 8).Value = string.Join(", ", r.OdaNolari);
            ws.Cell(satir, 9).Value = string.Join(", ", r.OdaTipleri);

            var satirRengi = DurumSatirRengi(r.RezervasyonDurumu);
            if (satirRengi is not null)
            {
                ws.Range(satir, 1, satir, basliklar.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(satirRengi);
            }

            satir++;
        }

        TabloCercevesiVeFiltre(ws, satir, basliklar.Length);
    }

    private static void TabloCercevesiVeFiltre(IXLWorksheet ws, int satirSonrasi, int kolonSayisi)
    {
        var sonSatir = Math.Max(1, satirSonrasi - 1);
        var tabloAraligi = ws.Range(1, 1, sonSatir, kolonSayisi);
        tabloAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabloAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        if (sonSatir > 1)
        {
            tabloAraligi.SetAutoFilter();
        }

        ws.SheetView.Freeze(1, 0);
        ws.Columns().AdjustToContents();
    }

    private static string? DurumSatirRengi(string rezervasyonDurumu) => rezervasyonDurumu switch
    {
        RezervasyonDurumlari.Iptal => RenkIptalSatir,
        RezervasyonDurumlari.CheckOutTamamlandi => RenkCheckOutSatir,
        RezervasyonDurumlari.CheckInTamamlandi => RenkCheckInSatir,
        RezervasyonDurumlari.Onayli => RenkOnayliSatir,
        RezervasyonDurumlari.Taslak => RenkTaslakSatir,
        _ => null
    };
}
