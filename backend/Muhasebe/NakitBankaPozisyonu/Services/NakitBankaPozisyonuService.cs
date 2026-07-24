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

        // 3) Bakiyeleri (TesisId, MuhasebeHesapPlaniId) BILESIK anahtariyla, yalnizca soft-delete
        // edilmemis hesap planlari icin TEK gruplu sorguda hesapla - ayni HesapPlaniId'nin farkli
        // tesislerde ayri hesaplar temsil edebilecegi durumda bakiyelerin KARISMAMASI icin.
        var aktifHesapPlaniIds = hesapPlanlari.Where(x => !x.IsDeleted).Select(x => x.Id).ToList();
        var bakiyeler = await GetBakiyelerAsync(aktifHesapPlaniIds, tesisIds, raporTarihi, cancellationToken);

        // 4) Rapor tarihi itibariyla var olan (OdemeTarihi rapor tarihinden SONRAKI degil) tum POS
        // valor kayitlarini, bagli oldugu muhasebe fisinin (varsa) FisTarihi'yle BIRLIKTE cek. Bu
        // sorgu KASITLI olarak ValorDurumu filtresine gore DARALTILMAZ (bu filtre yalnizca detay
        // sorgularini etkiler, bkz. DTO doc) ve KASITLI olarak yalnizca "bekleyen" durumlarla
        // sinirlandirilmaz (gecmis tarih raporlamasi icin Aktarildi/Iptal gecmisi de gereklidir).
        var ustSinirRapor = raporTarihi.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var valorKayitlari = await (
            from v in _dbContext.PosTahsilatValorleri.AsNoTracking()
            where !v.IsDeleted && tesisIds.Contains(v.TesisId) && v.OdemeTarihi < ustSinirRapor
            join f in _dbContext.MuhasebeFisler.AsNoTracking() on v.MuhasebeFisId equals (int?)f.Id into fj
            from f in fj.DefaultIfEmpty()
            select new ValorProjeksiyon(
                v.Id, v.BagliBankaHesapId, v.Durum, v.BeklenenValorTarihi, v.BrutTutar, v.KomisyonTutari, v.NetTutar,
                v.ParaBirimi, v.MuhasebeFisId, f != null ? (DateTime?)f.FisTarihi : null, v.UpdatedAt))
            .ToListAsync(cancellationToken);

        var uyarilar = new UyariToplayici();

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
                ParaBirimi = h.ParaBirimi ?? "TRY",
                MuhasebeHesapPlaniId = h.MuhasebeHesapPlaniId,
                MuhasebeHesapKodu = hesapPlani?.TamKod,
                MuhasebeHesapAdi = hesapPlani?.Ad,
                MuhasebeBakiyesi = bakiye.Borc - bakiye.Alacak,
                SonHareketTarihi = bakiye.SonHareket
            });
        }

        // 6) Banka/IBAN satirlari - her biri icin KENDI BagliBankaHesapId'sine esleşen valor
        //    kayitlari, ONCE uygunluk/veri-kalitesi kontrolunden gecirilip UYGUN olanlar TEK bir
        //    if/else if zinciriyle KESIN OLARAK bir bucket'a girer (mukerrer sayim yok); uygun
        //    OLMAYANLAR hicbir toplama girmeden yalnizca uyari listesine eklenir.
        foreach (var h in hesaplar.Where(x => x.Tip == KasaBankaHesapTipleri.Banka || x.Tip == KasaBankaHesapTipleri.DovizHesabi))
        {
            var hesapPlani = h.MuhasebeHesapPlaniId.HasValue ? hesapPlaniLookup.GetValueOrDefault(h.MuhasebeHesapPlaniId.Value) : null;
            var bakiye = h.MuhasebeHesapPlaniId.HasValue ? bakiyeler.GetValueOrDefault((h.TesisId, h.MuhasebeHesapPlaniId.Value)) : default;

            var dto = new BankaHesapPozisyonuDto
            {
                KasaBankaHesapId = h.Id,
                TesisId = h.TesisId,
                BankaAdi = h.BankaAdi ?? string.Empty,
                HesapAdi = h.Ad,
                Iban = h.Iban,
                ParaBirimi = h.ParaBirimi ?? "TRY",
                MuhasebeHesapPlaniId = h.MuhasebeHesapPlaniId,
                MuhasebeHesapKodu = hesapPlani?.TamKod,
                StysMuhasebeBakiyesi = bakiye.Borc - bakiye.Alacak,
                SonMuhasebeHareketTarihi = bakiye.SonHareket
            };

            foreach (var v in valorKayitlari.Where(v => v.BagliBankaHesapId == h.Id))
            {
                UygulaValorKaydi(v, h, dto, raporTarihi, gecmisTarihRaporuMu, uyarilar);
            }

            dto.ToplamBekleyenNet = dto.ValoruGecmisBekleyenNet + dto.BugunGelecekNet + dto.YarinGelecekNet
                + dto.Takip2_7GunGelecekNet + dto.Sonraki7GundenSonraNet;
            dto.TahminiBakiye = dto.StysMuhasebeBakiyesi + dto.ToplamBekleyenNet;

            sonuc.BankaHesaplari.Add(dto);
        }

        // 7) BagliBankaHesapId dolu ama bu tesis kapsaminda hicbir aktif banka hesabina karsilik
        //    GELMEYEN (yok/pasif/silinmis) veya para birimi tanimsiz kayitlar - hesaplar dongusune
        //    hic girmediginden burada AYRICA tespit edilir (aksi halde sessizce kaybolurlardi).
        foreach (var v in valorKayitlari.Where(v => v.BagliBankaHesapId.HasValue && !tumBankaHesapKimlikleri.ContainsKey(v.BagliBankaHesapId!.Value)))
        {
            uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.BankaHesabiBulunamadiVeyaPasif, v.BagliBankaHesapId, v.Id, v.NetTutar,
                "POS valör kaydının bağlı olduğu banka hesabı bulunamadı, silinmiş veya pasif.");
        }

        // 8) Hedef banka hesabi tanimsiz (BagliBankaHesapId NULL) kayitlar.
        foreach (var v in valorKayitlari.Where(v => !v.BagliBankaHesapId.HasValue))
        {
            uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.PosValorHedefBankaBelirlenemiyor, null, v.Id, v.NetTutar,
                "POS valör kaydının hedef banka hesabı (BagliBankaHesapId) belirlenemiyor.");
        }

        BuildYapisalUyarilar(hesaplar, hesapPlaniLookup, uyarilar);

        sonuc.Uyarilar = uyarilar.Listele();
        sonuc.Ozet = BuildOzet(sonuc, gecmisTarihRaporuMu);

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
        int Id, int? BagliBankaHesapId, string Durum, DateOnly BeklenenValorTarihi, decimal BrutTutar,
        decimal KomisyonTutari, decimal NetTutar, string ParaBirimi, int? MuhasebeFisId, DateTime? FisTarihi, DateTime? UpdatedAt);

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
    /// Sirasiyla: (a) zaten rapor tarihi itibariyla aktarilmis mi (bagli fisin FisTarihi'nden
    /// turetilir - varsa artik "bekleyen" DEGILDIR, muhasebe bakiyesi bunu zaten GetBakiyelerAsync
    /// araciligiyla AYNI FisTarihi kriteriyle icerir, bu yuzden iki bilesen ayni zaman esasini
    /// kullanir), (b) veri kalitesi kontrolleri (uygun degilse EKLENMEZ, yalnizca uyari), (c) rapor
    /// tarihi BUGUN ise guncel Durum'a, GECMISTE ise yalnizca tarih bucket'ina gore siniflandirma.</summary>
    private static void UygulaValorKaydi(
        ValorProjeksiyon v, HesapProjeksiyon h, BankaHesapPozisyonuDto dto, DateOnly raporTarihi, bool gecmisTarihRaporuMu, UyariToplayici uyarilar)
    {
        // Iptal edilmis kayitlarin ele alinisi: eger orijinal aktarim rapor tarihinden ONCE
        // gerceklesmisse (transferredAsOf), bu zaten normal "aktarilmis, artik bekleyen degil"
        // dalina duser (asagida) - sonraki iptal/ters kayit muhasebe bakiyesini kendi FisTarihi
        // kriteriyle zaten dogru sekilde etkiler, burada ekstra islem gerekmez. Aktarim hic
        // gerceklesmediyse (MuhasebeFisId yok) iptalin rapor tarihinden once mi sonra mi
        // gerceklestigi guvenilir sekilde bilinemez (IptalTarihi alani yok) - UpdatedAt best-effort
        // sinyal olarak kullanilir, temkinli davranilir.
        var transferredAsOf = v.MuhasebeFisId.HasValue && v.FisTarihi.HasValue && v.FisTarihi.Value < raporTarihi.AddDays(1).ToDateTime(TimeOnly.MinValue);

        if (v.Durum == PosTahsilatValorDurumlari.Iptal && !transferredAsOf)
        {
            var iptalRaporTarihindenOnceMi = v.UpdatedAt.HasValue && DateOnly.FromDateTime(v.UpdatedAt.Value) <= raporTarihi;
            if (iptalRaporTarihindenOnceMi)
            {
                return; // Rapor tarihinden once iptal edilmis (makul guvenle) - bekleyen tutara girmez, uyari gerekmez.
            }

            uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.GecmisTarihIcinIptalZamanlamasiBelirsiz, h.Id, v.Id, v.NetTutar,
                "Kayıt iptal edilmiş ancak iptalin rapor tarihinden önce mi sonra mı gerçekleştiği güvenilir şekilde belirlenemiyor; temkinli olarak bekleyen tutara dahil edildi.");
            // Temkinli yaklasim: iptal SONRASI ise (rapor tarihinde henuz gecerliydi) asagidaki
            // tarih bucket'ina girmesi dogrudur - devam edilir (return YOK).
        }
        else if (transferredAsOf)
        {
            return; // Rapor tarihi itibariyla zaten aktarilmis - bekleyen tutara GIRMEZ (rule d).
        }

        // Veri kalitesi kontrolleri - herhangi biri basarisizsa kayit HICBIR toplama eklenmeden
        // yalnizca uyari olarak raporlanir.
        if (v.BeklenenValorTarihi == default)
        {
            uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.ValorTarihiBos, h.Id, v.Id, v.NetTutar, "Beklenen valör tarihi tanımlı değil.");
            return;
        }

        if (v.BrutTutar > 0 && v.NetTutar == 0)
        {
            uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.NetVeyaKomisyonBilgisiEksik, h.Id, v.Id, v.BrutTutar,
                "Brüt tutar sıfırdan büyük olduğu halde net tutar sıfır görünüyor.");
            return;
        }

        if (!string.Equals(v.ParaBirimi, h.ParaBirimi, StringComparison.OrdinalIgnoreCase))
        {
            uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.ParaBirimiUyusmuyor, h.Id, v.Id, v.NetTutar,
                $"Kaydın para birimi ({v.ParaBirimi}) bağlı olduğu banka hesabının para biriminden ({h.ParaBirimi}) farklı.");
            return;
        }

        // Not: bir kayit MuhasebeFisId tasiyor ama transferredAsOf==false ise, bu yalnizca bagli
        // fisin FisTarihi'nin rapor tarihinden SONRA oldugu (yani "rapor tarihinde henuz
        // aktarilmamisti") anlamina gelir - gecmis tarihli raporlar icin BEKLENEN ve DOGRU bir
        // durumdur, ayrica bir uyari gerektirmez.

        // Durum=Aktarildi, tanim geregi bir MuhasebeFisId tasimasi GEREKIR (aktarim = fis
        // olusturulmasi ile es zamanlidir) - bunun eksik oldugu bir kayit veri tutarsizligidir,
        // guvenli tarafta kalinarak bekleyen toplama EKLENMEZ (aktarilmis oldugu iddia edildigi
        // icin), yalnizca uyari olarak raporlanir.
        if (v.Durum == PosTahsilatValorDurumlari.Aktarildi && !v.MuhasebeFisId.HasValue)
        {
            uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.AktarimDurumuFisIliskisiTutarsiz, h.Id, v.Id, v.NetTutar,
                "Kayıt 'Aktarıldı' durumunda ancak bağlı bir muhasebe fişi (MuhasebeFisId) bulunamıyor.");
            return;
        }

        // AktarimFisiIptalEdildi: "duzeltme-ters-kayit" akisiyla zaten sonuclandirilmis (ters kayit
        // olusturulmus) bir kayit - bu ekranin salt-okunur bekleyen/hatali toplamlarina bugun icin
        // dahil EDILMEZ (asagida gecmis rapor icin ayrica ele alinir, bkz. GecmisTarihIcinDurumBelirsiz).
        if (!gecmisTarihRaporuMu && v.Durum == PosTahsilatValorDurumlari.AktarimFisiIptalEdildi)
        {
            return;
        }

        if (gecmisTarihRaporuMu && v.Durum != PosTahsilatValorDurumlari.ValorBekliyor && v.Durum != PosTahsilatValorDurumlari.Iptal)
        {
            uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.GecmisTarihIcinDurumBelirsiz, h.Id, v.Id, v.NetTutar,
                $"Bu kaydın güncel durumu '{v.Durum}' ancak {raporTarihi:yyyy-MM-dd} tarihindeki gerçek durumu (Bekliyor/Mutabakat/Hata ayrımı) sistemde zaman damgalı olarak tutulmadığından güvenilir şekilde yeniden oluşturulamıyor; tutar bekleyen toplama dahil edildi.");
        }

        if (!gecmisTarihRaporuMu && v.Durum == PosTahsilatValorDurumlari.MutabakatBekliyor)
        {
            dto.MutabakatBekleyenNet += v.NetTutar;
            dto.MutabakatBekleyenAdet++;
            return;
        }

        if (!gecmisTarihRaporuMu && v.Durum == PosTahsilatValorDurumlari.Hata)
        {
            dto.HataliNet += v.NetTutar;
            dto.HataliAdet++;
            return;
        }

        // Kalan tum durumlar (ValorBekliyor her zaman; gecmis rapor icin ayrica Mutabakat/Hata/
        // Aktariliyor/TersKayitOlusturuluyor/AktarimFisiIptalEdildi de buraya) tarih bucket'ina gore
        // KESIN OLARAK bir gruba girer.
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

    private static NakitBankaPozisyonuOzetDto BuildOzet(NakitBankaPozisyonuDto sonuc, bool gecmisTarihRaporuMu)
    {
        // ONEMLI: genel ozet kartlari yalnizca RAPORLAMA (TRY) para birimindeki hesaplarin
        // toplamini yansitir - farkli para birimindeki hesaplar buraya KARISTIRILMAZ (kur donusum
        // altyapisi projede yok, bkz. ParaBirimiOzetleri asagida HER para birimi icin ayri ayri
        // hesaplanir).
        var tryKasa = sonuc.KasaHesaplari.Where(x => string.Equals(x.ParaBirimi, RaporlamaParaBirimi, StringComparison.OrdinalIgnoreCase)).ToList();
        var tryBanka = sonuc.BankaHesaplari.Where(x => string.Equals(x.ParaBirimi, RaporlamaParaBirimi, StringComparison.OrdinalIgnoreCase)).ToList();

        var ozet = new NakitBankaPozisyonuOzetDto
        {
            RaporTarihi = sonuc.RaporTarihi,
            GecmisTarihRaporuMu = gecmisTarihRaporuMu,
            ToplamNakit = tryKasa.Sum(x => x.MuhasebeBakiyesi),
            ToplamBankaMuhasebeBakiyesi = tryBanka.Sum(x => x.StysMuhasebeBakiyesi),
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

        var paraBirimleri = sonuc.KasaHesaplari.Select(x => x.ParaBirimi)
            .Concat(sonuc.BankaHesaplari.Select(x => x.ParaBirimi))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var pb in paraBirimleri)
        {
            var nakit = sonuc.KasaHesaplari.Where(x => string.Equals(x.ParaBirimi, pb, StringComparison.OrdinalIgnoreCase)).Sum(x => x.MuhasebeBakiyesi);
            var banka = sonuc.BankaHesaplari.Where(x => string.Equals(x.ParaBirimi, pb, StringComparison.OrdinalIgnoreCase)).Sum(x => x.StysMuhasebeBakiyesi);
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
                && hesapPlaniLookup.TryGetValue(h.MuhasebeHesapPlaniId.Value, out var hp)
                && hp.IsDeleted)
            {
                uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.SoftDeleteEdilmisBaglantiliMuhasebeHesabi, h.Id, null, null,
                    $"'{h.Ad}' hesabının bağlı olduğu muhasebe hesabı silinmiş (soft-delete); bakiye bu hesap üzerinden hesaplanamıyor.");
            }
        }

        // Bir muhasebe hesabina (tek MuhasebeHesapPlaniId) birden fazla aktif banka hesabi baglanmis
        // mi - KasaBankaHesap.MuhasebeHesapPlaniId tekil bir FK oldugu icin TERSI (bir banka
        // hesabinin birden fazla muhasebe hesabina baglanmasi) semayla imkansizdir.
        var muhasebeHesapPlaniGruplari = hesaplar
            .Where(x => (x.Tip == KasaBankaHesapTipleri.Banka || x.Tip == KasaBankaHesapTipleri.DovizHesabi) && x.MuhasebeHesapPlaniId.HasValue)
            .GroupBy(x => x.MuhasebeHesapPlaniId!.Value)
            .Where(g => g.Count() > 1);

        foreach (var grup in muhasebeHesapPlaniGruplari)
        {
            var adlar = string.Join(", ", grup.Select(x => x.Ad));
            foreach (var h in grup)
            {
                uyarilar.Ekle(NakitBankaPozisyonuUyariTipleri.AyniMuhasebeHesabinaBirdenFazlaAktifBankaHesabiBagli, h.Id, null, null,
                    $"Aynı muhasebe hesabına ({grup.Key}) birden fazla aktif banka hesabı bağlı: {adlar}.");
            }
        }
    }

    /// <summary>Ayni (UyariTipi, KasaBankaHesapId) icin birden fazla kayit varsa TEK bir ozet
    /// satirinda toplar (adet+tutar) - yuzlerce ayni-turden uyarinin listeyi bogmasi engellenir.</summary>
    private sealed class UyariToplayici
    {
        private readonly Dictionary<(string Tip, int? HesapId), VeriKalitesiUyariDto> _map = [];

        public void Ekle(string uyariTipi, int? kasaBankaHesapId, int? posTahsilatValorId, decimal? tutar, string aciklama)
        {
            var key = (uyariTipi, kasaBankaHesapId);
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
                Adet = 1
            };
        }

        public List<VeriKalitesiUyariDto> Listele() => [.. _map.Values];
    }

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
