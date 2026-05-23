using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.StokHareketleri.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Kdv.Services;

public class KdvBeyannameHazirlikKontrolService : IKdvBeyannameHazirlikKontrolService
{
    private readonly StysAppDbContext _db;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IKdvOzetRaporService _kdvOzetRaporService;

    private const string HareketRaporuRoute = "muhasebe/kdv-hareket-raporu";

    // ── Internal data holder (replaces anonymous type / dynamic) ──

    private sealed record FisSatirVerisi(
        int MuhasebeHesapPlaniId,
        string? MuhasebeHesapPlaniKod,
        decimal Borc,
        decimal Alacak);

    private sealed record StokFisVerisi(
        int Id,
        string HareketTipi,
        decimal Tutar,
        decimal KdvTutari,
        int KdvUygulamaTipi,
        string? KdvIstisnaKodu,
        int? FisId,
        string? FisDurum,
        decimal FisToplamBorc,
        decimal FisToplamAlacak,
        List<FisSatirVerisi>? Satirlar);

    public KdvBeyannameHazirlikKontrolService(
        StysAppDbContext db,
        IUserAccessScopeService userAccessScopeService,
        IKdvOzetRaporService kdvOzetRaporService)
    {
        _db = db;
        _userAccessScopeService = userAccessScopeService;
        _kdvOzetRaporService = kdvOzetRaporService;
    }

    public async Task<KdvBeyannameHazirlikKontrolDto> KontrolEtAsync(
        KdvBeyannameHazirlikKontrolFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        // 1. Tarih aralığını çözümle
        var (baslangicTarihi, bitisTarihi) = CozumleTarihAraligi(filter);

        // 2. Filtreli stok sorgusunu oluştur
        var stokQuery = await BuildFilteredStokQueryAsync(baslangicTarihi, bitisTarihi, filter, cancellationToken);

        // 3. Stok hareketlerini muhasebe fişleriyle left join yap
        var stokFisQuery = from stok in stokQuery
                           join musFis in _db.MuhasebeFisler
                               .Where(f => f.KaynakModul == "StokHareket"
                                          && f.IsDeleted == false
                                          && f.Durum != MuhasebeFisDurumlari.Iptal)
                               .Include(f => f.Satirlar)
                               on stok.Id equals musFis.KaynakId into fisGroup
                           from fis in fisGroup.DefaultIfEmpty()
                           select new { stok, fis };

        // 4. Tüm veriyi memory'ye al
        var rawData = await stokFisQuery
            .Select(x => new
            {
                x.stok.Id,
                x.stok.HareketTipi,
                x.stok.Tutar,
                x.stok.KdvTutari,
                x.stok.KdvUygulamaTipi,
                x.stok.KdvIstisnaKodu,
                FisId = x.fis != null ? (int?)x.fis.Id : null,
                FisDurum = x.fis != null ? x.fis.Durum : null,
                FisToplamBorc = x.fis != null ? x.fis.ToplamBorc : 0m,
                FisToplamAlacak = x.fis != null ? x.fis.ToplamAlacak : 0m,
                Satirlar = x.fis != null
                    ? x.fis.Satirlar
                        .Where(s => s.IsDeleted == false)
                        .Select(s => new FisSatirVerisi(
                            s.MuhasebeHesapPlaniId,
                            s.MuhasebeHesapPlani != null ? s.MuhasebeHesapPlani.Kod : null,
                            s.Borc,
                            s.Alacak))
                        .ToList()
                    : null
            })
            .ToListAsync(cancellationToken);

        // Project to named record type
        var allData = rawData
            .Select(x => new StokFisVerisi(
                x.Id,
                x.HareketTipi,
                x.Tutar,
                x.KdvTutari,
                x.KdvUygulamaTipi,
                x.KdvIstisnaKodu,
                x.FisId,
                x.FisDurum,
                x.FisToplamBorc,
                x.FisToplamAlacak,
                x.Satirlar))
            .ToList();

        // 5. Yön ayrımı
        var satisHareketler = allData
            .Where(x => StokHareketTipleri.CikisEtkisi.Contains(x.HareketTipi))
            .ToList();

        var alisHareketler = allData
            .Where(x => StokHareketTipleri.GirisEtkisi.Contains(x.HareketTipi))
            .ToList();

        // 6. KDV tutarlarını hesapla
        var hesaplananKdv = satisHareketler.Sum(x => x.KdvTutari);
        var indirilecekKdv = alisHareketler.Sum(x => x.KdvTutari);
        var netKdv = hesaplananKdv - indirilecekKdv;

        // 7. Tüm kontrolleri çalıştır
        var kontroller = new List<KdvBeyannameHazirlikKontrolMaddesiDto>();

        // 1) KDV_HAREKET_VAR_MI
        kontroller.Add(KontrolKdvHareketVarMi(allData));

        // 2) MUHASEBE_FISI_EKSIK
        kontroller.Add(KontrolMuhasebeFisiEksik(allData));

        // 3) KDV_TUTARI_EKSIK
        kontroller.Add(KontrolKdvTutariEksik(allData));

        // 4) ISTISNA_KODU_EKSIK
        kontroller.Add(KontrolIstisnaKoduEksik(allData));

        // 5) TEVKIFATLI_HAREKET_VAR
        kontroller.Add(KontrolTevkifatliHareketVar(allData));

        // 6) KDV_HESAP_UYUMU
        kontroller.Add(KontrolKdvHesapUyumu(allData));

        // 7) FIS_DENGE_KONTROLU
        kontroller.Add(KontrolFisDengesi(allData));

        // 8) TASLAK_FIS_VAR
        kontroller.Add(KontrolTaslakFisVar(allData));

        // 9) KDV_OZET_TUTARLILIK
        kontroller.Add(await KontrolKdvOzetTutarlilikAsync(filter, hesaplananKdv, indirilecekKdv, netKdv, cancellationToken));

        // 10) ISTISNA_AYRIMI_KONTROLU
        kontroller.Add(KontrolIstisnaAyrimi(allData));

        // 8. Özet sayıları hesapla
        var basarili = kontroller.Count(k => k.Durum == "Basarili");
        var uyarili = kontroller.Count(k => k.Durum == "Uyari");
        var bloklayici = kontroller.Count(k => k.Durum == "Bloklayici");
        var beyanaHazirMi = bloklayici == 0;

        return new KdvBeyannameHazirlikKontrolDto
        {
            TesisId = filter.TesisId,
            DepoId = filter.DepoId,
            MaliYil = filter.MaliYil,
            Donem = filter.Donem,
            BaslangicTarihi = baslangicTarihi,
            BitisTarihi = bitisTarihi,
            BeyanaHazirMi = beyanaHazirMi,
            ToplamKontrolSayisi = kontroller.Count,
            BasariliKontrolSayisi = basarili,
            UyariliKontrolSayisi = uyarili,
            BloklayiciKontrolSayisi = bloklayici,
            HesaplananKdvTutari = hesaplananKdv,
            IndirilecekKdvTutari = indirilecekKdv,
            NetKdv = netKdv,
            Kontroller = kontroller
        };
    }

