using ClosedXML.Excel;
using STYS.Raporlar.GunlukGirisCikis.Dto;

namespace STYS.Raporlar.GunlukGirisCikis.Services;

public class GunlukGirisCikisRaporExcelService : IGunlukGirisCikisRaporExcelService
{
    private const string RenkGecikenCikisSatir = "#F8CBAD";
    private const string RenkGirisSatir = "#E2EFDA";
    private const string RenkCikisSatir = "#DDEBF7";
    private const string RenkDevamEdenSatir = "#F2F2F2";
    private const string RenkKalanTutarDikkat = "#FFC7CE";
    private const string RenkHeader = "#D9E1F2";

    private readonly IGunlukGirisCikisRaporService _gunlukGirisCikisRaporService;

    public GunlukGirisCikisRaporExcelService(IGunlukGirisCikisRaporService gunlukGirisCikisRaporService)
    {
        _gunlukGirisCikisRaporService = gunlukGirisCikisRaporService;
    }

    public async Task<byte[]> OlusturAsync(
        int tesisId,
        DateTime tarih,
        string? listeTipi = null,
        CancellationToken cancellationToken = default)
    {
        var rapor = await _gunlukGirisCikisRaporService.GetRaporAsync(tesisId, tarih, listeTipi, cancellationToken);

        using var workbook = new XLWorkbook();
        YazOzetSayfasi(workbook, rapor);
        YazListeSayfasi(workbook, rapor);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void YazOzetSayfasi(XLWorkbook workbook, GunlukGirisCikisRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Özet");

        ws.Cell(1, 1).Value = "GÜNLÜK GİRİŞ-ÇIKIŞ LİSTESİ";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 2).Merge();

        var satir = 3;
        YazEtiketDeger(ws, ref satir, "Tesis", rapor.TesisAdi ?? string.Empty);
        YazEtiketDeger(ws, ref satir, "Tarih", rapor.Tarih.ToString("dd.MM.yyyy"));
        YazEtiketDeger(ws, ref satir, "Liste Tipi", ListeTipiLabel(rapor.ListeTipi));

        satir++;
        YazEtiketDeger(ws, ref satir, "Giriş Sayısı", rapor.Ozet.GirisSayisi);
        YazEtiketDeger(ws, ref satir, "Çıkış Sayısı", rapor.Ozet.CikisSayisi);
        YazEtiketDeger(ws, ref satir, "Devam Eden Sayısı", rapor.Ozet.DevamEdenSayisi);
        YazEtiketDeger(ws, ref satir, "Geciken Çıkış Sayısı", rapor.Ozet.GecikenCikisSayisi);
        YazEtiketDeger(ws, ref satir, "Toplam Rezervasyon", rapor.Ozet.ToplamRezervasyonSayisi);
        YazEtiketDeger(ws, ref satir, "Toplam Kişi", rapor.Ozet.ToplamKisiSayisi);

        var kalanTutarCell = ws.Cell(satir, 2);
        ws.Cell(satir, 1).Value = "Toplam Kalan Tutar";
        ws.Cell(satir, 1).Style.Font.Bold = true;
        kalanTutarCell.Value = rapor.Ozet.ToplamKalanTutar;
        kalanTutarCell.Style.NumberFormat.Format = ParaFormati(rapor.Ozet.ParaBirimi);
        satir++;

        ws.Column(1).Width = 32;
        ws.Column(2).Width = 24;
    }

    private static void YazEtiketDeger(IXLWorksheet ws, ref int satir, string etiket, object deger)
    {
        ws.Cell(satir, 1).Value = etiket;
        ws.Cell(satir, 1).Style.Font.Bold = true;
        ws.Cell(satir, 2).Value = XLCellValue.FromObject(deger);
        satir++;
    }

