using System.Globalization;
using ClosedXML.Excel;
using STYS.Raporlar.Dto;
using STYS.Rezervasyonlar;

namespace STYS.Raporlar.Services;

public class OdaDolulukRaporExcelService : IOdaDolulukRaporExcelService
{
    private static readonly CultureInfo TrCulture = new("tr-TR");

    private const string RenkBos = "#FFFFFF";
    private const string RenkReserved = "#BDD7EE";
    private const string RenkOccupied = "#C6E0B4";
    private const string RenkCheckedOut = "#D9D9D9";
    private const string RenkOdemeEksik = "#FFE699";
    private const string RenkCakisma = "#F8CBAD";

    private readonly IOdaDolulukRaporService _odaDolulukRaporService;

    public OdaDolulukRaporExcelService(IOdaDolulukRaporService odaDolulukRaporService)
    {
        _odaDolulukRaporService = odaDolulukRaporService;
    }

    public async Task<byte[]> OlusturAsync(
        int tesisId,
        int yil,
        int ay,
        bool maskele = false,
        CancellationToken cancellationToken = default)
    {
        var rapor = await _odaDolulukRaporService.GetAylikOdaDolulukRaporuAsync(tesisId, yil, ay, maskele, cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Aylık Oda Planı");

        var ayAdi = ToTitleCase(TrCulture.DateTimeFormat.GetMonthName(ay));

        // ── Ust bilgi alani ──
        ws.Cell(1, 1).Value = "Aylık Oda Doluluk ve Tahsilat Raporu";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = $"Tesis: {rapor.TesisAdi}";
        ws.Cell(3, 1).Value = $"Dönem: {ayAdi} {yil}";
        ws.Cell(4, 1).Value = $"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}";
        ws.Cell(5, 1).Value = $"Kişisel Veriler: {(maskele ? "Maskeli" : "Açık")}";

        // ── Ozet alani ──
        var ozetBaslangic = 7;
        ws.Cell(ozetBaslangic, 1).Value = "ÖZET";
        ws.Cell(ozetBaslangic, 1).Style.Font.Bold = true;
        ws.Cell(ozetBaslangic, 1).Style.Font.FontSize = 12;

        var oz = rapor.Ozet;
        var ozetSatirlari = new (string Label, string Value)[]
        {
            ("Toplam Oda", oz.ToplamOdaSayisi.ToString()),
            ("Gün Sayısı", oz.GunSayisi.ToString()),
            ("Toplam Oda/Gün", oz.ToplamOdaGunSayisi.ToString()),
            ("Dolu Oda/Gün", oz.DoluOdaGunSayisi.ToString()),
            ("Boş Oda/Gün", oz.BosOdaGunSayisi.ToString()),
            ("Doluluk Oranı", $"%{oz.DolulukOraniYuzde.ToString("0.00", TrCulture)}"),
            ("Ay İçinde Tahsil Edilen", FormatPara(oz.AyIcindeTahsilEdilenTutar)),
            ("Konaklayan Rezervasyonların Toplam Tahsilatı", FormatPara(oz.KonaklayanRezervasyonlarinToplamTahsilati)),
            ("Konaklayan Rezervasyonların Toplam Kalan Tutarı", FormatPara(oz.KonaklayanRezervasyonlarinToplamKalanTutari))
        };

        var row = ozetBaslangic + 1;
        foreach (var (label, value) in ozetSatirlari)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = value;
            row++;
        }

        // ── Matris tablosu ──
        row += 1;
        var tabloBaslangic = row;

        ws.Cell(row, 1).Value = "Tarih";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");

        for (var i = 0; i < rapor.Odalar.Count; i++)
        {
            var cell = ws.Cell(row, i + 2);
            cell.Value = $"{rapor.Odalar[i].OdaNo} NO'LU ODA";
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        }

        var basliklarSatiri = row;
        row++;

        foreach (var gun in rapor.Gunler)
        {
            ws.Cell(row, 1).Value = $"{gun.Tarih.Day} {ayAdi} {gun.Tarih.Year} {gun.GunAdi}";
            ws.Cell(row, 1).Style.Alignment.WrapText = true;

            for (var i = 0; i < gun.Hucreler.Count; i++)
            {
                var hucre = gun.Hucreler[i];
                var cell = ws.Cell(row, i + 2);
                cell.Style.Alignment.WrapText = true;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                if (!hucre.DoluMu)
                {
                    continue;
                }

                cell.Value = HucreMetniOlustur(hucre);
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(HucreRengi(hucre));
            }

            row++;
        }

        var tabloSonu = row - 1;

        // ── Bicimlendirme ──
        ws.Column(1).Width = 24;
        for (var i = 0; i < rapor.Odalar.Count; i++)
        {
            ws.Column(i + 2).Width = 22;
        }

        for (var r = tabloBaslangic + 1; r <= tabloSonu; r++)
        {
            ws.Row(r).Height = 90;
        }

        ws.SheetView.Freeze(basliklarSatiri, 1);

        var tabloAraligi = ws.Range(basliklarSatiri, 1, tabloSonu, rapor.Odalar.Count + 1);
        tabloAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabloAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Range(basliklarSatiri, 1, basliklarSatiri, rapor.Odalar.Count + 1).SetAutoFilter();

        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PaperSize = XLPaperSize.A3Paper;
        ws.PageSetup.PrintAreas.Clear();
        ws.PageSetup.PrintAreas.Add(1, 1, tabloSonu, Math.Max(rapor.Odalar.Count + 1, 2));

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static string HucreMetniOlustur(OdaDolulukHucreDto hucre)
    {
        var satirlar = new List<string>
        {
            hucre.MisafirAdiSoyadi ?? "-",
            hucre.ReferansNo ?? "-",
            $"{hucre.KisiSayisi} kişi",
            DurumLabel(hucre.RezervasyonDurumu)
        };

        if (hucre.OdemesiEksikMi)
        {
            satirlar.Add("Ödeme Eksik");
        }

        if (hucre.CakismaVarMi)
        {
            satirlar.Add($"ÇAKIŞMA VAR ({hucre.CakismaSayisi})");
            satirlar.Add($"Ana: {hucre.MisafirAdiSoyadi ?? "-"} / {hucre.ReferansNo ?? "-"}");
            for (var i = 0; i < hucre.Cakismalar.Count; i++)
            {
                var c = hucre.Cakismalar[i];
                satirlar.Add($"{i + 1}) {c.MisafirAdiSoyadi ?? "-"} / {c.ReferansNo} / {c.GirisTarihi:dd.MM}-{c.CikisTarihi:dd.MM}");
            }
        }

        satirlar.Add($"Toplam: {FormatPara(hucre.ToplamUcret, hucre.ParaBirimi)}");
        satirlar.Add($"Ödenen: {FormatPara(hucre.OdenenTutar, hucre.ParaBirimi)}");
        satirlar.Add($"Kalan: {FormatPara(hucre.KalanTutar, hucre.ParaBirimi)}");

        return string.Join("\n", satirlar);
    }

    private static string HucreRengi(OdaDolulukHucreDto hucre)
    {
        return hucre.HucreRenkKodu switch
        {
            "conflict" => RenkCakisma,
            "payment-missing" => RenkOdemeEksik,
            "checked-out" => RenkCheckedOut,
            "occupied" => RenkOccupied,
            "reserved" => RenkReserved,
            _ => RenkBos
        };
    }

    private static string DurumLabel(string? durum)
    {
        return durum switch
        {
            RezervasyonDurumlari.Taslak => "Taslak",
            RezervasyonDurumlari.Onayli => "Onaylı",
            RezervasyonDurumlari.CheckInTamamlandi => "Check-in Tamamlandı",
            RezervasyonDurumlari.CheckOutTamamlandi => "Check-out Tamamlandı",
            RezervasyonDurumlari.Iptal => "İptal",
            _ => durum ?? "-"
        };
    }

    private static string FormatPara(decimal tutar, string? paraBirimi = "TRY")
    {
        return $"{tutar.ToString("#,##0.00", TrCulture)} {paraBirimi ?? "TRY"}";
    }

    private static string ToTitleCase(string value)
    {
        return value.Length == 0 ? value : TrCulture.TextInfo.ToTitleCase(value.ToLower(TrCulture));
    }
}