    // ── Kontrol metotları ──

    /// <summary>1. Dönem içinde KDV hareketi var mı?</summary>
    private static KdvBeyannameHazirlikKontrolMaddesiDto KontrolKdvHareketVarMi(List<StokFisVerisi> allData)
    {
        if (allData.Count == 0)
            return Uyari(
                "KDV_HAREKET_VAR_MI",
                "KDV Hareketi Var Mı?",
                "Seçilen dönemde KDV hareketi bulunamadı.");

        return Basarili(
            "KDV_HAREKET_VAR_MI",
            "KDV Hareketi Var Mı?",
            $"Seçilen dönemde {allData.Count:N0} stok hareketi bulundu.");
    }

    /// <summary>2. Muhasebe fişi oluşmamış KDV hareketi var mı?</summary>
    private static KdvBeyannameHazirlikKontrolMaddesiDto KontrolMuhasebeFisiEksik(List<StokFisVerisi> allData)
    {
        var fisiOlmayanlar = allData.Count(x => x.FisId == null);

        if (fisiOlmayanlar > 0)
            return Bloklayici(
                "MUHASEBE_FISI_EKSIK",
                "Eksik Muhasebe Fişi",
                $"KDV hareketlerinden {fisiOlmayanlar} tanesinin muhasebe fişi henüz oluşturulmamış.",
                etkilenenKayitSayisi: fisiOlmayanlar,
                route: HareketRaporuRoute);

        return Basarili(
            "MUHASEBE_FISI_EKSIK",
            "Eksik Muhasebe Fişi",
            "Tüm KDV hareketlerinin muhasebe fişi oluşturulmuş.");
    }

