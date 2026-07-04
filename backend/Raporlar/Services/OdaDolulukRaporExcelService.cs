using System.Globalization;
using ClosedXML.Excel;
using STYS.Raporlar.Dto;
using STYS.Rezervasyonlar;

namespace STYS.Raporlar.Services;

public class OdaDolulukRaporExcelService : IOdaDolulukRaporExcelService
{
    private static readonly CultureInfo TrCulture = new("tr-TR");
    private static readonly string[] KisaGunAdlari = ["Paz", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt"];

    private const string RenkBos = "#FFFFFF";
    private const string RenkReserved = "#BDD7EE";
    private const string RenkOccupied = "#C6E0B4";
    private const string RenkCheckedOut = "#D9D9D9";
    private const string RenkOdemeEksik = "#FFE699";
    private const string RenkCakisma = "#F8CBAD";
    private const string RenkHeaderHafta = "#1F4E78";
    private const string RenkHeaderHaftaSonu = "#2E5F8A";
    private const string RenkHaftaSonuBos = "#F7F7F7";
    private const string RenkHeaderYesil = "#2E7D32";
    private const int MisafirKisaMaxUzunluk = 14;
    private const int MisafirKisaMaxUzunlukTarihSatir = 26;

    private const string MatrisYonuOdaSatir = "oda-satir";
    private const string MatrisYonuTarihSatirVarsayilan = "tarih-satir";

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
        string? matrisYonu = null,
        CancellationToken cancellationToken = default)
    {
        var rapor = await _odaDolulukRaporService.GetAylikOdaDolulukRaporuAsync(tesisId, yil, ay, maskele, cancellationToken);
        var ayAdi = ToTitleCase(TrCulture.DateTimeFormat.GetMonthName(ay));
        var yon = string.IsNullOrWhiteSpace(matrisYonu) ? MatrisYonuTarihSatirVarsayilan : matrisYonu.Trim().ToLowerInvariant();

        using var workbook = new XLWorkbook();

        BuildOzetSheet(workbook, rapor, ayAdi, maskele);
        BuildOdaPlaniSheet(workbook, rapor, ayAdi, yon);
        BuildTahsilatlarSheet(workbook, rapor);
        BuildRezervasyonListesiSheet(workbook, rapor);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void BuildOzetSheet(XLWorkbook workbook, AylikOdaDolulukRaporDto rapor, string ayAdi, bool maskele)
    {
        var ws = workbook.Worksheets.Add("Özet");

        ws.Range(1, 1, 1, 8).Merge();
        ws.Cell(1, 1).Value = "Aylık Oda Doluluk ve Tahsilat Raporu";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;

        ws.Cell(3, 1).Value = "Tesis:";
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 2).Value = rapor.TesisAdi;

        ws.Cell(4, 1).Value = "Dönem:";
        ws.Cell(4, 1).Style.Font.Bold = true;
        ws.Cell(4, 2).Value = $"{ayAdi} {rapor.Yil}";

        ws.Cell(5, 1).Value = "Rapor Tarihi:";
        ws.Cell(5, 1).Style.Font.Bold = true;
        ws.Cell(5, 2).Value = DateTime.Now.ToString("dd.MM.yyyy HH:mm", TrCulture);

        ws.Cell(6, 1).Value = "Kişisel Veriler:";
        ws.Cell(6, 1).Style.Font.Bold = true;
        ws.Cell(6, 2).Value = maskele ? "Maskeli" : "Açık";

        var oz = rapor.Ozet;
        var kpiler = new (string Label, string Value)[]
        {
            ("Toplam Oda", oz.ToplamOdaSayisi.ToString(TrCulture)),
            ("Gün Sayısı", oz.GunSayisi.ToString(TrCulture)),
            ("Toplam Oda/Gün", oz.ToplamOdaGunSayisi.ToString(TrCulture)),
            ("Dolu Oda/Gün", oz.DoluOdaGunSayisi.ToString(TrCulture)),
            ("Boş Oda/Gün", oz.BosOdaGunSayisi.ToString(TrCulture)),
            ("Doluluk Oranı", FormatYuzde(oz.DolulukOraniYuzde)),
            ("Ay İçinde Tahsil Edilen", FormatPara(oz.AyIcindeTahsilEdilenTutar)),
            ("Konaklayan Rezervasyonların Toplam Tahsilatı", FormatPara(oz.KonaklayanRezervasyonlarinToplamTahsilati)),
            ("Konaklayan Rezervasyonların Toplam Kalan Tutarı", FormatPara(oz.KonaklayanRezervasyonlarinToplamKalanTutari))
        };

        const int kpiBaseRow = 8;
        const int kutuGenislik = 2;
        const int kutuBosluk = 1;
        const int satirdakiKutuSayisi = 3;

        ws.Cell(kpiBaseRow - 1, 1).Value = "KPI ÖZETİ";
        ws.Cell(kpiBaseRow - 1, 1).Style.Font.Bold = true;
        ws.Cell(kpiBaseRow - 1, 1).Style.Font.FontSize = 12;

        for (var i = 0; i < kpiler.Length; i++)
        {
            var satirIndex = i / satirdakiKutuSayisi;
            var kutuIndex = i % satirdakiKutuSayisi;
            var startCol = kutuIndex * (kutuGenislik + kutuBosluk) + 1;
            var labelRow = kpiBaseRow + satirIndex * 3;
            var valueRow = labelRow + 1;

            var labelRange = ws.Range(labelRow, startCol, labelRow, startCol + kutuGenislik - 1);
            labelRange.Merge();
            var labelCell = ws.Cell(labelRow, startCol);
            labelCell.Value = kpiler[i].Label;
            labelCell.Style.Font.FontSize = 9;
            labelCell.Style.Font.Bold = true;
            labelCell.Style.Font.FontColor = XLColor.FromHtml("#666666");

            var valueRange = ws.Range(valueRow, startCol, valueRow, startCol + kutuGenislik - 1);
            valueRange.Merge();
            var valueCell = ws.Cell(valueRow, startCol);
            valueCell.Value = kpiler[i].Value;
            valueCell.Style.Font.FontSize = 13;
            valueCell.Style.Font.Bold = true;

            var kutuAraligi = ws.Range(labelRow, startCol, valueRow, startCol + kutuGenislik - 1);
            kutuAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            kutuAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");
        }

        ws.Column(1).Width = 24;
        for (var c = 2; c <= 8; c++)
        {
            ws.Column(c).Width = 16;
        }

        ws.SheetView.Freeze(2, 0);
    }

