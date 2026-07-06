using ClosedXML.Excel;
using STYS.Raporlar.GecikenCheckIn.Dto;

namespace STYS.Raporlar.GecikenCheckIn.Services;

public class GecikenCheckInRaporExcelService : IGecikenCheckInRaporExcelService
{
    private const string RenkHeader = "#DDEBF7";
    private const string RenkBugunGirisSatir = "#DDEBF7";
    private const string RenkGecikenSatir = "#FFF2CC";
    private const string RenkKritikGecikenSatir = "#FCE4E4";
    private const string RenkKalanTutarDikkat = "#FFC7CE";

    private readonly IGecikenCheckInRaporService _gecikenCheckInRaporService;

    public GecikenCheckInRaporExcelService(IGecikenCheckInRaporService gecikenCheckInRaporService)
    {
        _gecikenCheckInRaporService = gecikenCheckInRaporService;
    }

    public async Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime? referansTarihi = null,
        int? odaTipiId = null,
        string? gecikmeDurumu = null,
        CancellationToken cancellationToken = default)
    {
        var rapor = await _gecikenCheckInRaporService.GetRaporAsync(tesisId, referansTarihi, odaTipiId, gecikmeDurumu, cancellationToken);

        using var workbook = new XLWorkbook();
        YazOzetSayfasi(workbook, rapor);
        YazRezervasyonlarSayfasi(workbook, rapor);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void YazOzetSayfasi(XLWorkbook workbook, GecikenCheckInRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Özet");

        ws.Cell(1, 1).Value = "GECİKEN CHECK-IN / GİRİŞ YAPMAYAN REZERVASYONLAR RAPORU";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 2).Merge();

        var satir = 3;
        YazEtiketDeger(ws, ref satir, "Tesis", rapor.TesisAdi ?? string.Empty);
        YazEtiketDeger(ws, ref satir, "Referans Tarihi", rapor.ReferansTarihi.ToString("dd.MM.yyyy"));
        YazEtiketDeger(ws, ref satir, "Oda Tipi", rapor.OdaTipiId.HasValue ? (rapor.OdaTipiAdi ?? $"ID: {rapor.OdaTipiId}") : "Tümü");
        YazEtiketDeger(ws, ref satir, "Gecikme Durumu", GecikmeDurumuFiltreLabel(rapor.GecikmeDurumu));

        satir++;
        YazEtiketDeger(ws, ref satir, "Toplam Rezervasyon", rapor.Ozet.ToplamRezervasyonSayisi);
        YazEtiketDeger(ws, ref satir, "Bugün Giriş", rapor.Ozet.BugunGirisSayisi);
        YazEtiketDeger(ws, ref satir, "Geciken", rapor.Ozet.GecikenSayisi);
        YazEtiketDeger(ws, ref satir, "Kritik Geciken", rapor.Ozet.KritikGecikenSayisi);
        YazEtiketDeger(ws, ref satir, "Toplam Kişi", rapor.Ozet.ToplamKisiSayisi);

        var kalanTutarCell = ws.Cell(satir, 2);
        ws.Cell(satir, 1).Value = "Toplam Kalan Tutar";
        ws.Cell(satir, 1).Style.Font.Bold = true;
        kalanTutarCell.Value = rapor.Ozet.ToplamKalanTutar;
        var paraBirimi = rapor.Rezervasyonlar.FirstOrDefault()?.ParaBirimi ?? "TRY";
        kalanTutarCell.Style.NumberFormat.Format = ParaFormati(paraBirimi);
        satir++;

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

    private static void YazRezervasyonlarSayfasi(XLWorkbook workbook, GecikenCheckInRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Rezervasyonlar");

        string[] basliklar =
        [
            "Referans No",
            "Misafir Adı Soyadı",
            "Telefon",
            "Giriş Tarihi",
            "Çıkış Tarihi",
            "Geciken Gün",
            "Gecikme Durumu",
            "Kişi Sayısı",
            "Oda No(ları)",
            "Oda Tipi(leri)",
            "Rezervasyon Durumu",
            "Toplam Ücret",
            "Ödenen Tutar",
            "Kalan Tutar",
            "Para Birimi"
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
            ws.Cell(satir, 3).Value = r.MisafirTelefon;

            var girisCell = ws.Cell(satir, 4);
            girisCell.Value = r.GirisTarihi;
            girisCell.Style.DateFormat.Format = "dd.MM.yyyy";

            var cikisCell = ws.Cell(satir, 5);
            cikisCell.Value = r.CikisTarihi;
            cikisCell.Style.DateFormat.Format = "dd.MM.yyyy";

            ws.Cell(satir, 6).Value = r.GecikenGunSayisi;
            ws.Cell(satir, 7).Value = r.GecikmeDurumuLabel;
            ws.Cell(satir, 8).Value = r.KisiSayisi;
            ws.Cell(satir, 9).Value = string.Join(", ", r.OdaNolari);
            ws.Cell(satir, 10).Value = string.Join(", ", r.OdaTipleri);
            ws.Cell(satir, 11).Value = r.RezervasyonDurumuLabel;

            var paraFormati = ParaFormati(r.ParaBirimi ?? "TRY");

            var toplamCell = ws.Cell(satir, 12);
            toplamCell.Value = r.ToplamUcret;
            toplamCell.Style.NumberFormat.Format = paraFormati;

            var odenenCell = ws.Cell(satir, 13);
            odenenCell.Value = r.OdenenTutar;
            odenenCell.Style.NumberFormat.Format = paraFormati;

            var kalanCell = ws.Cell(satir, 14);
            kalanCell.Value = r.KalanTutar;
            kalanCell.Style.NumberFormat.Format = paraFormati;

            ws.Cell(satir, 15).Value = r.ParaBirimi;

            // Once satir durum rengi uygulanir, ardindan Kalan Tutar > 0 ise o hucre ayrica
            // dikkat rengiyle boyanir; aksi halde satir rengi bu uyariyi ezerdi.
            var satirRengi = r.GecikmeDurumu switch
            {
                "kritik-geciken" => RenkKritikGecikenSatir,
                "geciken" => RenkGecikenSatir,
                "bugun-giris" => RenkBugunGirisSatir,
                _ => (string?)null
            };
            if (satirRengi is not null)
            {
                ws.Range(satir, 1, satir, basliklar.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(satirRengi);
            }

            if (r.KalanTutar > 0m)
            {
                kalanCell.Style.Font.Bold = true;
                kalanCell.Style.Fill.BackgroundColor = XLColor.FromHtml(RenkKalanTutarDikkat);
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

    // Para birimi TRY ise ₺ sembolu, degilse ParaBirimi kodu tutar formatinin sonuna eklenir.
    private static string ParaFormati(string paraBirimi)
    {
        var sembol = string.IsNullOrWhiteSpace(paraBirimi) || paraBirimi.Equals("TRY", StringComparison.OrdinalIgnoreCase)
            ? "₺"
            : paraBirimi;
        return $"#,##0.00 \"{sembol}\"";
    }

    private static string GecikmeDurumuFiltreLabel(string gecikmeDurumu) => gecikmeDurumu switch
    {
        "tumu" => "Tümü",
        "bugun-giris" => "Bugün Giriş",
        "geciken" => "Geciken",
        "kritik-geciken" => "Kritik Geciken",
        _ => gecikmeDurumu
    };
}