    /// <summary>3. KDV'li olup KdvTutari <= 0 olan hareket var mı?</summary>
    private static KdvBeyannameHazirlikKontrolMaddesiDto KontrolKdvTutariEksik(List<StokFisVerisi> allData)
    {
        var kdvliFakatKdvSifir = allData.Count(x =>
            x.KdvUygulamaTipi == (int)KdvUygulamaTipi.Kdvli && x.KdvTutari == 0);

        if (kdvliFakatKdvSifir > 0)
            return Bloklayici(
                "KDV_TUTARI_EKSIK",
                "KDV Tutarı Eksik",
                $"KDV'li olarak işaretlenen {kdvliFakatKdvSifir} harekette KDV tutarı sıfır veya eksik.",
                etkilenenKayitSayisi: kdvliFakatKdvSifir,
                route: HareketRaporuRoute);

        return Basarili(
            "KDV_TUTARI_EKSIK",
            "KDV Tutarı Eksik",
            "KDV'li tüm hareketlerde KDV tutarı mevcut.");
    }

    /// <summary>4. İstisnalı/kapsam dışı olup istisna kodu eksik olan hareket var mı?</summary>
    private static KdvBeyannameHazirlikKontrolMaddesiDto KontrolIstisnaKoduEksik(List<StokFisVerisi> allData)
    {
        var istisnaliTipler = new HashSet<int>
        {
            (int)KdvUygulamaTipi.TamIstisna,
            (int)KdvUygulamaTipi.KismiIstisna,
            (int)KdvUygulamaTipi.KdvKapsamDisi
        };

        var istisnaliKodsuz = allData.Count(x =>
            istisnaliTipler.Contains(x.KdvUygulamaTipi)
            && string.IsNullOrWhiteSpace(x.KdvIstisnaKodu));

        if (istisnaliKodsuz > 0)
            return Bloklayici(
                "ISTISNA_KODU_EKSIK",
                "İstisna Kodu Eksik",
                $"İstisna veya kapsam dışı hareketlerde istisna kodu eksik {istisnaliKodsuz} kayıt var.",
                etkilenenKayitSayisi: istisnaliKodsuz,
                route: HareketRaporuRoute);

        return Basarili(
            "ISTISNA_KODU_EKSIK",
            "İstisna Kodu Eksik",
            "İstisna/kapsam dışı tüm hareketlerde istisna kodu mevcut.");
    }

    /// <summary>5. Tevkifatlı hareket var mı?</summary>
    private static KdvBeyannameHazirlikKontrolMaddesiDto KontrolTevkifatliHareketVar(List<StokFisVerisi> allData)
    {
        var tevkifatliSayisi = allData.Count(x => x.KdvUygulamaTipi == (int)KdvUygulamaTipi.Tevkifatli);

        if (tevkifatliSayisi > 0)
            return Bloklayici(
                "TEVKIFATLI_HAREKET_VAR",
                "Tevkifatlı Hareket Var",
                $"Tevkifatlı KDV işlemleri bu sistemde henüz desteklenmemektedir. ({tevkifatliSayisi} kayıt)",
                etkilenenKayitSayisi: tevkifatliSayisi,
                route: HareketRaporuRoute);

        return Basarili(
            "TEVKIFATLI_HAREKET_VAR",
            "Tevkifatlı Hareket Var",
            "Tevkifatlı KDV hareketi bulunamadı.");
    }

    /// <summary>6. 191/391 hesap uyumu kontrolü</summary>
    private static KdvBeyannameHazirlikKontrolMaddesiDto KontrolKdvHesapUyumu(List<StokFisVerisi> allData)
    {
        var kdvliFisler = allData
            .Where(x => x.KdvUygulamaTipi == (int)KdvUygulamaTipi.Kdvli && x.FisId != null && x.Satirlar != null)
            .ToList();

        int eksikSayisi = 0;

        foreach (var item in kdvliFisler)
        {
            var satirlar = item.Satirlar!;
            var cikisYonunde = StokHareketTipleri.CikisEtkisi.Contains(item.HareketTipi);

            var gerekliHesap = cikisYonunde ? "391" : "191";

            var gerekliHesapVarMi = satirlar.Any(s =>
            {
                var kod = s.MuhasebeHesapPlaniKod;
                return kod != null && (kod == gerekliHesap || kod.StartsWith(gerekliHesap + "."));
            });

            if (!gerekliHesapVarMi)
                eksikSayisi++;
        }

        if (eksikSayisi > 0)
            return Bloklayici(
                "KDV_HESAP_UYUMU",
                "KDV Hesap Uyumu",
                $"KDV'li hareketlerden {eksikSayisi} tanesine ait muhasebe fişlerinde 191/391 KDV hesabı bulunamadı.",
                etkilenenKayitSayisi: eksikSayisi);

        return Basarili(
            "KDV_HESAP_UYUMU",
            "KDV Hesap Uyumu",
            "KDV'li tüm hareketlerin fişlerinde 191/391 hesabı mevcut.");
    }