    private static void BuildOdaPlaniSheet(XLWorkbook workbook, AylikOdaDolulukRaporDto rapor, string ayAdi, string matrisYonu)
    {
        if (matrisYonu == MatrisYonuOdaSatir)
        {
            BuildOdaSatirGunKolonSheet(workbook, rapor, ayAdi);
        }
        else
        {
            BuildTarihSatirOdaKolonSheet(workbook, rapor, ayAdi);
        }
    }

    private static void BuildTarihSatirOdaKolonSheet(XLWorkbook workbook, AylikOdaDolulukRaporDto rapor, string ayAdi)
    {
        var ws = workbook.Worksheets.Add("Oda Planı");

        const int headerRow = 1;
        const int ilkOdaKolonu = 3;

        ws.Cell(headerRow, 1).Value = "TARİH";
        ws.Cell(headerRow, 2).Value = "GÜN";

        for (var o = 0; o < rapor.Odalar.Count; o++)
        {
            ws.Cell(headerRow, ilkOdaKolonu + o).Value = $"{rapor.Odalar[o].OdaNo} NO'LU ODA";
        }

        var sonKolon = ilkOdaKolonu + rapor.Odalar.Count - 1;
        var basliklarAraligi = ws.Range(headerRow, 1, headerRow, sonKolon);
        basliklarAraligi.Style.Font.Bold = true;
        basliklarAraligi.Style.Font.FontColor = XLColor.White;
        basliklarAraligi.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        basliklarAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml(RenkHeaderYesil);
        basliklarAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        basliklarAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var satir = headerRow + 1;
        for (var g = 0; g < rapor.Gunler.Count; g++)
        {
            var gun = rapor.Gunler[g];
            var haftaSonuMu = gun.Tarih.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            ws.Cell(satir, 1).Value = $"{gun.Tarih.Day} {ayAdi} {gun.Tarih.Year}";
            ws.Cell(satir, 2).Value = gun.GunAdi;
            if (haftaSonuMu)
            {
                ws.Range(satir, 1, satir, 2).Style.Fill.BackgroundColor = XLColor.FromHtml(RenkHaftaSonuBos);
            }

            for (var o = 0; o < rapor.Odalar.Count; o++)
            {
                var hucre = gun.Hucreler[o];
                var cell = ws.Cell(satir, ilkOdaKolonu + o);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Alignment.WrapText = true;

                if (hucre.DoluMu)
                {
                    cell.Value = KisaHucreMetniTarihSatir(hucre);
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml(HucreRengi(hucre));
                }
            }

            satir++;
        }

        var sonSatir = satir - 1;

        // ── Bicimlendirme ──
        ws.Column(1).Width = 18;
        ws.Column(2).Width = 14;
        for (var o = 0; o < rapor.Odalar.Count; o++)
        {
            ws.Column(ilkOdaKolonu + o).Width = 24;
        }

        ws.Row(headerRow).Height = 28;
        for (var r = headerRow + 1; r <= sonSatir; r++)
        {
            ws.Row(r).Height = 24;
        }

        ws.SheetView.Freeze(headerRow, 2);

        var tabloAraligi = ws.Range(headerRow, 1, sonSatir, sonKolon);
        tabloAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabloAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        BuildRenkLegend(ws, headerRow, sonKolon + 2);
        BuildTahsilatTablosuOdaPlani(ws, rapor, sonSatir + 3);

        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PaperSize = XLPaperSize.A3Paper;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.PrintAreas.Clear();
        ws.PageSetup.PrintAreas.Add(headerRow, 1, sonSatir, sonKolon);
    }