    private static void YazListeSayfasi(XLWorkbook workbook, GunlukGirisCikisRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Liste");

        string[] basliklar =
        [
            "Liste Durumu",
            "Referans No",
            "Misafir Adı Soyadı",
            "Kurum/Ünite",
            "Oda No(ları)",
            "Kişi Sayısı",
            "Giriş Tarihi",
            "Çıkış Tarihi",
            "Rezervasyon Durumu",
            "Toplam Ücret",
            "Ödenen Tutar",
            "Kalan Tutar",
            "Para Birimi",
            "Açıklama"
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
            ws.Cell(satir, 1).Value = r.ListeDurumuLabel;
            ws.Cell(satir, 2).Value = r.ReferansNo;
            ws.Cell(satir, 3).Value = r.MisafirAdiSoyadi;
            ws.Cell(satir, 4).Value = r.KurumUnite ?? string.Empty;
            ws.Cell(satir, 5).Value = string.Join(", ", r.OdaNolari);
            ws.Cell(satir, 6).Value = r.KisiSayisi;

            var girisCell = ws.Cell(satir, 7);
            girisCell.Value = r.GirisTarihi;
            girisCell.Style.DateFormat.Format = "dd.MM.yyyy";

            var cikisCell = ws.Cell(satir, 8);
            cikisCell.Value = r.CikisTarihi;
            cikisCell.Style.DateFormat.Format = "dd.MM.yyyy";

            ws.Cell(satir, 9).Value = r.RezervasyonDurumuLabel;

            var paraFormati = ParaFormati(r.ParaBirimi);

            var toplamCell = ws.Cell(satir, 10);
            toplamCell.Value = r.ToplamUcret;
            toplamCell.Style.NumberFormat.Format = paraFormati;

            var odenenCell = ws.Cell(satir, 11);
            odenenCell.Value = r.OdenenTutar;
            odenenCell.Style.NumberFormat.Format = paraFormati;

            var kalanCell = ws.Cell(satir, 12);
            kalanCell.Value = r.KalanTutar;
            kalanCell.Style.NumberFormat.Format = paraFormati;

            ws.Cell(satir, 13).Value = r.ParaBirimi;
            ws.Cell(satir, 14).Value = r.Aciklama ?? string.Empty;

            // Once satir durum rengi uygulanir, ardindan Kalan Tutar > 0 ise o hucre ayrica
            // dikkat rengiyle boyanir; aksi halde satir rengi bu uyariyi ezerdi.
            var satirAraligi = ws.Range(satir, 1, satir, basliklar.Length);
            var satirRengi = SatirRengi(r);
            if (satirRengi is not null)
            {
                satirAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml(satirRengi);
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
            ws.RangeUsed()?.SetAutoFilter();
        }

        ws.SheetView.Freeze(1, 0);
        ws.Columns().AdjustToContents();
    }

    // Satir rengi, oncelik sirasina gore secilir: geciken cikis > giris > cikis > devam eden.
    // "giris-cikis" (gunubirlik) kaydi giris rengiyle vurgulanir.
    private static string? SatirRengi(GunlukGirisCikisRezervasyonDto r)
    {
        if (r.GecikenCikisMi)
        {
            return RenkGecikenCikisSatir;
        }

        if (r.GirisYapacakMi)
        {
            return RenkGirisSatir;
        }

        if (r.CikisYapacakMi)
        {
            return RenkCikisSatir;
        }

        if (r.DevamEdiyorMu)
        {
            return RenkDevamEdenSatir;
        }

        return null;
    }

    // Para birimi TRY ise ₺ sembolu, degilse ParaBirimi kodu tutar formatinin sonuna eklenir.
    private static string ParaFormati(string paraBirimi)
    {
        var sembol = string.IsNullOrWhiteSpace(paraBirimi) || paraBirimi.Equals("TRY", StringComparison.OrdinalIgnoreCase)
            ? "₺"
            : paraBirimi;
        return $"#,##0.00 \"{sembol}\"";
    }

    private static string ListeTipiLabel(string listeTipi) => listeTipi switch
    {
        "tumu" => "Tümü",
        "girisler" => "Girişler",
        "cikislar" => "Çıkışlar",
        "devam-edenler" => "Devam Edenler",
        "geciken-cikislar" => "Geciken Çıkışlar",
        _ => listeTipi
    };
}