    /// <summary>7. Fiş dengesi kontrolü</summary>
    private static KdvBeyannameHazirlikKontrolMaddesiDto KontrolFisDengesi(List<StokFisVerisi> allData)
    {
        // Benzersiz fiş ID'lerini al (fisi olan kayıtlar)
        var fisIdList = allData
            .Where(x => x.FisId != null)
            .Select(x => x.FisId!.Value)
            .Distinct()
            .ToList();

        int dengesizSayisi = 0;

        foreach (var fisId in fisIdList)
        {
            var fisKaydi = allData.FirstOrDefault(x => x.FisId == fisId);
            if (fisKaydi == null) continue;

            decimal toplamBorc = fisKaydi.FisToplamBorc;
            decimal toplamAlacak = fisKaydi.FisToplamAlacak;

            if (Math.Abs(toplamBorc - toplamAlacak) > 0.001m)
                dengesizSayisi++;
        }

        if (dengesizSayisi > 0)
            return Bloklayici(
                "FIS_DENGE_KONTROLU",
                "Fiş Dengesi Kontrolü",
                $"Stok hareket kaynaklı {dengesizSayisi} muhasebe fişi dengeli değil.",
                etkilenenKayitSayisi: dengesizSayisi);

        return Basarili(
            "FIS_DENGE_KONTROLU",
            "Fiş Dengesi Kontrolü",
            "Stok hareket kaynaklı tüm fişler dengeli.");
    }

    /// <summary>8. Taslak fiş kontrolü</summary>
    private static KdvBeyannameHazirlikKontrolMaddesiDto KontrolTaslakFisVar(List<StokFisVerisi> allData)
    {
        var taslakFisliKayitlar = allData.Count(x =>
            x.FisId != null && x.FisDurum == MuhasebeFisDurumlari.Taslak);

        if (taslakFisliKayitlar > 0)
            return Uyari(
                "TASLAK_FIS_VAR",
                "Taslak Fiş Var",
                $"Seçilen dönemde {taslakFisliKayitlar} hareket için taslak durumda muhasebe fişi var. Beyanname öncesi onaylanması önerilir.",
                etkilenenKayitSayisi: taslakFisliKayitlar);

        return Basarili(
            "TASLAK_FIS_VAR",
            "Taslak Fiş Var",
            "Taslak durumda muhasebe fişi bulunamadı.");
    }

    /// <summary>9. KDV Özet Raporu tutarlılığı</summary>
    private async Task<KdvBeyannameHazirlikKontrolMaddesiDto> KontrolKdvOzetTutarlilikAsync(
        KdvBeyannameHazirlikKontrolFilterDto filter,
        decimal hesaplananKdv,
        decimal indirilecekKdv,
        decimal netKdv,
        CancellationToken cancellationToken)
    {
        try
        {
            var ozetFilter = new KdvOzetRaporFilterDto
            {
                TesisId = filter.TesisId,
                DepoId = filter.DepoId,
                MaliYil = filter.MaliYil,
                Donem = filter.Donem,
                BaslangicTarihi = filter.BaslangicTarihi,
                BitisTarihi = filter.BitisTarihi
            };

            var ozet = await _kdvOzetRaporService.GetOzetRaporAsync(ozetFilter, cancellationToken);

            const decimal tolerance = 0.01m;

            bool hesaplananUyumlu = Math.Abs(hesaplananKdv - ozet.Ozet.HesaplananKdvTutari) < tolerance;
            bool indirilecekUyumlu = Math.Abs(indirilecekKdv - ozet.Ozet.IndirilecekKdvTutari) < tolerance;
            bool netUyumlu = Math.Abs(netKdv - ozet.Ozet.NetKdv) < tolerance;

            if (hesaplananUyumlu && indirilecekUyumlu && netUyumlu)
                return Basarili(
                    "KDV_OZET_TUTARLILIK",
                    "KDV Özet Raporu Tutarlılığı",
                    "Checklist KDV tutarları KDV Özet Raporu ile uyumlu.");

            var uyumsuzluklar = new List<string>();
            if (!hesaplananUyumlu)
                uyumsuzluklar.Add($"Hesaplanan KDV: checklist={hesaplananKdv:N2}, özet={ozet.Ozet.HesaplananKdvTutari:N2}");
            if (!indirilecekUyumlu)
                uyumsuzluklar.Add($"İndirilecek KDV: checklist={indirilecekKdv:N2}, özet={ozet.Ozet.IndirilecekKdvTutari:N2}");
            if (!netUyumlu)
                uyumsuzluklar.Add($"Net KDV: checklist={netKdv:N2}, özet={ozet.Ozet.NetKdv:N2}");

            return Bloklayici(
                "KDV_OZET_TUTARLILIK",
                "KDV Özet Raporu Tutarlılığı",
                $"Checklist KDV değerleri KDV Özet Raporu ile uyumsuz. {string.Join("; ", uyumsuzluklar)}");
        }
        catch
        {
            // Özet rapor çalıştırılamazsa bunu bloklayıcı değil uyarı yap
            return Uyari(
                "KDV_OZET_TUTARLILIK",
                "KDV Özet Raporu Tutarlılığı",
                "KDV Özet Raporu çalıştırılamadığı için tutarlılık kontrolü yapılamadı.");
        }
    }

