using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.StokHareketleri.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Kdv.Services;

public class KdvOzetRaporService : IKdvOzetRaporService
{
    private readonly StysAppDbContext _db;
    private readonly IUserAccessScopeService _userAccessScopeService;

    /// <summary>Özet hesaplamada memory'ye alınacak maksimum satır sayısı.</summary>
    private const int MaxOzetRows = 100000;

    public KdvOzetRaporService(
        StysAppDbContext db,
        IUserAccessScopeService userAccessScopeService)
    {
        _db = db;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<KdvOzetRaporDto> GetOzetRaporAsync(
        KdvOzetRaporFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        // 1. Tarih aralığını çözümle
        var (baslangicTarihi, bitisTarihi) = CozumleTarihAraligi(filter);

        // 2. Filtreli stok sorgusunu oluştur (access scope + tüm filtreler)
        var stokQuery = await BuildFilteredStokQueryAsync(baslangicTarihi, bitisTarihi, filter, cancellationToken);

        // 3. Muhasebe fiş left join
        var query = from stok in stokQuery
                    join musFis in _db.MuhasebeFisler
                        .Where(f => f.KaynakModul == "StokHareket" && f.IsDeleted == false && f.Durum != MuhasebeFisDurumlari.Iptal)
                        on stok.Id equals musFis.KaynakId into fisGroup
                    from fis in fisGroup.DefaultIfEmpty()
                    select new { stok, fis };

        // MusFisDurumu filtresi
        if (!string.IsNullOrWhiteSpace(filter.MusFisDurumu))
        {
            if (filter.MusFisDurumu == "FisiOlan")
                query = query.Where(x => x.fis != null);
            else if (filter.MusFisDurumu == "FisiOlmayan")
                query = query.Where(x => x.fis == null);
        }

        // 4. Performance guard: count kontrolü
        var rowCount = await query.CountAsync(cancellationToken);
        if (rowCount > MaxOzetRows)
        {
            throw new BaseException(
                $"KDV özet raporu en fazla {MaxOzetRows:N0} hareket için çalıştırılabilir. Lütfen filtreleri daraltınız.",
                errorCode: 400);
        }

        // 5. Tüm veriyi memory'ye al (özet hesaplamalar için)
        var allData = await query
            .Select(x => new
            {
                x.stok.HareketTipi,
                x.stok.Tutar,
                x.stok.KdvTutari,
                x.stok.KdvUygulamaTipi,
                x.stok.KdvIstisnaKodu,
                x.stok.KdvIstisnaAciklamasi,
                FisVar = x.fis != null
            })
            .ToListAsync(cancellationToken);

        // 6. Satış / Alış yönünü belirle
        var satisHareketler = allData
            .Where(x => StokHareketTipleri.CikisEtkisi.Contains(x.HareketTipi))
            .ToList();

        var alisHareketler = allData
            .Where(x => StokHareketTipleri.GirisEtkisi.Contains(x.HareketTipi))
            .ToList();

        // 7. Tam/Kısmi istisna ayrımı (satış yönünde)
        var tamIstisnaMatrahi = satisHareketler
            .Where(x => x.KdvUygulamaTipi == (int)KdvUygulamaTipi.TamIstisna)
            .Sum(x => x.Tutar);

        var kismiIstisnaMatrahi = satisHareketler
            .Where(x => x.KdvUygulamaTipi == (int)KdvUygulamaTipi.KismiIstisna)
            .Sum(x => x.Tutar);

        // 8. Özet hesapla (satış = hesaplanan KDV, alış = indirilecek KDV)
        var ozet = new KdvOzetRaporOzetDto
        {
            DonemLabel = $"{baslangicTarihi:dd.MM.yyyy} — {bitisTarihi:dd.MM.yyyy}",

            // Satış (Hesaplanan KDV)
            SatisHareketSayisi = satisHareketler.Count,
            SatisMatrahi = satisHareketler.Sum(x => x.Tutar),
            HesaplananKdvTutari = satisHareketler.Sum(x => x.KdvTutari),

            // Alış (İndirilecek KDV)
            AlisHareketSayisi = alisHareketler.Count,
            AlisMatrahi = alisHareketler.Sum(x => x.Tutar),
            IndirilecekKdvTutari = alisHareketler.Sum(x => x.KdvTutari),

            // Net KDV
            NetKdv = satisHareketler.Sum(x => x.KdvTutari) - alisHareketler.Sum(x => x.KdvTutari),

            // İstisna / Kapsam Dışı (satış yönündeki istisna ve kapsam dışı matrahlar)
            TamIstisnaMatrahi = tamIstisnaMatrahi,
            KismiIstisnaMatrahi = kismiIstisnaMatrahi,
            IstisnaMatrahi = tamIstisnaMatrahi + kismiIstisnaMatrahi,
            KapsamDisiMatrah = satisHareketler
                .Where(x => x.KdvUygulamaTipi == (int)KdvUygulamaTipi.KdvKapsamDisi)
                .Sum(x => x.Tutar),

            // Genel
            ToplamKayitSayisi = allData.Count,
            FisiOlanSayisi = allData.Count(x => x.FisVar),
            FisiOlmayanSayisi = allData.Count(x => !x.FisVar)
        };

        // 9. Uygulama tipi özetleri
        var uygulamaTipiOzetleri = allData
            .GroupBy(x => x.KdvUygulamaTipi)
            .Select(g => new KdvUygulamaTipiOzetDto
            {
                KdvUygulamaTipi = g.Key,
                KdvUygulamaTipiAd = KdvUygulamaTipiAdi(g.Key),
                HareketSayisi = g.Count(),
                Matrah = g.Sum(x => x.Tutar),
                KdvTutari = g.Sum(x => x.KdvTutari)
            })
            .OrderBy(x => x.KdvUygulamaTipi)
            .ToList();

        // 10. İstisna kodu özetleri (istisna kodu boş olmayanlar)
        var istisnaKoduOzetleri = allData
            .Where(x => !string.IsNullOrWhiteSpace(x.KdvIstisnaKodu))
            .GroupBy(x => new { x.KdvIstisnaKodu, x.KdvIstisnaAciklamasi })
            .Select(g => new KdvIstisnaKoduOzetDto
            {
                KdvIstisnaKodu = g.Key.KdvIstisnaKodu,
                KdvIstisnaAciklamasi = g.Key.KdvIstisnaAciklamasi,
                HareketSayisi = g.Count(),
                Matrah = g.Sum(x => x.Tutar)
            })
            .OrderBy(x => x.KdvIstisnaKodu)
            .ToList();

        // 11. Uyarıları belirle (severity ve route ile)
        const string hareketRaporuRoute = "muhasebe/kdv-hareket-raporu";

        var uyarilar = new List<KdvOzetRaporUyariDto>();

        // MUHASEBE_FISI_EKSIK
        var fisiOlmayanlar = allData.Count(x => !x.FisVar);
        if (fisiOlmayanlar > 0)
        {
            uyarilar.Add(new KdvOzetRaporUyariDto
            {
                UyariKodu = "MUHASEBE_FISI_EKSIK",
                UyariMesaji = $"Muhasebe fişi oluşmamış {fisiOlmayanlar} stok hareketi bulundu. " +
                              "Beyanname öncesi eksik fişlerin tamamlanması önerilir.",
                EtkilenenKayitSayisi = fisiOlmayanlar,
                Severity = "warn",
                Route = hareketRaporuRoute
            });
        }

        // KDV_TUTARI_EKSIK
        var kdvliFakatKdvSifir = allData.Count(x =>
            x.KdvUygulamaTipi == (int)KdvUygulamaTipi.Kdvli && x.KdvTutari == 0);
        if (kdvliFakatKdvSifir > 0)
        {
            uyarilar.Add(new KdvOzetRaporUyariDto
            {
                UyariKodu = "KDV_TUTARI_EKSIK",
                UyariMesaji = $"KDV'li olarak işaretlenmiş ancak KDV tutarı sıfır olan {kdvliFakatKdvSifir} hareket bulundu. " +
                              "Taşınır kart KDV oranlarını kontrol edin.",
                EtkilenenKayitSayisi = kdvliFakatKdvSifir,
                Severity = "error",
                Route = hareketRaporuRoute
            });
        }

        // ISTISNA_KODU_EKSIK
        var istisnaliKodsuz = allData.Count(x =>
            (x.KdvUygulamaTipi == (int)KdvUygulamaTipi.TamIstisna
             || x.KdvUygulamaTipi == (int)KdvUygulamaTipi.KismiIstisna)
            && string.IsNullOrWhiteSpace(x.KdvIstisnaKodu));
        if (istisnaliKodsuz > 0)
        {
            uyarilar.Add(new KdvOzetRaporUyariDto
            {
                UyariKodu = "ISTISNA_KODU_EKSIK",
                UyariMesaji = $"İstisnalı olarak işaretlenmiş ancak istisna kodu atanmamış {istisnaliKodsuz} hareket bulundu. " +
                              "Beyannamede istisna kodu zorunludur.",
                EtkilenenKayitSayisi = istisnaliKodsuz,
                Severity = "warn",
                Route = hareketRaporuRoute
            });
        }

        // TEVKIFATLI_HAREKET_VAR
        var tevkifatliSayisi = allData.Count(x => x.KdvUygulamaTipi == (int)KdvUygulamaTipi.Tevkifatli);
        if (tevkifatliSayisi > 0)
        {
            uyarilar.Add(new KdvOzetRaporUyariDto
            {
                UyariKodu = "TEVKIFATLI_HAREKET_VAR",
                UyariMesaji = $"Tevkifatlı {tevkifatliSayisi} hareket bulundu. " +
                              "Tevkifatlı KDV'nin beyannamede ayrıca bildirilmesi gerekir.",
                EtkilenenKayitSayisi = tevkifatliSayisi,
                Severity = "warn",
                Route = hareketRaporuRoute
            });
        }

        return new KdvOzetRaporDto
        {
            BaslangicTarihi = baslangicTarihi,
            BitisTarihi = bitisTarihi,
            Ozet = ozet,
            UygulamaTipiOzetleri = uygulamaTipiOzetleri,
            IstisnaKoduOzetleri = istisnaKoduOzetleri,
            Uyarilar = uyarilar
        };
    }

    /// <summary>
    /// GetOzetRaporAsync sonucunu kullanarak Excel çıktısı üretir.
    /// Tüm validasyon (MaliYil/Donem, 100K limit, access scope) GetOzetRaporAsync içinde çalışır.
    /// </summary>
    public async Task<byte[]> ExportExcelAsync(
        KdvOzetRaporFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        // Aynı GetOzetRaporAsync sonucunu kullan; validasyon + hesaplama tek yerden
        var rapor = await GetOzetRaporAsync(filter, cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("KDV Özet Raporu");

        var now = DateTime.Now;
        int row = 1;

        // ── Başlık ──
        ws.Cell(row, 1).Value = "KDV Özet Raporu / Beyanname Hazırlık";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;

        // ── Üst bilgiler ──
        row = 3;
        ws.Cell(row, 1).Value = "Oluşturma Tarihi:";
        ws.Cell(row, 2).Value = now.ToString("dd.MM.yyyy HH:mm");

        row = 4;
        ws.Cell(row, 1).Value = "Dönem:";
        ws.Cell(row, 2).Value = rapor.Ozet.DonemLabel;

        row = 5;
        ws.Cell(row, 1).Value = "Başlangıç Tarihi:";
        ws.Cell(row, 2).Value = rapor.BaslangicTarihi.ToString("dd.MM.yyyy");

        row = 6;
        ws.Cell(row, 1).Value = "Bitiş Tarihi:";
        ws.Cell(row, 2).Value = rapor.BitisTarihi.ToString("dd.MM.yyyy");

        row = 7;
        if (filter.TesisId.HasValue)
        {
            ws.Cell(row, 1).Value = "Tesis ID:";
            ws.Cell(row, 2).Value = filter.TesisId.Value;
            row++;
        }
        if (filter.DepoId.HasValue)
        {
            ws.Cell(row, 1).Value = "Depo ID:";
            ws.Cell(row, 2).Value = filter.DepoId.Value;
            row++;
        }

        row += 1;

        // ── Ana özet alanları ──
        int ozetBas = row;
        ws.Cell(row, 1).Value = "ÖZET";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 12;
        row++;

        var oz = rapor.Ozet;
        var ozetSatirlar = new (string Label, object Value)[]
        {
            ("Satış Hareket Sayısı", oz.SatisHareketSayisi),
            ("KDV'li Satış Matrahı", oz.SatisMatrahi),
            ("Hesaplanan KDV", oz.HesaplananKdvTutari),
            ("Alış Hareket Sayısı", oz.AlisHareketSayisi),
            ("KDV'li Alış Matrahı", oz.AlisMatrahi),
            ("İndirilecek KDV", oz.IndirilecekKdvTutari),
            ("Net KDV", oz.NetKdv),
            ("İstisna Matrahı", oz.IstisnaMatrahi),
            ("Tam İstisna Matrahı", oz.TamIstisnaMatrahi),
            ("Kısmi İstisna Matrahı", oz.KismiIstisnaMatrahi),
            ("KDV Kapsam Dışı Matrah", oz.KapsamDisiMatrah),
            ("Toplam Kayıt", oz.ToplamKayitSayisi),
            ("Fişi Olan", oz.FisiOlanSayisi),
            ("Fişi Olmayan", oz.FisiOlmayanSayisi)
        };

        foreach (var (label, value) in ozetSatirlar)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = value is decimal d ? (double)d : Convert.ToDouble(value);
            if (value is decimal)
                ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        row += 1;

        // ── Tablo 1: KDV Uygulama Tipi Özeti ──
        ws.Cell(row, 1).Value = "KDV Uygulama Tipi Özeti";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 12;
        row++;

        var utHeaders = new[] { "KDV Uygulama Tipi", "Hareket Sayısı", "Matrah", "KDV Tutarı" };
        for (int i = 0; i < utHeaders.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = utHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        }
        row++;

        foreach (var u in rapor.UygulamaTipiOzetleri)
        {
            ws.Cell(row, 1).Value = u.KdvUygulamaTipiAd;
            ws.Cell(row, 2).Value = u.HareketSayisi;
            ws.Cell(row, 3).Value = u.Matrah;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 4).Value = u.KdvTutari;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        row += 1;

        // ── Tablo 2: KDV İstisna Kodu Özeti ──
        ws.Cell(row, 1).Value = "KDV İstisna Kodu Özeti";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 12;
        row++;

        var ikHeaders = new[] { "İstisna Kodu", "İstisna Açıklaması", "Hareket Sayısı", "Matrah" };
        for (int i = 0; i < ikHeaders.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = ikHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        }
        row++;

        if (rapor.IstisnaKoduOzetleri.Count == 0)
        {
            ws.Cell(row, 1).Value = "İstisna kodu verisi bulunmamaktadır.";
            row++;
        }
        else
        {
            foreach (var ik in rapor.IstisnaKoduOzetleri)
            {
                ws.Cell(row, 1).Value = ik.KdvIstisnaKodu ?? "";
                ws.Cell(row, 2).Value = ik.KdvIstisnaAciklamasi ?? "";
                ws.Cell(row, 3).Value = ik.HareketSayisi;
                ws.Cell(row, 4).Value = ik.Matrah;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }
        }

        row += 1;

        // ── Tablo 3: Uyarılar ──
        ws.Cell(row, 1).Value = "Uyarılar";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 12;
        row++;

        var uyHeaders = new[] { "Uyarı Kodu", "Severity", "Mesaj", "Etkilenen Kayıt Sayısı", "Route" };
        for (int i = 0; i < uyHeaders.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = uyHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        }
        row++;

        if (rapor.Uyarilar.Count == 0)
        {
            ws.Cell(row, 1).Value = "Uyarı bulunmamaktadır.";
            row++;
        }
        else
        {
            foreach (var uy in rapor.Uyarilar)
            {
                ws.Cell(row, 1).Value = uy.UyariKodu;
                ws.Cell(row, 2).Value = uy.Severity;
                ws.Cell(row, 3).Value = uy.UyariMesaji;
                ws.Cell(row, 4).Value = uy.EtkilenenKayitSayisi;
                ws.Cell(row, 5).Value = uy.Route ?? "";
                row++;
            }
        }

        // ── Biçimlendirme ──
        ws.Columns().AdjustToContents();
        // Minimum genişlik garantisi
        ws.Column(1).Width = ws.Column(1).Width < 30 ? 30 : ws.Column(1).Width;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// MaliYil + Donem verilmişse ayın ilk ve son gününü,
    /// aksi halde BaslangicTarihi/BitisTarihi'ni,
    /// hiçbiri verilmemişse içinde bulunulan ayı döner.
    /// </summary>
    private static (DateTime baslangic, DateTime bitis) CozumleTarihAraligi(KdvOzetRaporFilterDto filter)
    {
        if (filter.MaliYil.HasValue && filter.Donem.HasValue)
        {
            var maliYil = filter.MaliYil.Value;
            var donem = filter.Donem.Value;

            // MaliYil validasyonu: 2000-2100
            if (maliYil < 2000 || maliYil > 2100)
                throw new BaseException("Mali yıl 2000 ile 2100 arasında olmalıdır.", errorCode: 400);

            // Donem validasyonu: 1-12
            if (donem < 1 || donem > 12)
                throw new BaseException("Dönem 1 ile 12 arasında olmalıdır.", errorCode: 400);

            var baslangic = new DateTime(maliYil, donem, 1, 0, 0, 0, DateTimeKind.Utc);
            var bitis = baslangic.AddMonths(1).AddSeconds(-1);
            return (baslangic, bitis);
        }

        if (filter.BaslangicTarihi.HasValue && filter.BitisTarihi.HasValue)
        {
            return (filter.BaslangicTarihi.Value, filter.BitisTarihi.Value);
        }

        // Varsayılan: içinde bulunulan ay
        var now = DateTime.UtcNow;
        var defaultBaslangic = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var defaultBitis = defaultBaslangic.AddMonths(1).AddSeconds(-1);
        return (defaultBaslangic, defaultBitis);
    }

    /// <summary>
    /// KdvHareketRaporService.BuildFilteredStokQueryAsync ile aynı filtre mantığını uygular:
    /// access scope, tesis, depo, taşınır kart, hareket tipi, KDV uygulama tipi, istisna tanım/kod.
    /// </summary>
    private async Task<IQueryable<StokHareket>> BuildFilteredStokQueryAsync(
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        KdvOzetRaporFilterDto filter,
        CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);

        var stokQuery = _db.StokHareketleri
            .Include(s => s.Depo)
            .Include(s => s.TasinirKart)
            .Where(s => s.IsDeleted == false)
            .Where(s => s.HareketTarihi >= baslangicTarihi
                        && s.HareketTarihi <= bitisTarihi);

        if (scope.IsScoped)
            stokQuery = stokQuery.Where(s =>
                s.Depo != null &&
                s.Depo.TesisId.HasValue &&
                scope.TesisIds.Contains(s.Depo.TesisId.Value));

        if (filter.TesisId.HasValue)
            stokQuery = stokQuery.Where(s => s.Depo != null && s.Depo.TesisId == filter.TesisId.Value);

        if (filter.DepoId.HasValue)
            stokQuery = stokQuery.Where(s => s.DepoId == filter.DepoId.Value);

        if (filter.TasinirKartId.HasValue)
            stokQuery = stokQuery.Where(s => s.TasinirKartId == filter.TasinirKartId.Value);

        if (!string.IsNullOrWhiteSpace(filter.HareketTipi))
            stokQuery = stokQuery.Where(s => s.HareketTipi == filter.HareketTipi.Trim());

        if (filter.KdvUygulamaTipi.HasValue)
            stokQuery = stokQuery.Where(s => s.KdvUygulamaTipi == filter.KdvUygulamaTipi.Value);

        if (filter.KdvIstisnaTanimId.HasValue)
            stokQuery = stokQuery.Where(s => s.KdvIstisnaTanimId == filter.KdvIstisnaTanimId.Value);

        if (!string.IsNullOrWhiteSpace(filter.KdvIstisnaKodu))
        {
            var kod = filter.KdvIstisnaKodu.Trim();
            stokQuery = stokQuery.Where(s => s.KdvIstisnaKodu != null && s.KdvIstisnaKodu.Contains(kod));
        }

        return stokQuery;
    }

    private static string KdvUygulamaTipiAdi(int uygulamaTipi)
    {
        return uygulamaTipi switch
        {
            1 => "KDV'li",
            2 => "Tam İstisna",
            3 => "Kısmi İstisna",
            4 => "KDV Kapsam Dışı",
            5 => "Tevkifatlı",
            _ => "Bilinmiyor"
        };
    }
}