    // Musteri PDF ornegindeki gibi doluluk matrisinin altina sade bir tahsilat tablosu ekler.
    private static void BuildTahsilatTablosuOdaPlani(IXLWorksheet ws, AylikOdaDolulukRaporDto rapor, int baseRow)
    {
        var baslikCell = ws.Cell(baseRow, 1);
        baslikCell.Value = "TAHSİLATLAR";
        baslikCell.Style.Font.Bold = true;
        baslikCell.Style.Font.FontSize = 12;

        var headerRow = baseRow + 1;
        var basliklar = new[] { "ODA NUMARASI", "MAKBUZ NO", "ÖDEME YAPAN", "ÜNİTESİ", "TAHSİL EDİLEN" };
        for (var i = 0; i < basliklar.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = basliklar[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
        }

        var satir = headerRow + 1;
        if (rapor.Tahsilatlar.Count == 0)
        {
            ws.Range(satir, 1, satir, basliklar.Length).Merge();
            ws.Cell(satir, 1).Value = "Bu dönem için tahsilat kaydı bulunamadı.";
            satir++;
        }
        else
        {
            foreach (var tahsilat in rapor.Tahsilatlar)
            {
                ws.Cell(satir, 1).Value = tahsilat.OdaNo ?? "-";
                // TODO: RezervasyonOdeme entity'sine MakbuzNo alani eklendiginde buradan doldurulacak.
                ws.Cell(satir, 2).Value = tahsilat.MakbuzNo ?? "-";
                ws.Cell(satir, 3).Value = tahsilat.OdemeYapan ?? tahsilat.MisafirAdiSoyadi ?? "-";
                ws.Cell(satir, 4).Value = tahsilat.KurumUnite ?? "-";

                var tutarCell = ws.Cell(satir, 5);
                tutarCell.Value = FormatPara(tahsilat.OdemeTutari, tahsilat.ParaBirimi);
                tutarCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                satir++;
            }
        }

        var sonSatir = satir - 1;
        var tabloAraligi = ws.Range(headerRow, 1, sonSatir, basliklar.Length);
        tabloAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabloAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void BuildTahsilatlarSheet(XLWorkbook workbook, AylikOdaDolulukRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Tahsilatlar");

        var basliklar = new[]
        {
            "Oda No", "Misafir / Ödeme Yapan", "Ünite", "Referans No", "Giriş Tarihi", "Çıkış Tarihi",
            "Ödeme Tipi", "Tahsil Edilen", "Para Birimi", "Açıklama"
        };

        for (var i = 0; i < basliklar.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = basliklar[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        }

        var satir = 2;
        if (rapor.Tahsilatlar.Count == 0)
        {
            ws.Range(satir, 1, satir, basliklar.Length).Merge();
            ws.Cell(satir, 1).Value = "Bu dönem için tahsilat kaydı bulunamadı.";
            satir++;
        }
        else
        {
            // TODO: RezervasyonOdeme detaylari (MakbuzNo, gercek OdemeYapan, OdemeTipi) rapor DTO'suna eklendiginde buradan doldurulacak.
            foreach (var tahsilat in rapor.Tahsilatlar)
            {
                ws.Cell(satir, 1).Value = tahsilat.OdaNo ?? "-";
                ws.Cell(satir, 2).Value = tahsilat.OdemeYapan ?? tahsilat.MisafirAdiSoyadi ?? "-";
                ws.Cell(satir, 3).Value = tahsilat.KurumUnite ?? "-";
                ws.Cell(satir, 4).Value = tahsilat.ReferansNo ?? "-";

                var girisCell = ws.Cell(satir, 5);
                if (tahsilat.GirisTarihi.HasValue)
                {
                    girisCell.Value = tahsilat.GirisTarihi.Value;
                    girisCell.Style.DateFormat.Format = "dd.MM.yyyy";
                }
                else
                {
                    girisCell.Value = "-";
                }

                var cikisCell = ws.Cell(satir, 6);
                if (tahsilat.CikisTarihi.HasValue)
                {
                    cikisCell.Value = tahsilat.CikisTarihi.Value;
                    cikisCell.Style.DateFormat.Format = "dd.MM.yyyy";
                }
                else
                {
                    cikisCell.Value = "-";
                }

                ws.Cell(satir, 7).Value = tahsilat.OdemeTipi ?? "-";

                var tutarCell = ws.Cell(satir, 8);
                tutarCell.Value = FormatPara(tahsilat.OdemeTutari, tahsilat.ParaBirimi);
                tutarCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Cell(satir, 9).Value = tahsilat.ParaBirimi;
                ws.Cell(satir, 10).Value = tahsilat.Aciklama ?? "-";

                satir++;
            }
        }

        var sonSatir = Math.Max(satir - 1, 1);

        ws.Column(1).Width = 12;
        ws.Column(2).Width = 26;
        ws.Column(3).Width = 20;
        ws.Column(4).Width = 16;
        ws.Column(5).Width = 14;
        ws.Column(6).Width = 14;
        ws.Column(7).Width = 16;
        ws.Column(8).Width = 16;
        ws.Column(9).Width = 12;
        ws.Column(10).Width = 24;

        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, sonSatir, basliklar.Length).SetAutoFilter();
    }

    private static void BuildRenkLegend(IXLWorksheet ws, int baseRow, int baseCol)
    {
        ws.Range(baseRow, baseCol, baseRow, baseCol + 1).Merge();
        var baslikCell = ws.Cell(baseRow, baseCol);
        baslikCell.Value = "RENK AÇIKLAMALARI";
        baslikCell.Style.Font.Bold = true;
        baslikCell.Style.Font.FontSize = 11;

        var girdiler = new (string Renk, string Aciklama)[]
        {
            (RenkReserved, "Onaylı / Rezerve"),
            (RenkOccupied, "Check-in Tamamlandı"),
            (RenkCheckedOut, "Check-out Tamamlandı"),
            (RenkOdemeEksik, "Ödeme Eksik"),
            (RenkCakisma, "Çakışma")
        };

        for (var i = 0; i < girdiler.Length; i++)
        {
            var row = baseRow + 1 + i;
            var renkCell = ws.Cell(row, baseCol);
            renkCell.Value = " ";
            renkCell.Style.Fill.BackgroundColor = XLColor.FromHtml(girdiler[i].Renk);
            renkCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            var aciklamaCell = ws.Cell(row, baseCol + 1);
            aciklamaCell.Value = girdiler[i].Aciklama;
        }

        ws.Column(baseCol).Width = 4;
        ws.Column(baseCol + 1).Width = 24;
    }

    private static void BuildOdaSatirGunKolonSheet(XLWorkbook workbook, AylikOdaDolulukRaporDto rapor, string ayAdi)
    {
        var ws = workbook.Worksheets.Add("Oda Planı");

        const int headerRow = 1;
        const int ilkGunKolonu = 4;

        ws.Cell(headerRow, 1).Value = "Oda No";
        ws.Cell(headerRow, 2).Value = "Oda Tipi";
        ws.Cell(headerRow, 3).Value = "Kapasite";

        for (var g = 0; g < rapor.Gunler.Count; g++)
        {
            var gun = rapor.Gunler[g].Tarih;
            var col = ilkGunKolonu + g;
            var cell = ws.Cell(headerRow, col);
            cell.Value = $"{gun.Day} {KisaGunAdi(gun)}";

            var haftaSonuMu = gun.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(haftaSonuMu ? RenkHeaderHaftaSonu : RenkHeaderHafta);
        }

        var basliklarAraligi = ws.Range(headerRow, 1, headerRow, ilkGunKolonu + rapor.Gunler.Count - 1);
        basliklarAraligi.Style.Font.Bold = true;
        basliklarAraligi.Style.Font.FontColor = XLColor.White;
        basliklarAraligi.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        basliklarAraligi.Style.Fill.BackgroundColor = XLColor.FromHtml(RenkHeaderHafta);
        for (var g = 0; g < rapor.Gunler.Count; g++)
        {
            var haftaSonuMu = rapor.Gunler[g].Tarih.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (haftaSonuMu)
            {
                ws.Cell(headerRow, ilkGunKolonu + g).Style.Fill.BackgroundColor = XLColor.FromHtml(RenkHeaderHaftaSonu);
            }
        }

        var satir = headerRow + 1;
        for (var o = 0; o < rapor.Odalar.Count; o++)
        {
            var oda = rapor.Odalar[o];
            ws.Cell(satir, 1).Value = oda.OdaNo;
            ws.Cell(satir, 2).Value = oda.OdaTipiAdi ?? "-";
            ws.Cell(satir, 3).Value = oda.Kapasite;

            for (var g = 0; g < rapor.Gunler.Count; g++)
            {
                var hucre = rapor.Gunler[g].Hucreler[o];
                var cell = ws.Cell(satir, ilkGunKolonu + g);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Alignment.WrapText = true;

                if (hucre.DoluMu)
                {
                    cell.Value = KisaHucreMetni(hucre);
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml(HucreRengi(hucre));
                }
                else if (rapor.Gunler[g].Tarih.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml(RenkHaftaSonuBos);
                }
            }

            satir++;
        }

        var sonSatir = satir - 1;
        var sonKolon = ilkGunKolonu + rapor.Gunler.Count - 1;

        // ── Bicimlendirme ──
        ws.Column(1).Width = 12;
        ws.Column(2).Width = 18;
        ws.Column(3).Width = 10;
        for (var g = 0; g < rapor.Gunler.Count; g++)
        {
            ws.Column(ilkGunKolonu + g).Width = 13;
        }

        ws.Row(headerRow).Height = 30;
        for (var r = headerRow + 1; r <= sonSatir; r++)
        {
            ws.Row(r).Height = 36;
        }

        ws.SheetView.Freeze(headerRow, 3);

        var tabloAraligi = ws.Range(headerRow, 1, sonSatir, sonKolon);
        tabloAraligi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabloAraligi.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Range(headerRow, 1, sonSatir, 3).SetAutoFilter();

        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PaperSize = XLPaperSize.A3Paper;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.PrintAreas.Clear();
        ws.PageSetup.PrintAreas.Add(headerRow, 1, sonSatir, sonKolon);
    }

    private static void BuildRezervasyonListesiSheet(XLWorkbook workbook, AylikOdaDolulukRaporDto rapor)
    {
        var ws = workbook.Worksheets.Add("Rezervasyon Listesi");

        var basliklar = new[]
        {
            "Tarih", "Oda No", "Misafir", "Referans No", "Kişi Sayısı", "Giriş Tarihi", "Çıkış Tarihi",
            "Durum", "Toplam Ücret", "Ödenen Tutar", "Kalan Tutar", "Para Birimi", "Ödeme Eksik Mi",
            "Çakışma Var Mı", "Çakışma Sayısı"
        };

        for (var i = 0; i < basliklar.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = basliklar[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        }

        var eklenenler = new HashSet<(int RezervasyonId, int OdaId, DateTime Giris, DateTime Cikis)>();
        var satir = 2;

        foreach (var gun in rapor.Gunler)
        {
            foreach (var hucre in gun.Hucreler)
            {
                if (!hucre.DoluMu || !hucre.RezervasyonId.HasValue || !hucre.GirisTarihi.HasValue || !hucre.CikisTarihi.HasValue)
                {
                    continue;
                }

                var anahtar = (hucre.RezervasyonId.Value, hucre.OdaId, hucre.GirisTarihi.Value, hucre.CikisTarihi.Value);
                if (!eklenenler.Add(anahtar))
                {
                    continue;
                }

                ws.Cell(satir, 1).Value = hucre.GirisTarihi.Value;
                ws.Cell(satir, 1).Style.DateFormat.Format = "dd.MM.yyyy";
                ws.Cell(satir, 2).Value = hucre.OdaNo;
                ws.Cell(satir, 3).Value = hucre.MisafirAdiSoyadi ?? "-";
                ws.Cell(satir, 4).Value = hucre.ReferansNo ?? "-";
                ws.Cell(satir, 5).Value = hucre.KisiSayisi;
                ws.Cell(satir, 6).Value = hucre.GirisTarihi.Value;
                ws.Cell(satir, 6).Style.DateFormat.Format = "dd.MM.yyyy";
                ws.Cell(satir, 7).Value = hucre.CikisTarihi.Value;
                ws.Cell(satir, 7).Style.DateFormat.Format = "dd.MM.yyyy";
                ws.Cell(satir, 8).Value = DurumLabel(hucre.RezervasyonDurumu);
                ws.Cell(satir, 9).Value = FormatPara(hucre.ToplamUcret, hucre.ParaBirimi);
                ws.Cell(satir, 10).Value = FormatPara(hucre.OdenenTutar, hucre.ParaBirimi);
                ws.Cell(satir, 11).Value = FormatPara(hucre.KalanTutar, hucre.ParaBirimi);
                ws.Cell(satir, 12).Value = hucre.ParaBirimi ?? "TRY";
                ws.Cell(satir, 13).Value = hucre.OdemesiEksikMi ? "Evet" : "Hayır";
                ws.Cell(satir, 14).Value = hucre.CakismaVarMi ? "Evet" : "Hayır";
                ws.Cell(satir, 15).Value = hucre.CakismaSayisi;

                if (hucre.CakismaVarMi)
                {
                    ws.Row(satir).Style.Fill.BackgroundColor = XLColor.FromHtml(RenkCakisma);
                }
                else if (hucre.OdemesiEksikMi)
                {
                    ws.Row(satir).Style.Fill.BackgroundColor = XLColor.FromHtml(RenkOdemeEksik);
                }

                satir++;
            }
        }

        var sonSatir = Math.Max(satir - 1, 1);

        ws.Column(1).Width = 12;
        ws.Column(2).Width = 10;
        ws.Column(3).Width = 24;
        ws.Column(4).Width = 16;
        ws.Column(5).Width = 12;
        ws.Column(6).Width = 14;
        ws.Column(7).Width = 14;
        ws.Column(8).Width = 20;
        ws.Column(9).Width = 16;
        ws.Column(10).Width = 16;
        ws.Column(11).Width = 16;
        ws.Column(12).Width = 12;
        ws.Column(13).Width = 14;
        ws.Column(14).Width = 14;
        ws.Column(15).Width = 14;

        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, sonSatir, basliklar.Length).SetAutoFilter();
    }

    private static string KisaHucreMetni(OdaDolulukHucreDto hucre)
    {
        if (hucre.CakismaVarMi)
        {
            return "ÇAKIŞMA";
        }

        var misafirKisa = KisaltMisafirVeyaReferans(hucre);
        return hucre.OdemesiEksikMi ? $"{misafirKisa}\nEksik" : misafirKisa;
    }

    private static string KisaltMisafirVeyaReferans(OdaDolulukHucreDto hucre)
    {
        var kaynak = !string.IsNullOrWhiteSpace(hucre.MisafirAdiSoyadi) ? hucre.MisafirAdiSoyadi! : (hucre.ReferansNo ?? "-");
        return kaynak.Length > MisafirKisaMaxUzunluk ? kaynak[..MisafirKisaMaxUzunluk] + "…" : kaynak;
    }

    // Tarih-satir (musteri) formatinda hucrede durum/odeme metni yazilmaz; sadece cakisma istisnadir.
    private static string KisaHucreMetniTarihSatir(OdaDolulukHucreDto hucre)
    {
        if (hucre.CakismaVarMi)
        {
            return "ÇAKIŞMA";
        }

        var kaynak = !string.IsNullOrWhiteSpace(hucre.KurumUnite)
            ? hucre.KurumUnite!
            : !string.IsNullOrWhiteSpace(hucre.MisafirAdiSoyadi)
                ? hucre.MisafirAdiSoyadi!
                : (hucre.ReferansNo ?? "-");

        return kaynak.Length > MisafirKisaMaxUzunlukTarihSatir
            ? kaynak[..MisafirKisaMaxUzunlukTarihSatir] + "…"
            : kaynak;
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

    private static string KisaGunAdi(DateTime tarih)
    {
        return KisaGunAdlari[(int)tarih.DayOfWeek];
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
        var birim = paraBirimi ?? "TRY";
        var sembol = birim == "TRY" ? "₺" : birim;
        return $"{tutar.ToString("#,##0.00", TrCulture)} {sembol}";
    }

    private static string FormatYuzde(decimal deger)
    {
        return $"%{deger.ToString("0.00", TrCulture)}";
    }

    private static string ToTitleCase(string value)
    {
        return value.Length == 0 ? value : TrCulture.TextInfo.ToTitleCase(value.ToLower(TrCulture));
    }
}
