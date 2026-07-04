using System.Globalization;
using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using STYS.Raporlar.Dto;
using STYS.Rezervasyonlar;

namespace STYS.Raporlar.Services;

public class OdaDolulukRaporPdfService : IOdaDolulukRaporPdfService
{
    private static readonly CultureInfo TrCulture = new("tr-TR");

    // Bir sayfaya sigacak maksimum oda kolonu sayisi; fazlasi sonraki sayfaya tasinir.
    private const int OdaSutunSayfaLimiti = 8;
    private const int KisaMetinMaxUzunluk = 22;

    private static readonly Color RenkBos = ColorConstants.WHITE;
    private static readonly Color RenkReserved = new DeviceRgb(0xBD, 0xD7, 0xEE);
    private static readonly Color RenkOccupied = new DeviceRgb(0xC6, 0xE0, 0xB4);
    private static readonly Color RenkCheckedOut = new DeviceRgb(0xD9, 0xD9, 0xD9);
    private static readonly Color RenkOdemeEksik = new DeviceRgb(0xFF, 0xE6, 0x99);
    private static readonly Color RenkCakisma = new DeviceRgb(0xF8, 0xCB, 0xAD);
    private static readonly Color RenkHeader = new DeviceRgb(0x2E, 0x7D, 0x32);
    private static readonly Color RenkOzetKutu = new DeviceRgb(0xF5, 0xF7, 0xFA);

    // Windows (gelistirme) ve yaygin Linux fontconfig konumlarinda Turkce karakterleri destekleyen
    // gercek bir TTF ariyoruz; bulunamazsa base-14 Helvetica + Cp1254 + ASCII normalizasyonuna dusulur.
    private static readonly string[] RegularFontYollari =
    [
        @"C:\Windows\Fonts\segoeui.ttf",
        @"C:\Windows\Fonts\arial.ttf",
        @"C:\Windows\Fonts\calibri.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf"
    ];

    private static readonly string[] BoldFontYollari =
    [
        @"C:\Windows\Fonts\segoeuib.ttf",
        @"C:\Windows\Fonts\arialbd.ttf",
        @"C:\Windows\Fonts\calibrib.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        "/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf"
    ];

    private readonly IOdaDolulukRaporService _odaDolulukRaporService;

    public OdaDolulukRaporPdfService(IOdaDolulukRaporService odaDolulukRaporService)
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
        var ayAdi = ToTitleCase(TrCulture.DateTimeFormat.GetMonthName(ay));

        using var stream = new MemoryStream();
        using (var writer = new PdfWriter(stream))
        using (var pdf = new PdfDocument(writer))
        using (var document = new Document(pdf, PageSize.A3.Rotate()))
        {
            document.SetMargins(20, 20, 20, 20);

            var (regularFont, boldFont, turkceDestekli) = FontlariYukle();
            document.SetFont(regularFont);

            var odaGruplari = GrupOdalar(rapor.Odalar);

            for (var sayfaIndex = 0; sayfaIndex < odaGruplari.Count; sayfaIndex++)
            {
                if (sayfaIndex == 0)
                {
                    AddBaslikBolumu(document, rapor, ayAdi, maskele, boldFont, regularFont, turkceDestekli);
                    AddOzetBolumu(document, rapor, boldFont, regularFont, turkceDestekli);
                    AddRenkLegend(document, boldFont, regularFont, turkceDestekli);
                }
                else
                {
                    document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                }

                AddOdaPlaniTablosu(document, rapor, ayAdi, odaGruplari[sayfaIndex], boldFont, regularFont, turkceDestekli);
            }

            document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
            AddTahsilatlarBolumu(document, rapor, boldFont, regularFont, turkceDestekli);

            document.Close();
        }