    /// <summary>10. Tam/Kısmi istisna ayrımı kontrolü</summary>
    private static KdvBeyannameHazirlikKontrolMaddesiDto KontrolIstisnaAyrimi(List<StokFisVerisi> allData)
    {
        var tamIstisna = allData.Count(x => x.KdvUygulamaTipi == (int)KdvUygulamaTipi.TamIstisna);
        var kismiIstisna = allData.Count(x => x.KdvUygulamaTipi == (int)KdvUygulamaTipi.KismiIstisna);
        var kapsamDisi = allData.Count(x => x.KdvUygulamaTipi == (int)KdvUygulamaTipi.KdvKapsamDisi);

        if (tamIstisna + kismiIstisna + kapsamDisi == 0)
            return Basarili(
                "ISTISNA_AYRIMI_KONTROLU",
                "Tam/Kısmi İstisna Ayrımı",
                "İstisnalı hareket bulunamadı.");

        return Basarili(
            "ISTISNA_AYRIMI_KONTROLU",
            "Tam/Kısmi İstisna Ayrımı",
            $"Tam istisna: {tamIstisna}, Kısmi istisna: {kismiIstisna}, Kapsam dışı: {kapsamDisi}. Ayrım doğru yapılmış.");
    }

    // ── Helper metotlar ──

    private static KdvBeyannameHazirlikKontrolMaddesiDto Basarili(string kod, string baslik, string aciklama)
    {
        return new KdvBeyannameHazirlikKontrolMaddesiDto
        {
            Kod = kod,
            Baslik = baslik,
            Aciklama = aciklama,
            Durum = "Basarili",
            Severity = "success",
            BloklayiciMi = false
        };
    }

    private static KdvBeyannameHazirlikKontrolMaddesiDto Uyari(
        string kod, string baslik, string aciklama,
        int? etkilenenKayitSayisi = null, string? route = null)
    {
        return new KdvBeyannameHazirlikKontrolMaddesiDto
        {
            Kod = kod,
            Baslik = baslik,
            Aciklama = aciklama,
            Durum = "Uyari",
            Severity = "warn",
            BloklayiciMi = false,
            EtkilenenKayitSayisi = etkilenenKayitSayisi,
            Route = route
        };
    }

    private static KdvBeyannameHazirlikKontrolMaddesiDto Bloklayici(
        string kod, string baslik, string aciklama,
        int? etkilenenKayitSayisi = null, string? route = null)
    {
        return new KdvBeyannameHazirlikKontrolMaddesiDto
        {
            Kod = kod,
            Baslik = baslik,
            Aciklama = aciklama,
            Durum = "Bloklayici",
            Severity = "error",
            BloklayiciMi = true,
            EtkilenenKayitSayisi = etkilenenKayitSayisi,
            Route = route
        };
    }

    // ── Tarih çözümü (KdvOzetRaporService ile aynı mantık) ──

    private static (DateTime baslangic, DateTime bitis) CozumleTarihAraligi(
        KdvBeyannameHazirlikKontrolFilterDto filter)
    {
        if (filter.MaliYil > 0 && filter.Donem > 0)
        {
            var maliYil = filter.MaliYil;
            var donem = filter.Donem;

            if (maliYil < 2000 || maliYil > 2100)
                throw new BaseException("Mali yıl 2000 ile 2100 arasında olmalıdır.", errorCode: 400);

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

    // ── Access scope filtreli sorgu ──

    private async Task<IQueryable<StokHareket>> BuildFilteredStokQueryAsync(
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        KdvBeyannameHazirlikKontrolFilterDto filter,
        CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);

        var stokQuery = _db.StokHareketleri
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

        return stokQuery;
    }
}
