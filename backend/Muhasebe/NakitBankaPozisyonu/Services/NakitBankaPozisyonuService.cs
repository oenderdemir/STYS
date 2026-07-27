using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.NakitBankaPozisyonu.Dtos;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.NakitBankaPozisyonu.Services;

public class NakitBankaPozisyonuService : INakitBankaPozisyonuService
{
    private const string RaporlamaParaBirimi = "TRY";
    private const int VarsayilanSayfaBoyutu = 25;
    private const int MaksimumSayfaBoyutu = 200;

    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeTesisScopeService _tesisScopeService;

    public NakitBankaPozisyonuService(StysAppDbContext dbContext, IMuhasebeTesisScopeService tesisScopeService)
    {
        _dbContext = dbContext;
        _tesisScopeService = tesisScopeService;
    }

    public async Task<NakitBankaPozisyonuDto> GetPozisyonAsync(NakitBankaPozisyonuFilterDto filter, CancellationToken cancellationToken = default)
    {
        var bugun = BugunIstanbul();
        var raporTarihi = await ResolveRaporTarihiAsync(filter, bugun, cancellationToken);
        var tesisIds = await ResolveTesisIdsAsync(filter.TesisId, cancellationToken);
        var gecmisTarihRaporuMu = raporTarihi < bugun;

        var sonuc = new NakitBankaPozisyonuDto
        {
            RaporTarihi = raporTarihi,
            GecmisTarihRaporuMu = gecmisTarihRaporuMu,
            UygulananFiltre = filter
        };

        if (tesisIds.Count == 0)
        {
            return sonuc;
        }

        // 1) Aktif kasa/banka hesaplarini (KrediKarti HARIC - POS pozisyonu ayri, "bekleyen valor"
        //    uzerinden zaten yansitiliyor) filtreli olarak cek. Tesis composite anahtarin (TesisId,
        //    MuhasebeHesapPlaniId) bir parcasi oldugu icin TesisId burada projeksiyona dahil edilir.
        var hesapTuru = string.IsNullOrWhiteSpace(filter.HesapTuru) ? "Tumu" : filter.HesapTuru.Trim();
        var kasaBankaQuery = _dbContext.KasaBankaHesaplari.AsNoTracking()
            .Where(x => !x.IsDeleted && x.AktifMi && x.TesisId.HasValue && tesisIds.Contains(x.TesisId.Value));

        kasaBankaQuery = hesapTuru switch
        {
            "Kasa" => kasaBankaQuery.Where(x => x.Tip == KasaBankaHesapTipleri.NakitKasa),
            "Banka" => kasaBankaQuery.Where(x => x.Tip == KasaBankaHesapTipleri.Banka || x.Tip == KasaBankaHesapTipleri.DovizHesabi),
            _ => kasaBankaQuery.Where(x => x.Tip == KasaBankaHesapTipleri.NakitKasa || x.Tip == KasaBankaHesapTipleri.Banka || x.Tip == KasaBankaHesapTipleri.DovizHesabi)
        };

        if (filter.BankaHesapId.HasValue)
        {
            kasaBankaQuery = kasaBankaQuery.Where(x => x.Id == filter.BankaHesapId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.ParaBirimi))
        {
            kasaBankaQuery = kasaBankaQuery.Where(x => x.ParaBirimi == filter.ParaBirimi);
        }

        var hesaplar = await kasaBankaQuery
            .Select(x => new HesapProjeksiyon(x.Id, x.TesisId!.Value, x.Tip, x.Ad, x.Kod, x.ParaBirimi, x.MuhasebeHesapPlaniId, x.BankaAdi, x.Iban))
            .ToListAsync(cancellationToken);

        // 1b) Filtreden BAGIMSIZ, tesis kapsamindaki TUM aktif banka/IBAN hesaplarinin kimlik/para
        // birimi bilgisi - POS valor kayitlarinin BagliBankaHesapId'sinin "gercekten yok/pasif" mi
        // yoksa yalnizca bu görünümün filtresi disinda mi kaldigini ayirt etmek icin (yanlis
        // pozitif uyari uretmemek adina) AYRI ve filtrelenMEMIS bir sorgu gerekir.
        var tumBankaHesapKimlikleri = await _dbContext.KasaBankaHesaplari.AsNoTracking()
            .Where(x => !x.IsDeleted && x.AktifMi && x.TesisId.HasValue && tesisIds.Contains(x.TesisId.Value)
                && (x.Tip == KasaBankaHesapTipleri.Banka || x.Tip == KasaBankaHesapTipleri.DovizHesabi))
            .Select(x => new { x.Id, x.ParaBirimi })
            .ToDictionaryAsync(x => x.Id, x => x.ParaBirimi, cancellationToken);

        // 2) Bagli muhasebe hesap plani bilgisi (Kod/Ad/aktiflik/soft-delete) - TEK sorgu.
        // IgnoreQueryFilters() KASITLI: soft-delete edilmis bir baglanti yalnizca boyle tespit
        // edilebilir - normal (filtreli) sorgu bu satiri sessizce YOK SAYARDI.
        var hesapPlaniIds = hesaplar.Where(h => h.MuhasebeHesapPlaniId.HasValue).Select(h => h.MuhasebeHesapPlaniId!.Value).Distinct().ToList();
        var hesapPlanlari = hesapPlaniIds.Count == 0
            ? []
            : await _dbContext.MuhasebeHesapPlanlari.IgnoreQueryFilters().AsNoTracking()
                .Where(x => hesapPlaniIds.Contains(x.Id))
                .Select(x => new HesapPlaniProjeksiyon(x.Id, x.TamKod, x.Ad, x.IsDeleted, x.AktifMi))
                .ToListAsync(cancellationToken);
        var hesapPlaniLookup = hesapPlanlari.ToDictionary(x => x.Id);

        // 3) Bakiyeleri (TesisId, MuhasebeHesapPlaniId) BILESIK anahtariyla TEK gruplu sorguda
        // hesapla - ayni HesapPlaniId'nin farkli tesislerde kullanilmasi durumunda bakiyelerin
        // KARISMAMASI icin. Yalnizca GECERLI (soft-delete edilmemis VE aktif) hesap planlari dahil
        // edilir: pasif/silinmis bir muhasebe hesabi normal pozisyona girmez.
        var gecerliHesapPlaniIds = hesapPlanlari.Where(x => !x.IsDeleted && x.AktifMi).Select(x => x.Id).ToList();
        var gecerliHesapPlaniIdSet = gecerliHesapPlaniIds.ToHashSet();
        var bakiyeler = await GetBakiyelerAsync(gecerliHesapPlaniIds, tesisIds, raporTarihi, cancellationToken);

        var uyarilar = new UyariToplayici();

        // 4) POS valor kayitlari.
        //
        // GECMIS TARIH KARARI: PosTahsilatValor'da IPTAL ZAMANI ve DURUM GECIS TARIHCESI
        // TUTULMAZ (mevcut gercek alanlar yalnizca CreatedAt, AktarimTarihi, DeletedAt ve bagli
        // MuhasebeFis.FisTarihi'dir; PosTahsilatValorDegisiklikGecmisi yalnizca MANUEL komisyon/net
        // duzenlemelerini kaydeder, durum gecislerini DEGIL). Bu nedenle bir kaydin gecmis bir
        // tarihteki gercek durumu (bekliyor / mutabakat / hata / iptal) DETERMINISTIK olarak
        // kurulamaz. Veri modelinin saglayamadigi bir tarihsel dogrulugu tahmin ederek uretmemek
        // icin, gecmis tarihli raporlarda POS pozisyonu HIC HESAPLANMAZ - tum POS tutarlari
        // finansal toplamlarin TAMAMINDAN cikarilir ve durum kullaniciya acikca bildirilir.
        // Muhasebe bakiyesi tarafi bundan BAGIMSIZDIR: fis satirlari gercek FisTarihi tasidigi icin
        // gecmis tarihli muhasebe bakiyesi hesaplanmaya devam eder.
        List<ValorProjeksiyon> valorKayitlari = [];
        var fisDogrulamalari = new Dictionary<int, DogrulanmisFis>();
        var fisEtkilenenHesaplar = new Dictionary<int, HashSet<int>>();
        var fisOzetleri = new Dictionary<int, FisOzeti>();
        var fisHesapNetEtkiSozlugu = new Dictionary<(int FisId, int HesapId), decimal>();
        if (gecmisTarihRaporuMu)
        {
            sonuc.PosPozisyonuHesaplandiMi = false;
            sonuc.PosPozisyonuHesaplanmamaNedeni =
                "Geçmiş tarihli raporlarda POS/valör pozisyonu hesaplanmaz: POS valör kayıtlarında iptal zamanı ve durum geçiş geçmişi " +
                "tutulmadığından, bir kaydın seçilen geçmiş tarihteki gerçek durumu güvenilir biçimde belirlenemez. " +
                "Aşağıdaki banka bakiyeleri yalnızca muhasebe kayıtlarına dayanır; bekleyen POS tutarı içermez.";
            uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.GecmisTarihPosPozisyonuHesaplanmadi, null, null, null,
                sonuc.PosPozisyonuHesaplanmamaNedeni);
        }
        else
        {
            // Bugunun raporu: kaydin BUGUN itibariyla var olmasi yeterlidir; kaydin sisteme giris
            // zamani (CreatedAt) ile filtrelemeye gerek yoktur. ValorDurumu filtresi KASITLI olarak
            // uygulanmaz (yalnizca detay sorgularini etkiler, bkz. DTO doc).
            valorKayitlari = await _dbContext.PosTahsilatValorleri.AsNoTracking()
                .Where(v => !v.IsDeleted && tesisIds.Contains(v.TesisId))
                .Select(v => new ValorProjeksiyon(
                    v.Id, v.TesisId, v.BagliBankaHesapId, v.Durum, v.BeklenenValorTarihi, v.BrutTutar, v.KomisyonTutari, v.NetTutar,
                    v.ParaBirimi, v.MuhasebeFisId, v.TersKayitMuhasebeFisId))
                .ToListAsync(cancellationToken);

            // Valorlerin isaret ettigi fisleri TEK sorguda, IgnoreQueryFilters ile (soft-delete
            // edilmis fisi "bulunamadi" degil "silinmis" olarak ayirt edebilmek icin) dogrula.
            // Ayrica fis SATIRLARINDAN, beklenen kasa/banka hesabinin gercekten etkilenip
            // etkilenmedigi tespit edilir - N+1 YOK, iki gruplu sorgu.
            var fisIdleri = valorKayitlari
                .SelectMany(v => new[] { v.MuhasebeFisId, v.TersKayitMuhasebeFisId })
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();

            if (fisIdleri.Count > 0)
            {
                // Tesis kisiti KORUNUR: IgnoreQueryFilters yalnizca soft-delete'i "bulunamadi"dan
                // ayirt etmek icindir; baska tesisin fis verisi bu sorguya GIRMEZ.
                var fisler = await _dbContext.MuhasebeFisler.IgnoreQueryFilters().AsNoTracking()
                    .Where(f => fisIdleri.Contains(f.Id) && tesisIds.Contains(f.TesisId))
                    .Select(f => new { f.Id, f.IsDeleted, f.Durum, f.TesisId, f.MaliYil, f.Donem, f.FisTarihi, f.ToplamBorc, f.IptalEdilenFisId })
                    .ToListAsync(cancellationToken);

                // Ters kayit iliskisi: ters kayit fisinin IptalEdilenFisId'si ve ayni asil fise
                // bagli ters kayit adedi (mukerrer terslenme tespiti) - TEK gruplu sorgu.
                var iptalEdilenIdler = fisler.Where(f => f.IptalEdilenFisId.HasValue).Select(f => f.IptalEdilenFisId!.Value).Distinct().ToList();
                var tersKayitSayaclari = iptalEdilenIdler.Count == 0
                    ? []
                    : await _dbContext.MuhasebeFisler.IgnoreQueryFilters().AsNoTracking()
                        .Where(f => f.IptalEdilenFisId.HasValue && iptalEdilenIdler.Contains(f.IptalEdilenFisId.Value)
                            && tesisIds.Contains(f.TesisId))
                        .GroupBy(f => f.IptalEdilenFisId!.Value)
                        .Select(g => new { AsilFisId = g.Key, Adet = g.Count() })
                        .ToDictionaryAsync(x => x.AsilFisId, x => x.Adet, cancellationToken);

                // Fis -> Kurum cozumlemesi (Tesis uzerinden) - ters kayit tesis/kurum uyumu icin.
                var fisTesisIdleri = fisler.Select(f => f.TesisId).Distinct().ToList();
                var kurumIdSozlugu = await _dbContext.Tesisler.AsNoTracking()
                    .Where(t => fisTesisIdleri.Contains(t.Id))
                    .Select(t => new { t.Id, t.KurumId })
                    .ToDictionaryAsync(x => x.Id, x => (int?)x.KurumId, cancellationToken);

                // Fisin TEMSILI para birimi - satir bazinda tutulur, ilk (SiraNo) satirdan okunur.
                var fisParaBirimleri = await _dbContext.MuhasebeFisSatirlari.IgnoreQueryFilters().AsNoTracking()
                    .Where(s => !s.IsDeleted && fisIdleri.Contains(s.MuhasebeFisId))
                    .GroupBy(s => s.MuhasebeFisId)
                    .Select(g => new { FisId = g.Key, ParaBirimi = g.OrderBy(x => x.SiraNo).Select(x => x.ParaBirimi).FirstOrDefault() })
                    .ToDictionaryAsync(x => x.FisId, x => x.ParaBirimi, cancellationToken);

                fisOzetleri = fisler.ToDictionary(
                    f => f.Id,
                    f => new FisOzeti(
                        f.IptalEdilenFisId, f.ToplamBorc, f.TesisId,
                        KurumId: kurumIdSozlugu.GetValueOrDefault(f.TesisId),
                        ParaBirimi: fisParaBirimleri.GetValueOrDefault(f.Id),
                        TersKayitAdedi: f.IptalEdilenFisId.HasValue ? tersKayitSayaclari.GetValueOrDefault(f.IptalEdilenFisId.Value) : 0));

                // (FisId, KasaBankaHesapId) -> NET etki (Borc-Alacak) - hem "hangi hesaplar
                // etkilendi" (varlik) hem "ters kayitta bu hesap GERCEKTEN ters yonde mi etkilendi"
                // (madde 7 - ters yonlu hesap etkisi) sorularina TEK sorguyla cevap verir.
                var fisHesapNetEtkileri = await _dbContext.MuhasebeFisSatirlari.IgnoreQueryFilters().AsNoTracking()
                    .Where(s => !s.IsDeleted && fisIdleri.Contains(s.MuhasebeFisId) && s.KasaBankaHesapId.HasValue)
                    .GroupBy(s => new { s.MuhasebeFisId, KasaBankaHesapId = s.KasaBankaHesapId!.Value })
                    .Select(g => new { g.Key.MuhasebeFisId, g.Key.KasaBankaHesapId, Net = g.Sum(x => x.Borc - x.Alacak) })
                    .ToListAsync(cancellationToken);

                fisHesapNetEtkiSozlugu = fisHesapNetEtkileri.ToDictionary(x => (x.MuhasebeFisId, x.KasaBankaHesapId), x => x.Net);

                var etkilenenHesaplar = fisHesapNetEtkileri
                    .GroupBy(x => x.MuhasebeFisId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.KasaBankaHesapId).ToHashSet());

                foreach (var f in fisler)
                {
                    fisDogrulamalari[f.Id] = new DogrulanmisFis(
                        FisId: f.Id,
                        Bulundu: true,
                        SoftDeleteEdilmis: f.IsDeleted,
                        Durum: f.Durum,
                        TesisId: f.TesisId,
                        MaliYil: f.MaliYil,
                        Donem: f.Donem,
                        FisTarihi: f.FisTarihi,
                        BeklenenKasaBankaHesabiEtkilenmisMi: null); // hesap bazli deger asagida kayit basina cozulur
                }

                fisEtkilenenHesaplar = etkilenenHesaplar;
            }
        }

        // 5) Kasa satirlari.
        foreach (var h in hesaplar.Where(x => x.Tip == KasaBankaHesapTipleri.NakitKasa))
        {
            var hesapPlani = h.MuhasebeHesapPlaniId.HasValue ? hesapPlaniLookup.GetValueOrDefault(h.MuhasebeHesapPlaniId.Value) : null;
            var bakiye = h.MuhasebeHesapPlaniId.HasValue ? bakiyeler.GetValueOrDefault((h.TesisId, h.MuhasebeHesapPlaniId.Value)) : default;

            sonuc.KasaHesaplari.Add(new NakitHesapPozisyonuDto
            {
                KasaBankaHesapId = h.Id,
                TesisId = h.TesisId,
                Ad = h.Ad,
                Kod = h.Kod,
                ParaBirimi = NormalizeParaBirimi(h.ParaBirimi),
                MuhasebeHesapPlaniId = h.MuhasebeHesapPlaniId,
                MuhasebeHesapKodu = hesapPlani?.TamKod,
                MuhasebeHesapAdi = hesapPlani?.Ad,
                MuhasebeBakiyesi = bakiye.Borc - bakiye.Alacak,
                SonHareketTarihi = bakiye.SonHareket
            });
        }

        // 6) Banka/IBAN satirlari. Her valor kaydi PosValorFinansalSiniflandirici'den gecer ve
        //    SADECE tek bir kategoriye girer; yalnizca NormalBekleyen kategorisi tarih bucket'larina
        //    ve tahmini bakiyeye katilir. Diger TUM kategoriler (bilinen durumlar, TANINMAYAN
        //    durumlar ve veri kalitesi kapisindan gecemeyen kayitlar) toplamlarin disinda kalir.
        foreach (var h in hesaplar.Where(x => x.Tip == KasaBankaHesapTipleri.Banka || x.Tip == KasaBankaHesapTipleri.DovizHesabi))
        {
            var hesapPlani = h.MuhasebeHesapPlaniId.HasValue ? hesapPlaniLookup.GetValueOrDefault(h.MuhasebeHesapPlaniId.Value) : null;
            var muhasebeBaglantisiGecerli = h.MuhasebeHesapPlaniId.HasValue && gecerliHesapPlaniIdSet.Contains(h.MuhasebeHesapPlaniId.Value);
            var bakiye = muhasebeBaglantisiGecerli ? bakiyeler.GetValueOrDefault((h.TesisId, h.MuhasebeHesapPlaniId!.Value)) : default;

            var dto = new BankaHesapPozisyonuDto
            {
                KasaBankaHesapId = h.Id,
                TesisId = h.TesisId,
                BankaAdi = h.BankaAdi ?? string.Empty,
                HesapAdi = h.Ad,
                Iban = h.Iban,
                ParaBirimi = NormalizeParaBirimi(h.ParaBirimi),
                MuhasebeHesapPlaniId = h.MuhasebeHesapPlaniId,
                MuhasebeHesapKodu = hesapPlani?.TamKod,
                MuhasebeBakiyesiGecerliMi = muhasebeBaglantisiGecerli,
                StysMuhasebeBakiyesi = bakiye.Borc - bakiye.Alacak,
                SonMuhasebeHareketTarihi = bakiye.SonHareket
            };

            foreach (var v in valorKayitlari.Where(v => v.BagliBankaHesapId == h.Id))
            {
                UygulaValorKaydi(v, h, dto, raporTarihi, muhasebeBaglantisiGecerli, uyarilar, fisDogrulamalari, fisEtkilenenHesaplar, fisOzetleri, fisHesapNetEtkiSozlugu);
            }

            dto.ToplamBekleyenNet = dto.ValoruGecmisBekleyenNet + dto.BugunGelecekNet + dto.YarinGelecekNet
                + dto.Takip2_7GunGelecekNet + dto.Sonraki7GundenSonraNet;

            // Muhasebe baglantisi gecersizse StysMuhasebeBakiyesi anlamsizdir - yalnizca POS
            // tutarindan olusan SAHTE bir "tahmini bakiye" URETILMEZ (null birakilir).
            dto.TahminiBakiye = muhasebeBaglantisiGecerli
                ? dto.StysMuhasebeBakiyesi + dto.ToplamBekleyenNet
                : null;

            sonuc.BankaHesaplari.Add(dto);
        }

        // 7) BagliBankaHesapId dolu ama bu tesis kapsaminda hicbir aktif banka hesabina karsilik
        //    GELMEYEN (yok/pasif/silinmis) kayitlar - hesaplar dongusune hic girmediginden burada
        //    AYRICA tespit edilir (aksi halde sessizce kaybolurlardi).
        //    Bu kayitlar HICBIR banka DTO'suna baglanamadigi icin tutarlari yalnizca uyari metninde
        //    kalmamali - genel UyariliTutarlar ozetine de girmeli (bkz. baglanamayanUyariliTutarlar).
        var baglanamayanUyariliTutarlar = new List<UyariliTutarOzetiDto>();

        void EkleBaglanamayan(string uyariTipi, string? paraBirimi, decimal netTutar, string aciklama)
        {
            var pb = NormalizeParaBirimi(paraBirimi);
            var mevcut = baglanamayanUyariliTutarlar.FirstOrDefault(x => x.UyariTipi == uyariTipi && x.ParaBirimi == pb);
            if (mevcut is null)
            {
                baglanamayanUyariliTutarlar.Add(new UyariliTutarOzetiDto
                {
                    UyariTipi = uyariTipi, ParaBirimi = pb, Adet = 1, ToplamNetTutar = netTutar, Aciklama = aciklama
                });
                return;
            }
            mevcut.Adet++;
            mevcut.ToplamNetTutar += netTutar;
        }

        foreach (var v in valorKayitlari.Where(v => v.BagliBankaHesapId.HasValue && !tumBankaHesapKimlikleri.ContainsKey(v.BagliBankaHesapId!.Value)))
        {
            const string aciklama = "POS valör kaydının bağlı olduğu banka hesabı bulunamadı, silinmiş veya pasif.";
            uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.BankaHesabiBulunamadiVeyaPasif, v.BagliBankaHesapId, v.Id, v.NetTutar, aciklama, v.ParaBirimi);
            EkleBaglanamayan(NakitBankaPozisyonuUyariTipleri.BankaHesabiBulunamadiVeyaPasif, v.ParaBirimi, v.NetTutar, aciklama);
        }

        // 8) Hedef banka hesabi tanimsiz (BagliBankaHesapId NULL) kayitlar.
        foreach (var v in valorKayitlari.Where(v => !v.BagliBankaHesapId.HasValue))
        {
            const string aciklama = "POS valör kaydının hedef banka hesabı (BagliBankaHesapId) belirlenemiyor.";
            uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.PosValorHedefBankaBelirlenemiyor, null, v.Id, v.NetTutar, aciklama, v.ParaBirimi);
            EkleBaglanamayan(NakitBankaPozisyonuUyariTipleri.PosValorHedefBankaBelirlenemiyor, v.ParaBirimi, v.NetTutar, aciklama);
        }

        BuildYapisalUyarilar(hesaplar, hesapPlaniLookup, uyarilar);

        sonuc.Uyarilar = uyarilar.Listele();
        sonuc.Ozet = BuildOzet(sonuc, gecmisTarihRaporuMu, baglanamayanUyariliTutarlar);

        return sonuc;
    }

    public async Task<BankaValorTakvimiDto> GetValorTakvimiAsync(int kasaBankaHesapId, DateOnly? raporTarihi, CancellationToken cancellationToken = default)
    {
        var hesap = await DogrulaVeYetkilendirAsync(kasaBankaHesapId, cancellationToken);
        var etkinRaporTarihi = raporTarihi ?? BugunIstanbul();

        var gunler = await _dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(v => !v.IsDeleted && v.BagliBankaHesapId == kasaBankaHesapId && v.Durum == PosTahsilatValorDurumlari.ValorBekliyor)
            .GroupBy(v => v.BeklenenValorTarihi)
            .OrderBy(g => g.Key)
            .Select(g => new GunlukValorOzetiDto
            {
                ValorTarihi = g.Key,
                IslemSayisi = g.Count(),
                BrutTutar = g.Sum(x => x.BrutTutar),
                KomisyonTutari = g.Sum(x => x.KomisyonTutari),
                NetTutar = g.Sum(x => x.NetTutar)
            })
            .ToListAsync(cancellationToken);

        return new BankaValorTakvimiDto
        {
            KasaBankaHesapId = kasaBankaHesapId,
            RaporTarihi = etkinRaporTarihi,
            Gunler = gunler
        };
    }

    public async Task<PagedResult<ValorDetayDto>> GetValorGunDetaylariAsync(
        int kasaBankaHesapId, DateOnly valorTarihi, string? valorDurumu, int sayfa, int sayfaBoyutu, CancellationToken cancellationToken = default)
    {
        await DogrulaVeYetkilendirAsync(kasaBankaHesapId, cancellationToken);

        var etkinSayfa = sayfa < 1 ? 1 : sayfa;
        var etkinSayfaBoyutu = sayfaBoyutu <= 0 ? VarsayilanSayfaBoyutu : Math.Min(sayfaBoyutu, MaksimumSayfaBoyutu);

        var query = _dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(v => !v.IsDeleted && v.BagliBankaHesapId == kasaBankaHesapId && v.BeklenenValorTarihi == valorTarihi);

        if (!string.IsNullOrWhiteSpace(valorDurumu))
        {
            query = query.Where(v => v.Durum == valorDurumu);
        }

        var toplam = await query.CountAsync(cancellationToken);

        var kayitlar = await query
            .OrderBy(v => v.Id)
            .Skip((etkinSayfa - 1) * etkinSayfaBoyutu)
            .Take(etkinSayfaBoyutu)
            .Select(v => new
            {
                v.Id,
                v.TahsilatOdemeBelgesiId,
                v.KrediKartiHesapId,
                v.OdemeTarihi,
                v.BeklenenValorTarihi,
                v.BrutTutar,
                v.KomisyonTutari,
                v.NetTutar,
                v.Durum,
                v.MuhasebeFisId,
                v.HataMesaji
            })
            .ToListAsync(cancellationToken);

        var belgeIdler = kayitlar.Select(x => x.TahsilatOdemeBelgesiId).Distinct().ToList();
        var belgeNolari = belgeIdler.Count == 0
            ? new Dictionary<int, string?>()
            : await _dbContext.TahsilatOdemeBelgeleri.AsNoTracking()
                .Where(x => belgeIdler.Contains(x.Id))
                .Select(x => new { x.Id, x.BelgeNo })
                .ToDictionaryAsync(x => x.Id, x => (string?)x.BelgeNo, cancellationToken);

        var krediKartiHesapIdler = kayitlar.Select(x => x.KrediKartiHesapId).Distinct().ToList();
        var krediKartiAdlari = krediKartiHesapIdler.Count == 0
            ? new Dictionary<int, string?>()
            : await _dbContext.KasaBankaHesaplari.AsNoTracking()
                .Where(x => krediKartiHesapIdler.Contains(x.Id))
                .Select(x => new { x.Id, x.Ad })
                .ToDictionaryAsync(x => x.Id, x => (string?)x.Ad, cancellationToken);

        var detaylar = kayitlar.Select(x => new ValorDetayDto
        {
            Id = x.Id,
            TahsilatOdemeBelgesiId = x.TahsilatOdemeBelgesiId,
            TahsilatBelgeNo = belgeNolari.GetValueOrDefault(x.TahsilatOdemeBelgesiId),
            KrediKartiHesapAdi = krediKartiAdlari.GetValueOrDefault(x.KrediKartiHesapId),
            OdemeTarihi = x.OdemeTarihi,
            BeklenenValorTarihi = x.BeklenenValorTarihi,
            BrutTutar = x.BrutTutar,
            KomisyonTutari = x.KomisyonTutari,
            NetTutar = x.NetTutar,
            Durum = x.Durum,
            MuhasebeFisId = x.MuhasebeFisId,
            HataMesaji = x.HataMesaji
        }).ToList();

        return new PagedResult<ValorDetayDto>(detaylar, etkinSayfa, etkinSayfaBoyutu, toplam);
    }

    // ─────────────────────────────────────────────────────────────
    // Projeksiyon tipleri
    // ─────────────────────────────────────────────────────────────

    private sealed record HesapProjeksiyon(int Id, int TesisId, string Tip, string Ad, string Kod, string? ParaBirimi, int? MuhasebeHesapPlaniId, string? BankaAdi, string? Iban);

    private sealed record HesapPlaniProjeksiyon(int Id, string TamKod, string Ad, bool IsDeleted, bool AktifMi);

    private sealed record ValorProjeksiyon(
        int Id, int TesisId, int? BagliBankaHesapId, string Durum, DateOnly BeklenenValorTarihi, decimal BrutTutar,
        decimal KomisyonTutari, decimal NetTutar, string ParaBirimi, int? MuhasebeFisId, int? TersKayitMuhasebeFisId);

    /// <summary>Ters kayit iliskisi dogrulamasi icin toplanan fis ozeti - IptalEdilenFisId (otoriter
    /// iliski), tesis/kurum kapsami, tutar/para birimi ve mukerrer ters kayit adedi.</summary>
    private sealed record FisOzeti(int? IptalEdilenFisId, decimal ToplamBorc, int TesisId, int? KurumId, string? ParaBirimi, int TersKayitAdedi);

    // ─────────────────────────────────────────────────────────────
    // Yardimcilar
    // ─────────────────────────────────────────────────────────────

    private async Task<(int TesisId, string Tip)> DogrulaVeYetkilendirAsync(int kasaBankaHesapId, CancellationToken cancellationToken)
    {
        var hesap = await _dbContext.KasaBankaHesaplari.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Id == kasaBankaHesapId)
            .Select(x => new { x.Id, x.TesisId, x.Tip })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException("Banka/IBAN hesabı bulunamadı.", 404);

        if (hesap.Tip != KasaBankaHesapTipleri.Banka && hesap.Tip != KasaBankaHesapTipleri.DovizHesabi)
        {
            throw new BaseException("Yalnızca Banka/Döviz tipi hesaplar için valör takvimi görüntülenebilir.", 400);
        }

        var tesisId = hesap.TesisId ?? throw new BaseException("Hesabın tesisi belirlenemedi.", 400);
        await _tesisScopeService.EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        return (tesisId, hesap.Tip);
    }

    /// <summary>Tek bir valor kaydinin, tek bir banka hesabi (dto) uzerindeki etkisini belirler.
    /// Siniflandirmayi PosValorFinansalSiniflandirici (saf, DB'siz, ayrica birim testi yazilabilen
    /// bilesen) yapar; bu metot yalnizca sonucu ilgili sayaca/bucket'a yazar. YALNIZCA
    /// NormalBekleyen kategorisi tarih bucket'larina ve dolayisiyla tahmini bakiyeye katilir.</summary>
    private static void UygulaValorKaydi(
        ValorProjeksiyon v, HesapProjeksiyon h, BankaHesapPozisyonuDto dto, DateOnly raporTarihi,
        bool muhasebeBaglantisiGecerli, UyariToplayici uyarilar,
        IReadOnlyDictionary<int, DogrulanmisFis> fisDogrulamalari,
        IReadOnlyDictionary<int, HashSet<int>> fisEtkilenenHesaplar,
        IReadOnlyDictionary<int, FisOzeti> fisOzetleri,
        IReadOnlyDictionary<(int FisId, int HesapId), decimal> fisHesapNetEtkiSozlugu)
    {
        var siniflandirma = PosValorFinansalSiniflandirici.Siniflandir(new PosValorSiniflandirmaGirdisi(
            Durum: v.Durum,
            BeklenenValorTarihi: v.BeklenenValorTarihi,
            BrutTutar: v.BrutTutar,
            KomisyonTutari: v.KomisyonTutari,
            NetTutar: v.NetTutar,
            ValorParaBirimi: v.ParaBirimi,
            BankaHesabiParaBirimi: h.ParaBirimi,
            MuhasebeFisId: v.MuhasebeFisId,
            TersKayitMuhasebeFisId: v.TersKayitMuhasebeFisId,
            BankaHesabiGecerliMi: true, // bu dala girildiyse hesap zaten aktif+silinmemis yuklenmistir
            MuhasebeHesabiGecerliMi: muhasebeBaglantisiGecerli,
            DogrulanmisAktarimFisi: CozumleFis(v.MuhasebeFisId, h.Id, fisDogrulamalari, fisEtkilenenHesaplar),
            DogrulanmisTersKayitFisi: CozumleFis(v.TersKayitMuhasebeFisId, h.Id, fisDogrulamalari, fisEtkilenenHesaplar),
            ValorTesisId: v.TesisId,
            BankaHesabiTesisId: h.TesisId,
            TersKayitIliskisi: CozumleTersKayitIliskisi(v, h.Id, fisOzetleri, fisHesapNetEtkiSozlugu)));

        switch (siniflandirma.Kategori)
        {
            case PosValorKategori.MutabakatBekliyor:
                dto.MutabakatBekleyenNet += v.NetTutar;
                dto.MutabakatBekleyenAdet++;
                EkleUyariliTutar(dto, NakitBankaPozisyonuUyariTipleri.MutabakatBekleyen, v.ParaBirimi, v.NetTutar,
                    "Mutabakat bekleyen POS tahsilatı - komisyon/net bilgisi kesinleşmediği için tahmini bakiyeye dahil edilmedi.");
                return;

            case PosValorKategori.Hatali:
                dto.HataliNet += v.NetTutar;
                dto.HataliAdet++;
                EkleUyariliTutar(dto, NakitBankaPozisyonuUyariTipleri.HataliValor, v.ParaBirimi, v.NetTutar,
                    "Aktarımı hata ile sonuçlanmış POS tahsilatı - tahmini bakiyeye dahil edilmedi.");
                return;

            case PosValorKategori.Aktarilmis:
            case PosValorKategori.IptalEdilmis:
                // Aktarilmis kaydin etkisini muhasebe bakiyesi kendi fisi uzerinden ZATEN icerir;
                // iptal edilmis kayit ise gecersiz kilinmistir. Ikisi de bekleyen tutara EKLENMEZ.
                return;

            case PosValorKategori.AktarimSurecinde:
            case PosValorKategori.TersKayitSurecinde:
                EkleUyariliTutar(dto, NakitBankaPozisyonuUyariTipleri.AktarimSurecindeValor, v.ParaBirimi, v.NetTutar,
                    $"Kayıt '{v.Durum}' ara durumunda (işlem sürüyor) - sonucu kesinleşmediği için tahmini bakiyeye dahil edilmedi.");
                return;

            case PosValorKategori.TaninmayanDurum:
            case PosValorKategori.VeriKalitesiUyarisi:
                uyarilar.Ekle(siniflandirma.UyariTipi!, h.Id, v.Id, v.NetTutar, siniflandirma.Aciklama!, v.ParaBirimi);
                EkleUyariliTutar(dto, siniflandirma.UyariTipi!, v.ParaBirimi, v.NetTutar, siniflandirma.Aciklama!);
                return;

            case PosValorKategori.NormalBekleyen:
                break;
        }

        // NormalBekleyen - tarih bucket'ina gore KESIN OLARAK tek bir gruba girer.
        if (v.BeklenenValorTarihi < raporTarihi)
        {
            dto.ValoruGecmisBekleyenNet += v.NetTutar;
        }
        else if (v.BeklenenValorTarihi == raporTarihi)
        {
            dto.BugunGelecekNet += v.NetTutar;
        }
        else if (v.BeklenenValorTarihi == raporTarihi.AddDays(1))
        {
            dto.YarinGelecekNet += v.NetTutar;
        }
        else if (v.BeklenenValorTarihi <= raporTarihi.AddDays(7))
        {
            dto.Takip2_7GunGelecekNet += v.NetTutar;
        }
        else
        {
            dto.Sonraki7GundenSonraNet += v.NetTutar;
        }
    }

    /// <summary>Bir fis id'sini, sorgu katmaninda toplanan dogrulama bilgisine cozer. ID dolu oldugu
    /// halde sozlukte yoksa fis GERCEKTEN bulunamamis demektir (Bulundu=false doner) - bu, "ID dolu
    /// oldugu icin gecerli sayma" hatasini yapisal olarak engeller.</summary>
    private static DogrulanmisFis? CozumleFis(
        int? fisId, int beklenenKasaBankaHesapId,
        IReadOnlyDictionary<int, DogrulanmisFis> fisDogrulamalari,
        IReadOnlyDictionary<int, HashSet<int>> fisEtkilenenHesaplar)
    {
        if (!fisId.HasValue)
        {
            return null;
        }

        if (!fisDogrulamalari.TryGetValue(fisId.Value, out var fis))
        {
            return new DogrulanmisFis(fisId.Value, Bulundu: false, SoftDeleteEdilmis: false,
                Durum: null, TesisId: null, MaliYil: null, Donem: null, FisTarihi: null,
                BeklenenKasaBankaHesabiEtkilenmisMi: null);
        }

        var hesapEtkilenmis = fisEtkilenenHesaplar.TryGetValue(fisId.Value, out var hesaplar)
            && hesaplar.Contains(beklenenKasaBankaHesapId);

        return fis with { BeklenenKasaBankaHesabiEtkilenmisMi = hesapEtkilenmis };
    }

    /// <summary>Ters kayit fisinin ASIL fisi gercekten tersleyip terslemedigini degerlendirmek icin
    /// gereken gercek verileri (IptalEdilenFisId, tutarlar, tesisler, mukerrer ters kayit adedi)
    /// toplar. Veri yoksa null doner - bu durumda dogrulama "kanitlanamadi" sonucunu uretir.</summary>
    /// <summary>Tutar karsilastirmasinda kabul edilen yuvarlama toleransi (ters yonlu hesap etkisi icin).</summary>
    private const decimal TersYonHesapEtkisiToleransi = 0.01m;

    private static TersKayitIliskisi? CozumleTersKayitIliskisi(
        ValorProjeksiyon v, int hesapId,
        IReadOnlyDictionary<int, FisOzeti> fisOzetleri,
        IReadOnlyDictionary<(int FisId, int HesapId), decimal> fisHesapNetEtkiSozlugu)
    {
        if (!v.TersKayitMuhasebeFisId.HasValue || !fisOzetleri.TryGetValue(v.TersKayitMuhasebeFisId.Value, out var ters))
        {
            return null;
        }

        FisOzeti? asil = v.MuhasebeFisId.HasValue && fisOzetleri.TryGetValue(v.MuhasebeFisId.Value, out var a) ? a : null;

        // Ters yonlu hesap etkisi (madde 7): asil fiste BU hesabin (h.Id) net etkisi ile ters
        // kayittaki net etkisi TOPLAMDA SIFIRA yakin olmalidir (borc/alacak yer degistirip birbirini
        // TAM olarak iptal etmelidir). Ikisinden biri bu hesaba dokunmuyorsa DOGRULANAMAZ (null).
        bool? tersYonluUyumlu = null;
        if (asil is not null
            && fisHesapNetEtkiSozlugu.TryGetValue((v.MuhasebeFisId!.Value, hesapId), out var asilNet)
            && fisHesapNetEtkiSozlugu.TryGetValue((v.TersKayitMuhasebeFisId.Value, hesapId), out var tersNet))
        {
            tersYonluUyumlu = Math.Abs(asilNet + tersNet) <= TersYonHesapEtkisiToleransi;
        }

        return new TersKayitIliskisi(
            TersKayitFisId: v.TersKayitMuhasebeFisId.Value,
            AsilFisId: v.MuhasebeFisId,
            TersKayitIptalEdilenFisId: ters.IptalEdilenFisId,
            TersKayitTesisId: ters.TesisId,
            AsilFisTesisId: asil?.TesisId,
            TersKayitKurumId: ters.KurumId,
            AsilFisKurumId: asil?.KurumId,
            TersKayitToplamBorc: ters.ToplamBorc,
            AsilFisToplamBorc: asil?.ToplamBorc,
            TersKayitParaBirimi: ters.ParaBirimi,
            AsilFisParaBirimi: asil?.ParaBirimi,
            TersYonluHesapEtkisiUyumluMu: tersYonluUyumlu,
            AyniAsilFiseBagliTersKayitSayisi: ters.TersKayitAdedi);
    }

    /// <summary>Normal toplamin disinda kalan tutarlari, hesap bazinda (UyariTipi, ParaBirimi)
    /// kiriliminda toplar - farkli para birimleri ASLA birlestirilmez.</summary>
    private static void EkleUyariliTutar(BankaHesapPozisyonuDto dto, string uyariTipi, string? paraBirimi, decimal netTutar, string aciklama)
    {
        // Bos para birimi TRY VARSAYILMAZ - "Bilinmiyor" olarak AYRI gosterilir.
        var pb = NormalizeParaBirimi(paraBirimi);
        var mevcut = dto.UyariliTutarlar.FirstOrDefault(x => x.UyariTipi == uyariTipi && x.ParaBirimi == pb);
        if (mevcut is null)
        {
            dto.UyariliTutarlar.Add(new UyariliTutarOzetiDto
            {
                UyariTipi = uyariTipi,
                ParaBirimi = pb,
                Adet = 1,
                ToplamNetTutar = netTutar,
                Aciklama = aciklama
            });
            return;
        }

        mevcut.Adet++;
        mevcut.ToplamNetTutar += netTutar;
    }

    private static NakitBankaPozisyonuOzetDto BuildOzet(
        NakitBankaPozisyonuDto sonuc, bool gecmisTarihRaporuMu, IReadOnlyList<UyariliTutarOzetiDto> baglanamayanUyariliTutarlar)
    {
        // ONEMLI: genel ozet kartlari yalnizca RAPORLAMA (TRY) para birimindeki hesaplarin
        // toplamini yansitir - farkli para birimindeki hesaplar buraya KARISTIRILMAZ (kur donusum
        // altyapisi projede yok, bkz. ParaBirimiOzetleri asagida HER para birimi icin ayri ayri
        // hesaplanir).
        var tryKasa = sonuc.KasaHesaplari.Where(x => string.Equals(x.ParaBirimi, RaporlamaParaBirimi, StringComparison.OrdinalIgnoreCase)).ToList();
        var tryBanka = sonuc.BankaHesaplari.Where(x => string.Equals(x.ParaBirimi, RaporlamaParaBirimi, StringComparison.OrdinalIgnoreCase)).ToList();

        // Muhasebe baglantisi GECERSIZ olan hesaplarin "bakiyesi" anlamsizdir (her zaman 0'dir) ve
        // genel muhasebe bakiyesi toplamina KATILMAZ - aksi halde hicbir muhasebe kaydi olmayan bir
        // hesap toplami sessizce degistirmis gibi gorunurdu.
        var tryBankaGecerli = tryBanka.Where(x => x.MuhasebeBakiyesiGecerliMi).ToList();

        var ozet = new NakitBankaPozisyonuOzetDto
        {
            RaporTarihi = sonuc.RaporTarihi,
            GecmisTarihRaporuMu = gecmisTarihRaporuMu,
            PosPozisyonuHesaplandiMi = sonuc.PosPozisyonuHesaplandiMi,
            PosPozisyonuHesaplanmamaNedeni = sonuc.PosPozisyonuHesaplanmamaNedeni,
            ToplamNakit = tryKasa.Sum(x => x.MuhasebeBakiyesi),
            ToplamBankaMuhasebeBakiyesi = tryBankaGecerli.Sum(x => x.StysMuhasebeBakiyesi),
            ValoruGecmisBekleyenNet = tryBanka.Sum(x => x.ValoruGecmisBekleyenNet),
            BugunGelecekNet = tryBanka.Sum(x => x.BugunGelecekNet),
            YarinGelecekNet = tryBanka.Sum(x => x.YarinGelecekNet),
            Takip2_7GunGelecekNet = tryBanka.Sum(x => x.Takip2_7GunGelecekNet),
            Sonraki7GundenSonraNet = tryBanka.Sum(x => x.Sonraki7GundenSonraNet),
            ToplamBekleyenNetPos = tryBanka.Sum(x => x.ToplamBekleyenNet),
            MutabakatBekleyenToplam = tryBanka.Sum(x => x.MutabakatBekleyenNet),
            MutabakatBekleyenAdet = tryBanka.Sum(x => x.MutabakatBekleyenAdet),
            HataliToplam = tryBanka.Sum(x => x.HataliNet),
            HataliAdet = tryBanka.Sum(x => x.HataliAdet),
            UyariSayisi = sonuc.Uyarilar.Count
        };
        ozet.TahminiToplamBankaPozisyonu = ozet.ToplamBankaMuhasebeBakiyesi + ozet.ToplamBekleyenNetPos;

        // Uyarili (normal toplamin DISINDA kalan) tutarlar - (UyariTipi, ParaBirimi) bazinda,
        // para birimleri BIRLESTIRILMEDEN.
        ozet.UyariliTutarlar = [.. sonuc.BankaHesaplari
            .SelectMany(x => x.UyariliTutarlar)
            // Hicbir banka hesabina baglanamayan (hedef hesap yok/pasif/silinmis) kayitlarin
            // tutarlari da ozete DAHIL edilir - aksi halde yalnizca uyari metninde kalirlardi.
            .Concat(baglanamayanUyariliTutarlar)
            .GroupBy(x => (x.UyariTipi, x.ParaBirimi))
            .Select(g => new UyariliTutarOzetiDto
            {
                UyariTipi = g.Key.UyariTipi,
                ParaBirimi = g.Key.ParaBirimi,
                Adet = g.Sum(x => x.Adet),
                ToplamNetTutar = g.Sum(x => x.ToplamNetTutar),
                Aciklama = g.First().Aciklama
            })
            .OrderByDescending(x => x.ToplamNetTutar)];

        var paraBirimleri = sonuc.KasaHesaplari.Select(x => x.ParaBirimi)
            .Concat(sonuc.BankaHesaplari.Select(x => x.ParaBirimi))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var pb in paraBirimleri)
        {
            var nakit = sonuc.KasaHesaplari.Where(x => string.Equals(x.ParaBirimi, pb, StringComparison.OrdinalIgnoreCase)).Sum(x => x.MuhasebeBakiyesi);
            var banka = sonuc.BankaHesaplari.Where(x => x.MuhasebeBakiyesiGecerliMi && string.Equals(x.ParaBirimi, pb, StringComparison.OrdinalIgnoreCase)).Sum(x => x.StysMuhasebeBakiyesi);
            var bekleyen = sonuc.BankaHesaplari.Where(x => string.Equals(x.ParaBirimi, pb, StringComparison.OrdinalIgnoreCase)).Sum(x => x.ToplamBekleyenNet);

            ozet.ParaBirimiOzetleri.Add(new ParaBirimiOzetDto
            {
                ParaBirimi = pb,
                ToplamNakit = nakit,
                ToplamBankaMuhasebeBakiyesi = banka,
                ToplamBekleyenNetPos = bekleyen,
                TahminiToplamBankaPozisyonu = banka + bekleyen
            });
        }

        return ozet;
    }

    /// <summary>
    /// MuhasebeHesapBakiyeleri tablosu yalnizca (TesisId, MaliYil, Donem) bazinda o DONEME AIT
    /// HAREKET TOPLAMINI tutar (bkz. MuhasebeHesapBakiyeGuncellemeService/RebuildAsync); "rapor
    /// tarihi itibariyla bakiye" kumulatif bir deger oldugu icin bu tablo KULLANILMAZ. Bunun
    /// yerine Hizli Mizan'in canli hesaplama yolunun (GetMizanAsync) AYNI kuralini (yalnizca
    /// Onayli+TersKayit fisler, FisTarihi &lt; rapor tarihi + 1 gun) izleyerek MuhasebeFisSatirlari'ndan
    /// DOGRUDAN hesaplanir. Sonuc (TesisId, MuhasebeHesapPlaniId) BILESIK anahtarla gruplanir -
    /// ayni HesapPlaniId'nin farkli tesislerde kullanilmasi durumunda bakiyelerin KARISMAMASI icin
    /// (yetkisiz bir tesisin verisi zaten tesisIds disinda kaldigi icin sorguya hic girmez).
    /// </summary>
    private async Task<Dictionary<(int TesisId, int HesapPlaniId), (decimal Borc, decimal Alacak, DateTime? SonHareket)>> GetBakiyelerAsync(
        IReadOnlyCollection<int> hesapPlaniIds, IReadOnlyCollection<int> tesisIds, DateOnly raporTarihi, CancellationToken cancellationToken)
    {
        if (hesapPlaniIds.Count == 0)
        {
            return [];
        }

        var ustSinir = raporTarihi.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var satirlar = _dbContext.MuhasebeFisSatirlari.AsNoTracking()
            .Where(s => !s.IsDeleted
                && hesapPlaniIds.Contains(s.MuhasebeHesapPlaniId)
                && s.MuhasebeFis != null
                && !s.MuhasebeFis.IsDeleted
                && tesisIds.Contains(s.MuhasebeFis.TesisId)
                && (s.MuhasebeFis.Durum == MuhasebeFisDurumlari.Onayli || s.MuhasebeFis.Durum == MuhasebeFisDurumlari.TersKayit)
                && s.MuhasebeFis.FisTarihi < ustSinir);

        var gruplu = await satirlar
            .GroupBy(s => new { s.MuhasebeFis!.TesisId, s.MuhasebeHesapPlaniId })
            .Select(g => new
            {
                g.Key.TesisId,
                g.Key.MuhasebeHesapPlaniId,
                Borc = g.Sum(x => x.Borc),
                Alacak = g.Sum(x => x.Alacak),
                SonHareket = g.Max(x => (DateTime?)x.MuhasebeFis!.FisTarihi)
            })
            .ToListAsync(cancellationToken);

        return gruplu.ToDictionary(x => (x.TesisId, x.MuhasebeHesapPlaniId), x => (x.Borc, x.Alacak, x.SonHareket));
    }

    /// <summary>Hesap/hesap-plani iliskisine dair yapisal veri kalitesi kontrollerini uygular
    /// (POS valor kayitlarindan bagimsiz - bkz. valor-bazli kontroller GetPozisyonAsync icinde).</summary>
    private static void BuildYapisalUyarilar(
        List<HesapProjeksiyon> hesaplar, Dictionary<int, HesapPlaniProjeksiyon> hesapPlaniLookup, UyariToplayici uyarilar)
    {
        foreach (var h in hesaplar.Where(x => x.Tip == KasaBankaHesapTipleri.Banka || x.Tip == KasaBankaHesapTipleri.DovizHesabi))
        {
            if (!string.IsNullOrWhiteSpace(h.Iban) && !h.MuhasebeHesapPlaniId.HasValue)
            {
                uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.IbanVarMuhasebeHesabiYok, h.Id, null, null,
                    $"'{h.Ad}' hesabının IBAN'ı tanımlı ama bağlı bir muhasebe detay hesabı yok.");
            }

            if (h.MuhasebeHesapPlaniId.HasValue && string.IsNullOrWhiteSpace(h.Iban))
            {
                uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.MuhasebeHesabiVarIbanYok, h.Id, null, null,
                    $"'{h.Ad}' hesabının muhasebe bağlantısı var ama IBAN'ı tanımlı değil.");
            }

            if (h.MuhasebeHesapPlaniId.HasValue
                && hesapPlaniLookup.TryGetValue(h.MuhasebeHesapPlaniId.Value, out var hp))
            {
                if (hp.IsDeleted)
                {
                    uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.SoftDeleteEdilmisBaglantiliMuhasebeHesabi, h.Id, null, null,
                        $"'{h.Ad}' hesabının bağlı olduğu muhasebe hesabı silinmiş (soft-delete); bakiye bu hesap üzerinden hesaplanamıyor.");
                }
                else if (!hp.AktifMi)
                {
                    uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.PasifBaglantiliMuhasebeHesabi, h.Id, null, null,
                        $"'{h.Ad}' hesabının bağlı olduğu muhasebe hesabı pasif (AktifMi=false); normal pozisyona dahil edilmedi.");
                }
            }
        }

        // Bir muhasebe hesabina birden fazla aktif banka/IBAN hesabi baglanmis mi.
        //
        // ONEMLI - ANAHTAR (TesisId, MuhasebeHesapPlaniId): MuhasebeHesapPlani.TesisId NULLABLE'dir,
        // yani bir hesap plani tesise ozel de olabilir, tesisler arasi paylasilan (TesisId=null) da.
        // Paylasilan bir hesap planinin FARKLI tesislerde birer banka hesabina baglanmasi NORMAL bir
        // kurulumdur ve mukerrerlik DEGILDIR. Gercek mukerrerlik ancak AYNI TESIS icinde ayni hesap
        // planina birden fazla aktif banka hesabi baglandiginda vardir - bu yuzden gruplama anahtari
        // banka hesabinin TesisId'sini de icerir.
        //
        // TERS YON (bir banka hesabinin birden fazla aktif muhasebe hesabina baglanmasi) semayla
        // yapisal olarak IMKANSIZDIR - KasaBankaHesap.MuhasebeHesapPlaniId TEKIL bir FK'dir. Bu iki
        // kontrol birbirinin yerine KULLANILMAZ (bkz. AyniBankaHesabiBirdenFazlaMuhasebeHesabinaBagli).
        var muhasebeHesapPlaniGruplari = hesaplar
            .Where(x => (x.Tip == KasaBankaHesapTipleri.Banka || x.Tip == KasaBankaHesapTipleri.DovizHesabi) && x.MuhasebeHesapPlaniId.HasValue)
            .GroupBy(x => (x.TesisId, HesapPlaniId: x.MuhasebeHesapPlaniId!.Value))
            .Where(g => g.Count() > 1);

        foreach (var grup in muhasebeHesapPlaniGruplari)
        {
            var adlar = string.Join(", ", grup.Select(x => x.Ad));
            foreach (var h in grup)
            {
                uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.AyniMuhasebeHesabinaBirdenFazlaAktifBankaHesabiBagli, h.Id, null, null,
                    $"Aynı tesiste (TesisId={grup.Key.TesisId}) aynı muhasebe hesabına ({grup.Key.HesapPlaniId}) birden fazla aktif banka hesabı bağlı: {adlar}.");
            }
        }
    }

    /// <summary>Ayni (UyariTipi, KasaBankaHesapId) icin birden fazla kayit varsa TEK bir ozet
    /// satirinda toplar (adet+tutar) - yuzlerce ayni-turden uyarinin listeyi bogmasi engellenir.</summary>
    private sealed class UyariToplayici
    {
        // Anahtara PARA BIRIMI de dahildir - farkli para birimlerindeki tutarlar ayni uyari
        // satirinda TOPLANMAZ (kur donusum altyapisi yok).
        private readonly Dictionary<(string Tip, int? HesapId, string ParaBirimi), VeriKalitesiUyariDto> _map = [];

        public void Ekle(string uyariTipi, int? kasaBankaHesapId, int? posTahsilatValorId, decimal? tutar, string aciklama, string? paraBirimi = null)
        {
            var pb = string.IsNullOrWhiteSpace(paraBirimi) ? "-" : paraBirimi;
            var key = (uyariTipi, kasaBankaHesapId, pb);
            if (_map.TryGetValue(key, out var mevcut))
            {
                mevcut.Adet++;
                mevcut.Tutar = (mevcut.Tutar ?? 0) + (tutar ?? 0);
                mevcut.PosTahsilatValorId = null; // birden fazla kayit oldugu icin tekil id anlamsizlasti.
                return;
            }

            _map[key] = new VeriKalitesiUyariDto
            {
                UyariTipi = uyariTipi,
                Aciklama = aciklama,
                KasaBankaHesapId = kasaBankaHesapId,
                PosTahsilatValorId = posTahsilatValorId,
                Tutar = tutar,
                ParaBirimi = pb == "-" ? null : pb,
                Adet = 1
            };
        }

        public List<VeriKalitesiUyariDto> Listele() => [.. _map.Values];
    }

    /// <summary>Para birimi etiketini normalize eder. Bos/tanimsiz deger TRY VARSAYILMAZ - ayri bir
    /// "Bilinmiyor" etiketiyle gosterilir ki hicbir gercek para birimi toplamina karismasin.</summary>
    internal const string BilinmeyenParaBirimi = "Bilinmiyor";

    private static string NormalizeParaBirimi(string? paraBirimi) =>
        string.IsNullOrWhiteSpace(paraBirimi) ? BilinmeyenParaBirimi : paraBirimi.Trim().ToUpperInvariant();

    private static DateOnly BugunIstanbul()
    {
        var istanbul = ResolveIstanbulTimeZone();
        var simdi = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istanbul);
        return DateOnly.FromDateTime(simdi);
    }

    private static TimeZoneInfo ResolveIstanbulTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
    }

    private async Task<DateOnly> ResolveRaporTarihiAsync(NakitBankaPozisyonuFilterDto filter, DateOnly bugun, CancellationToken cancellationToken)
    {
        DateOnly? donemBitisTarihi = null;
        DateOnly? donemBaslangicTarihi = null;

        if (filter.TesisId.HasValue && filter.MaliYil.HasValue && filter.Donem.HasValue)
        {
            var donem = await _dbContext.MuhasebeDonemler.AsNoTracking()
                .Where(x => !x.IsDeleted && x.TesisId == filter.TesisId.Value && x.MaliYil == filter.MaliYil.Value && x.DonemNo == filter.Donem.Value)
                .Select(x => new { x.BaslangicTarihi, x.BitisTarihi })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new BaseException("Belirtilen muhasebe dönemi bulunamadı.", 400);

            donemBaslangicTarihi = DateOnly.FromDateTime(donem.BaslangicTarihi);
            donemBitisTarihi = DateOnly.FromDateTime(donem.BitisTarihi);
        }

        DateOnly raporTarihi;
        if (filter.RaporTarihi.HasValue)
        {
            raporTarihi = filter.RaporTarihi.Value;

            // Donem VE rapor tarihi birlikte verilmisse, rapor tarihinin o donemin araligina
            // gercekten ait oldugu dogrulanir - aksi halde donem filtresi yalnizca dekoratif
            // kalir ve kullanicinin secimiyle celisen bir sonuc uretebilirdi.
            if (donemBaslangicTarihi.HasValue && donemBitisTarihi.HasValue
                && (raporTarihi < donemBaslangicTarihi.Value || raporTarihi > donemBitisTarihi.Value))
            {
                throw new BaseException(
                    $"Seçilen rapor tarihi ({raporTarihi:yyyy-MM-dd}), seçilen muhasebe döneminin ({donemBaslangicTarihi:yyyy-MM-dd} - {donemBitisTarihi:yyyy-MM-dd}) dışında.", 400);
            }
        }
        else if (donemBitisTarihi.HasValue)
        {
            raporTarihi = donemBitisTarihi.Value;
        }
        else
        {
            raporTarihi = bugun;
        }

        if (raporTarihi > bugun)
        {
            throw new BaseException("Rapor tarihi gelecekte bir tarih olamaz.", 400);
        }

        return raporTarihi;
    }

    private async Task<IReadOnlyList<int>> ResolveTesisIdsAsync(int? tesisId, CancellationToken cancellationToken)
    {
        if (tesisId.HasValue)
        {
            await _tesisScopeService.EnsureCanAccessTesisAsync(tesisId.Value, cancellationToken);
            return [tesisId.Value];
        }

        var effective = await _tesisScopeService.GetEffectiveTesisIdsAsync(cancellationToken);
        return effective;
    }
}