        return stream.ToArray();
    }

    private static List<List<OdaDolulukOdaDto>> GrupOdalar(List<OdaDolulukOdaDto> odalar)
    {
        var gruplar = odalar
            .Select((oda, index) => new { oda, index })
            .GroupBy(x => x.index / OdaSutunSayfaLimiti)
            .Select(g => g.Select(x => x.oda).ToList())
            .ToList();

        return gruplar.Count == 0 ? [[]] : gruplar;
    }

    private static void AddBaslikBolumu(
        Document document,
        AylikOdaDolulukRaporDto rapor,
        string ayAdi,
        bool maskele,
        PdfFont boldFont,
        PdfFont regularFont,
        bool turkceDestekli)
    {
        document.Add(new Paragraph(Metin("Aylık Oda Doluluk ve Tahsilat Raporu", turkceDestekli))
            .SetFont(boldFont)
            .SetFontSize(18));

        document.Add(new Paragraph(Metin($"Tesis: {rapor.TesisAdi}", turkceDestekli)).SetFont(regularFont).SetFontSize(10));
        document.Add(new Paragraph(Metin($"Dönem: {ayAdi} {rapor.Yil}", turkceDestekli)).SetFont(regularFont).SetFontSize(10));
        document.Add(new Paragraph(Metin($"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}", turkceDestekli)).SetFont(regularFont).SetFontSize(10));
        document.Add(new Paragraph(Metin($"Kişisel Veriler: {(maskele ? "Maskeli" : "Açık")}", turkceDestekli)).SetFont(regularFont).SetFontSize(10));
        document.Add(new Paragraph(" ").SetFontSize(4));
    }

    private static void AddOzetBolumu(
        Document document,
        AylikOdaDolulukRaporDto rapor,
        PdfFont boldFont,
        PdfFont regularFont,
        bool turkceDestekli)
    {
        var oz = rapor.Ozet;
        var kpiler = new (string Label, string Value)[]
        {
            ("Toplam Oda", oz.ToplamOdaSayisi.ToString(TrCulture)),
            ("Dolu Oda/Gün", oz.DoluOdaGunSayisi.ToString(TrCulture)),
            ("Boş Oda/Gün", oz.BosOdaGunSayisi.ToString(TrCulture)),
            ("Doluluk Oranı", FormatYuzde(oz.DolulukOraniYuzde)),
            ("Ay İçinde Tahsil Edilen", FormatPara(oz.AyIcindeTahsilEdilenTutar)),
            ("Toplam Kalan Tutar", FormatPara(oz.ToplamKalanTutar))
        };

        var table = new Table(UnitValue.CreatePercentArray(kpiler.Select(_ => 1f).ToArray())).UseAllAvailableWidth();
        foreach (var (label, value) in kpiler)
        {
            var cell = new Cell()
                .Add(new Paragraph(Metin(label, turkceDestekli)).SetFont(regularFont).SetFontSize(8).SetFontColor(new DeviceRgb(0x66, 0x66, 0x66)))
                .Add(new Paragraph(Metin(value, turkceDestekli)).SetFont(boldFont).SetFontSize(12))
                .SetBackgroundColor(RenkOzetKutu)
                .SetBorder(new SolidBorder(0.5f))
                .SetPadding(5);
            table.AddCell(cell);
        }

        document.Add(table);
        document.Add(new Paragraph(" ").SetFontSize(4));
    }

    private static void AddRenkLegend(Document document, PdfFont boldFont, PdfFont regularFont, bool turkceDestekli)
    {
        document.Add(new Paragraph(Metin("RENK AÇIKLAMALARI", turkceDestekli)).SetFont(boldFont).SetFontSize(11));

        var girdiler = new (Color Renk, string Aciklama)[]
        {
            (RenkReserved, "Onaylı / Rezerve"),
            (RenkOccupied, "Check-in Tamamlandı"),
            (RenkCheckedOut, "Check-out Tamamlandı"),
            (RenkOdemeEksik, "Ödeme Eksik"),
            (RenkCakisma, "Çakışma")
        };

        var table = new Table(UnitValue.CreatePercentArray(girdiler.Select(_ => 1f).ToArray())).UseAllAvailableWidth();
        foreach (var (renk, aciklama) in girdiler)
        {
            var innerTable = new Table(UnitValue.CreatePointArray([14f, 100f]));
            innerTable.AddCell(new Cell().SetBackgroundColor(renk).SetBorder(new SolidBorder(0.5f)).SetHeight(10));
            innerTable.AddCell(new Cell()
                .Add(new Paragraph(Metin(aciklama, turkceDestekli)).SetFont(regularFont).SetFontSize(8))
                .SetBorder(Border.NO_BORDER));

            table.AddCell(new Cell().Add(innerTable).SetBorder(Border.NO_BORDER));
        }

        document.Add(table);
        document.Add(new Paragraph(" ").SetFontSize(4));
    }

    private static void AddOdaPlaniTablosu(
        Document document,
        AylikOdaDolulukRaporDto rapor,
        string ayAdi,
        List<OdaDolulukOdaDto> odaGrubu,
        PdfFont boldFont,
        PdfFont regularFont,
        bool turkceDestekli)
    {
        var kolonGenislikleri = new List<float> { 1.6f, 1.3f };
        kolonGenislikleri.AddRange(odaGrubu.Select(_ => 1f));
        var table = new Table(UnitValue.CreatePercentArray(kolonGenislikleri.ToArray())).UseAllAvailableWidth();

        table.AddHeaderCell(HeaderHucre("TARİH", boldFont, turkceDestekli));
        table.AddHeaderCell(HeaderHucre("GÜN", boldFont, turkceDestekli));
        foreach (var oda in odaGrubu)
        {
            table.AddHeaderCell(HeaderHucre($"{oda.OdaNo} NO'LU ODA", boldFont, turkceDestekli));
        }

        foreach (var gun in rapor.Gunler)
        {
            table.AddCell(new Cell()
                .Add(new Paragraph(Metin($"{gun.Tarih.Day} {ayAdi} {gun.Tarih.Year}", turkceDestekli)).SetFont(regularFont).SetFontSize(7))
                .SetBorder(new SolidBorder(0.5f))
                .SetPadding(2));
            table.AddCell(new Cell()
                .Add(new Paragraph(Metin(gun.GunAdi, turkceDestekli)).SetFont(regularFont).SetFontSize(7))
                .SetBorder(new SolidBorder(0.5f))
                .SetPadding(2));

            foreach (var oda in odaGrubu)
            {
                var odaIndex = rapor.Odalar.FindIndex(x => x.OdaId == oda.OdaId);
                var hucre = gun.Hucreler[odaIndex];

                var cell = new Cell()
                    .SetBorder(new SolidBorder(0.5f))
                    .SetPadding(2)
                    .SetTextAlignment(TextAlignment.CENTER);

                if (hucre.DoluMu)
                {
                    cell.Add(new Paragraph(Metin(KisaHucreMetni(hucre), turkceDestekli)).SetFont(regularFont).SetFontSize(7));
                    cell.SetBackgroundColor(HucreRengi(hucre));
                }
                else
                {
                    cell.SetBackgroundColor(RenkBos);
                }

                table.AddCell(cell);
            }
        }

        document.Add(table);
    }

    private static void AddTahsilatlarBolumu(
        Document document,
        AylikOdaDolulukRaporDto rapor,
        PdfFont boldFont,
        PdfFont regularFont,
        bool turkceDestekli)
    {
        document.Add(new Paragraph(Metin("TAHSİLATLAR", turkceDestekli)).SetFont(boldFont).SetFontSize(14));
        document.Add(new Paragraph(" ").SetFontSize(4));

        if (rapor.Tahsilatlar.Count == 0)
        {
            document.Add(new Paragraph(Metin("Bu dönem için tahsilat kaydı bulunamadı.", turkceDestekli))
                .SetFont(regularFont)
                .SetFontSize(10));
            return;
        }

        var basliklar = new[] { "ODA NUMARASI", "MAKBUZ NO", "ÖDEME YAPAN", "ÜNİTESİ", "TAHSİL EDİLEN" };
        var table = new Table(UnitValue.CreatePercentArray([1f, 1f, 1.6f, 1.4f, 1.2f])).UseAllAvailableWidth();

        foreach (var baslik in basliklar)
        {
            table.AddHeaderCell(HeaderHucre(baslik, boldFont, turkceDestekli));
        }

        foreach (var tahsilat in rapor.Tahsilatlar)
        {
            table.AddCell(BodyHucre(tahsilat.OdaNo ?? "-", regularFont, TextAlignment.CENTER, turkceDestekli));
            table.AddCell(BodyHucre(tahsilat.MakbuzNo ?? "-", regularFont, TextAlignment.CENTER, turkceDestekli));
            table.AddCell(BodyHucre(tahsilat.OdemeYapan ?? tahsilat.MisafirAdiSoyadi ?? "-", regularFont, TextAlignment.LEFT, turkceDestekli));
            table.AddCell(BodyHucre(tahsilat.KurumUnite ?? "-", regularFont, TextAlignment.LEFT, turkceDestekli));
            table.AddCell(BodyHucre(FormatPara(tahsilat.OdemeTutari, tahsilat.ParaBirimi), regularFont, TextAlignment.RIGHT, turkceDestekli));
        }

        document.Add(table);
    }

    private static Cell HeaderHucre(string metin, PdfFont boldFont, bool turkceDestekli)
    {
        return new Cell()
            .Add(new Paragraph(Metin(metin, turkceDestekli)).SetFont(boldFont).SetFontSize(8).SetFontColor(ColorConstants.WHITE))
            .SetBackgroundColor(RenkHeader)
            .SetBorder(new SolidBorder(0.5f))
            .SetTextAlignment(TextAlignment.CENTER)
            .SetPadding(3);
    }

    private static Cell BodyHucre(string metin, PdfFont font, TextAlignment alignment, bool turkceDestekli)
    {
        return new Cell()
            .Add(new Paragraph(Metin(metin, turkceDestekli)).SetFont(font).SetFontSize(9))
            .SetBorder(new SolidBorder(0.5f))
            .SetTextAlignment(alignment)
            .SetPadding(3);
    }

    private static string KisaHucreMetni(OdaDolulukHucreDto hucre)
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

        return kaynak.Length > KisaMetinMaxUzunluk ? kaynak[..KisaMetinMaxUzunluk] + "…" : kaynak;
    }

    private static Color HucreRengi(OdaDolulukHucreDto hucre)
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

    private static (PdfFont Regular, PdfFont Bold, bool TurkceDestekli) FontlariYukle()
    {
        var regularPath = RegularFontYollari.FirstOrDefault(File.Exists);
        var boldPath = BoldFontYollari.FirstOrDefault(File.Exists);

        if (regularPath is not null && boldPath is not null)
        {
            var regular = PdfFontFactory.CreateFont(regularPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);
            var bold = PdfFontFactory.CreateFont(boldPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);
            return (regular, bold, true);
        }

        // TODO: Uretim ortaminda (orn. Linux container) Turkce karakterleri tam destekleyen bir TTF bulunamazsa
        // Ş/Ğ/İ/ı/Ö/Ü/Ç karakterleri ASCII karsiliklarina donusturulerek gosterilir. Kalici cozum icin
        // projeye acik lisansli bir Unicode font (orn. DejaVu Sans) gomulu asset olarak eklenmelidir.
        var fallbackRegular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA, "Cp1254");
        var fallbackBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD, "Cp1254");
        return (fallbackRegular, fallbackBold, false);
    }

    private static string Metin(string deger, bool turkceDestekli)
    {
        return turkceDestekli ? deger : NormalizeForPdf(deger);
    }

    private static string NormalizeForPdf(string value)
    {
        return value
            .Replace('Ç', 'C').Replace('ç', 'c')
            .Replace('Ğ', 'G').Replace('ğ', 'g')
            .Replace('İ', 'I').Replace('ı', 'i')
            .Replace('Ö', 'O').Replace('ö', 'o')
            .Replace('Ş', 'S').Replace('ş', 's')
            .Replace('Ü', 'U').Replace('ü', 'u')
            .Replace("₺", "TL", StringComparison.Ordinal);
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
