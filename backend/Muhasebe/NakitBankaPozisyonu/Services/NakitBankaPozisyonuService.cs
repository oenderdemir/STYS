using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.NakitBankaPozisyonu.Dtos;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.NakitBankaPozisyonu.Services;

public class NakitBankaPozisyonuService : INakitBankaPozisyonuService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeTesisScopeService _tesisScopeService;

    public NakitBankaPozisyonuService(StysAppDbContext dbContext, IMuhasebeTesisScopeService tesisScopeService)
    {
        _dbContext = dbContext;
        _tesisScopeService = tesisScopeService;
    }

    public async Task<NakitBankaPozisyonuOzetDto> GetOzetAsync(NakitBankaPozisyonuFilterDto filter, CancellationToken cancellationToken = default)
    {
        var hesaplar = await GetHesaplarAsync(filter, cancellationToken);

        // ONEMLI: genel ozet kartlari yalnizca RAPORLAMA (TRY) para birimindeki hesaplarin
        // toplamini yansitir - farkli para birimindeki hesaplar buraya KARISTIRILMAZ (kur donusum
        // altyapisi projede yok, bkz. ParaBirimiOzetleri asagida HER para birimi icin ayri ayri
        // hesaplanir). TRY disi para birimine sahip hesaplar yalnizca ParaBirimiOzetleri altinda
        // GORULEBILIR - genel kartlarda SESSIZCE toplanmaz/kaybolmaz.
        const string raporlamaParaBirimi = "TRY";
        var tryKasa = hesaplar.KasaHesaplari.Where(x => string.Equals(x.ParaBirimi, raporlamaParaBirimi, StringComparison.OrdinalIgnoreCase)).ToList();
        var tryBanka = hesaplar.BankaHesaplari.Where(x => string.Equals(x.ParaBirimi, raporlamaParaBirimi, StringComparison.OrdinalIgnoreCase)).ToList();

        var ozet = new NakitBankaPozisyonuOzetDto
        {
            RaporTarihi = hesaplar.RaporTarihi,
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
            UyariSayisi = hesaplar.Uyarilar.Count
        };
        ozet.TahminiToplamBankaPozisyonu = ozet.ToplamBankaMuhasebeBakiyesi + ozet.ToplamBekleyenNetPos;

        // Para birimine gore ayri toplamlar - farkli para birimleri DOGRUDAN TOPLANMAZ (kur
        // donusum altyapisi projede yok). TRY dahil TUM para birimleri burada eksiksiz gorulur.
        var paraBirimleri = hesaplar.KasaHesaplari.Select(x => x.ParaBirimi)
            .Concat(hesaplar.BankaHesaplari.Select(x => x.ParaBirimi))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var pb in paraBirimleri)
        {
            var nakit = hesaplar.KasaHesaplari.Where(x => string.Equals(x.ParaBirimi, pb, StringComparison.OrdinalIgnoreCase)).Sum(x => x.MuhasebeBakiyesi);
            var banka = hesaplar.BankaHesaplari.Where(x => string.Equals(x.ParaBirimi, pb, StringComparison.OrdinalIgnoreCase)).Sum(x => x.StysMuhasebeBakiyesi);
            var bekleyen = hesaplar.BankaHesaplari.Where(x => string.Equals(x.ParaBirimi, pb, StringComparison.OrdinalIgnoreCase)).Sum(x => x.ToplamBekleyenNet);

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

    public async Task<NakitBankaPozisyonuHesaplarDto> GetHesaplarAsync(NakitBankaPozisyonuFilterDto filter, CancellationToken cancellationToken = default)
    {
        var raporTarihi = await ResolveRaporTarihiAsync(filter, cancellationToken);
        var tesisIds = await ResolveTesisIdsAsync(filter.TesisId, cancellationToken);

        var sonuc = new NakitBankaPozisyonuHesaplarDto { RaporTarihi = raporTarihi };
        if (tesisIds.Count == 0)
        {
            return sonuc;
        }

        // 1) Aktif kasa/banka hesaplarini (KrediKarti HARIC - POS pozisyonu ayri, "bekleyen valor"
        //    uzerinden zaten yansitiliyor) filtreli olarak cek.
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
            .Select(x => new HesapProjeksiyon(x.Id, x.Tip, x.Ad, x.Kod, x.ParaBirimi, x.MuhasebeHesapPlaniId, x.BankaAdi, x.Iban))
            .ToListAsync(cancellationToken);

        // 2) Bagli muhasebe hesap plani bilgisi (Kod/Ad/aktiflik/soft-delete) - TEK sorgu.
        // IgnoreQueryFilters() KASITLI: soft-delete edilmis bir baglanti (uyari #8) yalnizca boyle
        // tespit edilebilir - normal (filtreli) sorgu bu satiri sessizce YOK SAYARDI.
        var hesapPlaniIds = hesaplar.Where(h => h.MuhasebeHesapPlaniId.HasValue).Select(h => h.MuhasebeHesapPlaniId!.Value).Distinct().ToList();
        var hesapPlanlari = hesapPlaniIds.Count == 0
            ? []
            : await _dbContext.MuhasebeHesapPlanlari.IgnoreQueryFilters().AsNoTracking()
                .Where(x => hesapPlaniIds.Contains(x.Id))
                .Select(x => new HesapPlaniProjeksiyon(x.Id, x.TamKod, x.Ad, x.IsDeleted, x.AktifMi))
                .ToListAsync(cancellationToken);
        var hesapPlaniLookup = hesapPlanlari.ToDictionary(x => x.Id);

        // 3) Bakiyeleri (yalnizca soft-delete edilmemis hesap planlari icin) TEK gruplu sorguda hesapla.
        var aktifHesapPlaniIds = hesapPlanlari.Where(x => !x.IsDeleted).Select(x => x.Id).ToList();
        var bakiyeler = await GetBakiyelerAsync(aktifHesapPlaniIds, tesisIds, raporTarihi, cancellationToken);

        // 4) Bekleyen/mutabakat bekleyen/hatali POS valor kayitlarini cek - yalnizca bu 3 durum;
        //    aktarilmis/iptal edilmis kayitlar burada YOKTUR (mukerrer sayimi engeller). Filtre
        //    acikca bir Durum sectiyse (ValorDurumu), sonuc bu tekile daraltilir - aksi halde
        //    3'u de dahil edilir.
        var valorDurumFiltresi = string.IsNullOrWhiteSpace(filter.ValorDurumu) ? null : filter.ValorDurumu.Trim();
        var valorQuery = _dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(v => !v.IsDeleted && tesisIds.Contains(v.TesisId)
                && (v.Durum == PosTahsilatValorDurumlari.ValorBekliyor
                    || v.Durum == PosTahsilatValorDurumlari.MutabakatBekliyor
                    || v.Durum == PosTahsilatValorDurumlari.Hata));

        if (valorDurumFiltresi is not null)
        {
            valorQuery = valorQuery.Where(v => v.Durum == valorDurumFiltresi);
        }

        var valorKayitlari = await valorQuery
            .Select(v => new ValorProjeksiyon(
                v.Id, v.BagliBankaHesapId, v.Durum, v.BeklenenValorTarihi, v.BrutTutar, v.KomisyonTutari, v.NetTutar,
                v.ParaBirimi, v.MuhasebeFisId, v.TersKayitMuhasebeFisId))
            .ToListAsync(cancellationToken);

        // 5) Kasa satirlari.
        foreach (var h in hesaplar.Where(x => x.Tip == KasaBankaHesapTipleri.NakitKasa))
        {
            var hesapPlani = h.MuhasebeHesapPlaniId.HasValue ? hesapPlaniLookup.GetValueOrDefault(h.MuhasebeHesapPlaniId.Value) : null;
            var bakiye = h.MuhasebeHesapPlaniId.HasValue ? bakiyeler.GetValueOrDefault(h.MuhasebeHesapPlaniId.Value) : default;

            sonuc.KasaHesaplari.Add(new NakitHesapPozisyonuDto
            {
                KasaBankaHesapId = h.Id,
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

        // 6) Banka/IBAN satirlari - her biri icin KENDI BagliBankaHesapId'sine esleşen bekleyen
        //    POS kayitlari bucket'lanir (ayni valor kaydi ASLA iki bankaya/iki tarih grubuna
        //    birden eklenmez - tek bir if/else if zinciriyle KESIN OLARAK bir bucket'a girer).
        foreach (var h in hesaplar.Where(x => x.Tip == KasaBankaHesapTipleri.Banka || x.Tip == KasaBankaHesapTipleri.DovizHesabi))
        {
            var hesapPlani = h.MuhasebeHesapPlaniId.HasValue ? hesapPlaniLookup.GetValueOrDefault(h.MuhasebeHesapPlaniId.Value) : null;
            var bakiye = h.MuhasebeHesapPlaniId.HasValue ? bakiyeler.GetValueOrDefault(h.MuhasebeHesapPlaniId.Value) : default;

            var buHesabaAitValorler = valorKayitlari.Where(v => v.BagliBankaHesapId == h.Id).ToList();

            var dto = new BankaHesapPozisyonuDto
            {
                KasaBankaHesapId = h.Id,
                BankaAdi = h.BankaAdi ?? string.Empty,
                HesapAdi = h.Ad,
                Iban = h.Iban,
                ParaBirimi = h.ParaBirimi ?? "TRY",
                MuhasebeHesapPlaniId = h.MuhasebeHesapPlaniId,
                MuhasebeHesapKodu = hesapPlani?.TamKod,
                StysMuhasebeBakiyesi = bakiye.Borc - bakiye.Alacak,
                SonMuhasebeHareketTarihi = bakiye.SonHareket
            };

            foreach (var v in buHesabaAitValorler)
            {
                if (v.Durum == PosTahsilatValorDurumlari.MutabakatBekliyor)
                {
                    dto.MutabakatBekleyenNet += v.NetTutar;
                    dto.MutabakatBekleyenAdet++;
                    continue;
                }

                if (v.Durum == PosTahsilatValorDurumlari.Hata)
                {
                    dto.HataliNet += v.NetTutar;
                    dto.HataliAdet++;
                    continue;
                }

                // Durum == ValorBekliyor: tarih bucket'ina gore KESIN OLARAK bir grubun icine girer.
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

            dto.ToplamBekleyenNet = dto.ValoruGecmisBekleyenNet + dto.BugunGelecekNet + dto.YarinGelecekNet
                + dto.Takip2_7GunGelecekNet + dto.Sonraki7GundenSonraNet;
            dto.TahminiBakiye = dto.StysMuhasebeBakiyesi + dto.ToplamBekleyenNet;

            sonuc.BankaHesaplari.Add(dto);
        }

        sonuc.Uyarilar = BuildUyarilar(hesaplar, hesapPlaniLookup, valorKayitlari);

        return sonuc;
    }

    public async Task<BankaValorTakvimiDto> GetValorTakvimiAsync(int kasaBankaHesapId, DateOnly? raporTarihi, CancellationToken cancellationToken = default)
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

        await _tesisScopeService.EnsureCanAccessTesisAsync(
            hesap.TesisId ?? throw new BaseException("Hesabın tesisi belirlenemedi.", 400), cancellationToken);

        var etkinRaporTarihi = raporTarihi ?? BugunIstanbul();

        var kayitlar = await _dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(v => !v.IsDeleted && v.BagliBankaHesapId == kasaBankaHesapId && v.Durum == PosTahsilatValorDurumlari.ValorBekliyor)
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

        // Belge no / kredi karti hesap adi icin ayri, hafif projeksiyon sorgulari (Include YOK).
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

        var gunler = kayitlar
            .GroupBy(x => x.BeklenenValorTarihi)
            .OrderBy(g => g.Key)
            .Select(g => new GunlukValorOzetiDto
            {
                ValorTarihi = g.Key,
                IslemSayisi = g.Count(),
                BrutTutar = g.Sum(x => x.BrutTutar),
                KomisyonTutari = g.Sum(x => x.KomisyonTutari),
                NetTutar = g.Sum(x => x.NetTutar),
                Detaylar = g.OrderBy(x => x.Id).Select(x => new ValorDetayDto
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
                }).ToList()
            })
            .ToList();

        return new BankaValorTakvimiDto
        {
            KasaBankaHesapId = kasaBankaHesapId,
            RaporTarihi = etkinRaporTarihi,
            Gunler = gunler
        };
    }

    // ─────────────────────────────────────────────────────────────
    // Projeksiyon tipleri
    // ─────────────────────────────────────────────────────────────

    private sealed record HesapProjeksiyon(int Id, string Tip, string Ad, string Kod, string? ParaBirimi, int? MuhasebeHesapPlaniId, string? BankaAdi, string? Iban);

    private sealed record HesapPlaniProjeksiyon(int Id, string TamKod, string Ad, bool IsDeleted, bool AktifMi);

    private sealed record ValorProjeksiyon(
        int Id, int? BagliBankaHesapId, string Durum, DateOnly BeklenenValorTarihi, decimal BrutTutar,
        decimal KomisyonTutari, decimal NetTutar, string ParaBirimi, int? MuhasebeFisId, int? TersKayitMuhasebeFisId);

    // ─────────────────────────────────────────────────────────────
    // Yardimcilar
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// MuhasebeHesapBakiyeleri tablosu yalnizca (TesisId, MaliYil, Donem) bazinda o DONEME AIT
    /// HAREKET TOPLAMINI tutar (bkz. MuhasebeHesapBakiyeGuncellemeService/RebuildAsync - her ikisi
    /// de yalnizca ILGILI DONEMIN fis satirlarini toplar; onceki donemlerden devreden bir acilis
    /// bakiyesi/kumulatif toplam KAVRAMI YOKTUR). Bu nedenle "rapor tarihi itibariyla bakiye"
    /// ISTENEN kumulatif bir deger oldugu icin bu tablo BURADA KULLANILMAZ - aksi halde secilen
    /// rapor tarihi bugunden farkli (ozellikle donem ici bir tarih) oldugunda YANLIS (yalnizca o
    /// tek donemin hareketi kadar) bir "bakiye" gosterilmis olurdu. Bunun yerine, Hizli Mizan'in
    /// canli hesaplama yolunun (GetMizanAsync) AYNI kuralini (yalnizca Onayli+TersKayit fisler,
    /// FisTarihi &lt;= rapor tarihi) izleyerek MuhasebeFisSatirlari'ndan DOGRUDAN, TEK bir gruplu
    /// SQL sorgusuyla (yalnizca ilgili hesap planlari + tesisler icin, N+1 YOK, gereksiz Include
    /// YOK) kumulatif bakiye hesaplanir.
    /// </summary>
    private async Task<Dictionary<int, (decimal Borc, decimal Alacak, DateTime? SonHareket)>> GetBakiyelerAsync(
        IReadOnlyCollection<int> hesapPlaniIds, IReadOnlyCollection<int> tesisIds, DateOnly raporTarihi, CancellationToken cancellationToken)
    {
        if (hesapPlaniIds.Count == 0)
        {
            return [];
        }

        // Rapor tarihinin TAMAMINI (gun sonuna kadar) kapsamasi icin ertesi gunun basindan
        // KUCUK (strict less-than) karsilastirilir - FisTarihi'nin saat bilesenine bagimli
        // olunmaz.
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
            .GroupBy(s => s.MuhasebeHesapPlaniId)
            .Select(g => new
            {
                MuhasebeHesapPlaniId = g.Key,
                Borc = g.Sum(x => x.Borc),
                Alacak = g.Sum(x => x.Alacak),
                SonHareket = g.Max(x => (DateTime?)x.MuhasebeFis!.FisTarihi)
            })
            .ToListAsync(cancellationToken);

        return gruplu.ToDictionary(x => x.MuhasebeHesapPlaniId, x => (x.Borc, x.Alacak, x.SonHareket));
    }

    /// <summary>Bolum 9'daki 8 veri kalitesi kuralini uygular. Bu kayitlar normal tahmini
    /// bakiyeye/toplamlara HICBIR ZAMAN otomatik eklenmez - yalnizca ayri bir uyari listesi olarak
    /// dondurulur.</summary>
    private static List<VeriKalitesiUyariDto> BuildUyarilar(
        List<HesapProjeksiyon> hesaplar,
        Dictionary<int, HesapPlaniProjeksiyon> hesapPlaniLookup,
        List<ValorProjeksiyon> valorKayitlari)
    {
        var uyarilar = new List<VeriKalitesiUyariDto>();

        foreach (var h in hesaplar.Where(x => x.Tip == KasaBankaHesapTipleri.Banka || x.Tip == KasaBankaHesapTipleri.DovizHesabi))
        {
            // 1) IBAN tanimli fakat bagli muhasebe detay hesabi yok.
            if (!string.IsNullOrWhiteSpace(h.Iban) && !h.MuhasebeHesapPlaniId.HasValue)
            {
                uyarilar.Add(new VeriKalitesiUyariDto
                {
                    UyariTipi = NakitBankaPozisyonuUyariTipleri.IbanVarMuhasebeHesabiYok,
                    Aciklama = $"'{h.Ad}' hesabının IBAN'ı tanımlı ama bağlı bir muhasebe detay hesabı yok.",
                    KasaBankaHesapId = h.Id
                });
            }

            // 2) Muhasebe hesabi var fakat IBAN/banka hesabi baglantisi yok.
            if (h.MuhasebeHesapPlaniId.HasValue && string.IsNullOrWhiteSpace(h.Iban))
            {
                uyarilar.Add(new VeriKalitesiUyariDto
                {
                    UyariTipi = NakitBankaPozisyonuUyariTipleri.MuhasebeHesabiVarIbanYok,
                    Aciklama = $"'{h.Ad}' hesabının muhasebe bağlantısı var ama IBAN'ı tanımlı değil.",
                    KasaBankaHesapId = h.Id
                });
            }

            // 8) Soft-delete edilmis bagli muhasebe hesabi.
            if (h.MuhasebeHesapPlaniId.HasValue
                && hesapPlaniLookup.TryGetValue(h.MuhasebeHesapPlaniId.Value, out var hp)
                && hp.IsDeleted)
            {
                uyarilar.Add(new VeriKalitesiUyariDto
                {
                    UyariTipi = NakitBankaPozisyonuUyariTipleri.SoftDeleteEdilmisBaglantiliMuhasebeHesabi,
                    Aciklama = $"'{h.Ad}' hesabının bağlı olduğu muhasebe hesabı silinmiş (soft-delete); bakiye bu hesap üzerinden hesaplanamıyor.",
                    KasaBankaHesapId = h.Id
                });
            }
        }

        // 7) Ayni banka hesabina bagli birden fazla aktif muhasebe hesabi.
        var muhasebeHesapPlaniGruplari = hesaplar
            .Where(x => (x.Tip == KasaBankaHesapTipleri.Banka || x.Tip == KasaBankaHesapTipleri.DovizHesabi) && x.MuhasebeHesapPlaniId.HasValue)
            .GroupBy(x => x.MuhasebeHesapPlaniId!.Value)
            .Where(g => g.Count() > 1);

        foreach (var grup in muhasebeHesapPlaniGruplari)
        {
            var adlar = string.Join(", ", grup.Select(x => x.Ad));
            foreach (var h in grup)
            {
                uyarilar.Add(new VeriKalitesiUyariDto
                {
                    UyariTipi = NakitBankaPozisyonuUyariTipleri.AyniBankaHesabinaBirdenFazlaAktifMuhasebeHesabi,
                    Aciklama = $"Aynı muhasebe hesabına ({grup.Key}) birden fazla aktif banka hesabı bağlı: {adlar}.",
                    KasaBankaHesapId = h.Id
                });
            }
        }

        foreach (var v in valorKayitlari)
        {
            // 3) POS valor kaydinin hedef banka hesabi belirlenemiyor.
            if (!v.BagliBankaHesapId.HasValue)
            {
                uyarilar.Add(new VeriKalitesiUyariDto
                {
                    UyariTipi = NakitBankaPozisyonuUyariTipleri.PosValorHedefBankaBelirlenemiyor,
                    Aciklama = "POS valör kaydının hedef banka hesabı (BagliBankaHesapId) belirlenemiyor.",
                    PosTahsilatValorId = v.Id,
                    Tutar = v.NetTutar
                });
            }

            // 4) Net tutar veya komisyon bilgisi eksik (brut var ama net sifir - suphe uyandiran
            // bir kayit).
            if (v.BrutTutar > 0 && v.NetTutar == 0)
            {
                uyarilar.Add(new VeriKalitesiUyariDto
                {
                    UyariTipi = NakitBankaPozisyonuUyariTipleri.NetVeyaKomisyonBilgisiEksik,
                    Aciklama = "Brüt tutar sıfırdan büyük olduğu halde net tutar sıfır görünüyor.",
                    PosTahsilatValorId = v.Id,
                    Tutar = v.BrutTutar
                });
            }

            // 5) Valor tarihi bos (varsayilan/uninitialized DateOnly).
            if (v.BeklenenValorTarihi == default)
            {
                uyarilar.Add(new VeriKalitesiUyariDto
                {
                    UyariTipi = NakitBankaPozisyonuUyariTipleri.ValorTarihiBos,
                    Aciklama = "Beklenen valör tarihi tanımlı değil.",
                    PosTahsilatValorId = v.Id,
                    Tutar = v.NetTutar
                });
            }

            // 6) Aktarim durumu ile muhasebe fisi iliskisi tutarsiz: bu kayit HENUZ aktarilmamis
            // (ValorBekliyor/MutabakatBekliyor/Hata) olmasina ragmen bir MuhasebeFisId tasiyorsa
            // bu durum/fis iliskisi celiskilidir.
            if (v.MuhasebeFisId.HasValue)
            {
                uyarilar.Add(new VeriKalitesiUyariDto
                {
                    UyariTipi = NakitBankaPozisyonuUyariTipleri.AktarimDurumuFisIliskisiTutarsiz,
                    Aciklama = $"Kayıt '{v.Durum}' durumunda olduğu halde bir muhasebe fişine (Id={v.MuhasebeFisId}) bağlı görünüyor.",
                    PosTahsilatValorId = v.Id,
                    Tutar = v.NetTutar
                });
            }
        }

        return uyarilar;
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

    private async Task<DateOnly> ResolveRaporTarihiAsync(NakitBankaPozisyonuFilterDto filter, CancellationToken cancellationToken)
    {
        if (filter.RaporTarihi.HasValue)
        {
            return filter.RaporTarihi.Value;
        }

        if (filter.TesisId.HasValue && filter.MaliYil.HasValue && filter.Donem.HasValue)
        {
            var donem = await _dbContext.MuhasebeDonemler.AsNoTracking()
                .Where(x => !x.IsDeleted && x.TesisId == filter.TesisId.Value && x.MaliYil == filter.MaliYil.Value && x.DonemNo == filter.Donem.Value)
                .Select(x => new { x.BitisTarihi })
                .FirstOrDefaultAsync(cancellationToken);

            if (donem is not null)
            {
                return DateOnly.FromDateTime(donem.BitisTarihi);
            }
        }

        return BugunIstanbul();
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
