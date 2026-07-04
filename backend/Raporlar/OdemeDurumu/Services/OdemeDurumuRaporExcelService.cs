using ClosedXML.Excel;
using STYS.Raporlar.OdemeDurumu.Dto;

namespace STYS.Raporlar.OdemeDurumu.Services;

public class OdemeDurumuRaporExcelService : IOdemeDurumuRaporExcelService
{
    private const string RenkBorcluSatir = "#FFF2CC";
    private const string RenkCikisYapmisBorcluSatir = "#F8CBAD";
    private const string RenkHeader = "#D9E1F2";

    private readonly IOdemeDurumuRaporService _odemeDurumuRaporService;

    public OdemeDurumuRaporExcelService(IOdemeDurumuRaporService odemeDurumuRaporService)
    {
        _odemeDurumuRaporService = odemeDurumuRaporService;
    }

    public async Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        string? odemeDurumu,
        CancellationToken cancellationToken = default)
    {
        var rapor = await _odemeDurumuRaporService.GetRaporAsync(tesisId, baslangic, bitis, odemeDurumu, cancellationToken);

        using var workbook = new XLWorkbook();
        YazOzetSayfasi(workbook, rapor);
        YazRezervasyonlarSayfasi(workbook, rapor);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void YazOzetSayfasi(XLWorkbook workbook, OdemeDurumuRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Özet");

        ws.Cell(1, 1).Value = "ÖDEME DURUMU / BORÇLU REZERVASYONLAR RAPORU";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 2).Merge();

        var satir = 3;
        YazEtiketDeger(ws, ref satir, "Tesis", rapor.TesisAdi ?? string.Empty);
        YazEtiketDeger(ws, ref satir, "Tarih Aralığı", $"{rapor.Baslangic:dd.MM.yyyy} - {rapor.Bitis:dd.MM.yyyy}");
        YazEtiketDeger(ws, ref satir, "Filtre", OdemeDurumuFiltreLabel(rapor.OdemeDurumu));

        satir++;
        ws.Cell(satir, 1).Value = "ÖZET BİLGİLER";
        ws.Cell(satir, 1).Style.Font.Bold = true;
        satir++;

        YazEtiketDeger(ws, ref satir, "Toplam Rezervasyon Sayısı", rapor.Ozet.ToplamRezervasyonSayisi);
        YazEtiketDeger(ws, ref satir, "Borçlu Rezervasyon Sayısı", rapor.Ozet.BorcluRezervasyonSayisi);
        YazEtiketDeger(ws, ref satir, "Ödemesi Olmayan Rezervasyon Sayısı", rapor.Ozet.OdemesiOlmayanRezervasyonSayisi);
        YazEtiketDeger(ws, ref satir, "Kısmi Ödendi Rezervasyon Sayısı", rapor.Ozet.KismiOdendiRezervasyonSayisi);
        YazEtiketDeger(ws, ref satir, "Tamamen Ödendi Rezervasyon Sayısı", rapor.Ozet.TamamenOdendiRezervasyonSayisi);
        YazEtiketDeger(ws, ref satir, "Çıkış Yapmış Borçlu Rezervasyon Sayısı", rapor.Ozet.CikisYapmisBorcluRezervasyonSayisi);

        satir++;
        YazEtiketDegerPara(ws, ref satir, "Toplam Ücret", rapor.Ozet.ToplamUcret);
        YazEtiketDegerPara(ws, ref satir, "Toplam Ödenen Tutar", rapor.Ozet.ToplamOdenenTutar);
        YazEtiketDegerPara(ws, ref satir, "Toplam Kalan Tutar", rapor.Ozet.ToplamKalanTutar);

        ws.Column(1).Width = 40;
        ws.Column(2).Width = 24;
    }

    private static void YazEtiketDeger(IXLWorksheet ws, ref int satir, string etiket, object deger)
    {
        ws.Cell(satir, 1).Value = etiket;
        ws.Cell(satir, 1).Style.Font.Bold = true;
        ws.Cell(satir, 2).Value = XLCellValue.FromObject(deger);
        satir++;
    }

    private static void YazEtiketDegerPara(IXLWorksheet ws, ref int satir, string etiket, decimal deger)
    {
        ws.Cell(satir, 1).Value = etiket;
        ws.Cell(satir, 1).Style.Font.Bold = true;
        var cell = ws.Cell(satir, 2);
        cell.Value = deger;
        cell.Style.NumberFormat.Format = "#,##0.00";
        satir++;
    }

    private static void YazRezervasyonlarSayfasi(XLWorkbook workbook, OdemeDurumuRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Rezervasyonlar");

        string[] basliklar =
        [
            "Referans No",
            "Misafir Adı Soyadı",
            "Kurum/Ünite",
            "Giriş Tarihi",
            "Çıkış Tarihi",
            "Durum",
            "Oda No(ları)",
            "Kişi Sayısı",
            "Toplam Ücret",
            "Ödenen Tutar",
            "Kalan Tutar",
            "Ödeme Durumu",
            "Son Ödeme Tarihi",
            "Çıkış Yapmış mı"
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
            ws.Cell(satir, 3).Value = r.KurumUnite ?? string.Empty;

            var girisCell = ws.Cell(satir, 4);
            girisCell.Value = r.GirisTarihi;
            girisCell.Style.DateFormat.Format = "dd.MM.yyyy";

            var cikisCell = ws.Cell(satir, 5);
            cikisCell.Value = r.CikisTarihi;
            cikisCell.Style.DateFormat.Format = "dd.MM.yyyy";

            ws.Cell(satir, 6).Value = r.RezervasyonDurumuLabel;
            ws.Cell(satir, 7).Value = string.Join(", ", r.OdaNolari);
            ws.Cell(satir, 8).Value = r.KisiSayisi;

            var toplamCell = ws.Cell(satir, 9);
            toplamCell.Value = r.ToplamUcret;
            toplamCell.Style.NumberFormat.Format = "#,##0.00";

            var odenenCell = ws.Cell(satir, 10);
            odenenCell.Value = r.OdenenTutar;
            odenenCell.Style.NumberFormat.Format = "#,##0.00";

            var kalanCell = ws.Cell(satir, 11);
            kalanCell.Value = r.KalanTutar;
            kalanCell.Style.NumberFormat.Format = "#,##0.00";

            ws.Cell(satir, 12).Value = r.OdemeDurumuLabel;

            var sonOdemeCell = ws.Cell(satir, 13);
            if (r.SonOdemeTarihi.HasValue)
            {
                sonOdemeCell.Value = r.SonOdemeTarihi.Value;
                sonOdemeCell.Style.DateFormat.Format = "dd.MM.yyyy";
            }

            ws.Cell(satir, 14).Value = r.CikisYapmisMi ? "Evet" : "Hayır";

            var satirAraligi = ws.Range(satir, 1, satir, basliklar.Length);
            if (r.CikisYapmisBorcluMu)
            {
                satirAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml(RenkCikisYapmisBorcluSatir);
            }
            else if (r.KalanTutar > 0m)
            {
                satirAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml(RenkBorcluSatir);
            }

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

    private static string OdemeDurumuFiltreLabel(string filtre) => filtre switch
    {
        "tumu" => "Tümü",
        "borclu" => "Borçlu",
        "odemesi-yok" => "Ödemesi Yok",
        "kismi-odendi" => "Kısmi Ödendi",
        "tamamen-odendi" => "Tamamen Ödendi",
        "cikis-yapmis-borclu" => "Çıkış Yapmış Borçlu",
        _ => filtre
    };
}
