using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kamp.Services;

public class KampBasvuruService : IKampBasvuruService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IKampPuanlamaService _puanlamaService;
    private readonly IKampUcretHesaplamaService _ucretHesaplamaService;
    private readonly IKampParametreService _parametreService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public KampBasvuruService(
        StysAppDbContext dbContext,
        IKampPuanlamaService puanlamaService,
        IKampUcretHesaplamaService ucretHesaplamaService,
        IKampParametreService parametreService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _dbContext = dbContext;
        _puanlamaService = puanlamaService;
        _ucretHesaplamaService = ucretHesaplamaService;
        _parametreService = parametreService;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<KampBasvuruBaglamDto> GetBaglamAsync(CancellationToken cancellationToken = default)
    {
        await _parametreService.LoadAsync(cancellationToken);
        var lookupBaglami = await LoadLookupBaglamiAsync(cancellationToken);
        var donemler = await _dbContext.KampDonemleri
            .Where(x => x.AktifMi && x.KampProgrami != null && x.KampProgrami.AktifMi)
            .Include(x => x.KampProgrami)
            .Include(x => x.TesisAtamalari.Where(y => y.AktifMi && y.BasvuruyaAcikMi))
            .ThenInclude(x => x.Tesis)
            .OrderBy(x => x.KonaklamaBaslangicTarihi)
            .ToListAsync(cancellationToken);

        return new KampBasvuruBaglamDto
        {
            Donemler = donemler
                .Select(x => new KampBasvuruDonemSecenekDto
                {
                    Id = x.Id,
                    KampProgramiId = x.KampProgramiId,
                    KampProgramiAd = x.KampProgrami != null ? x.KampProgrami.Ad : null,
                    KampProgramiYil = x.KampProgrami != null ? x.KampProgrami.Yil : 0,
                    Ad = x.Ad,
                    KonaklamaBaslangicTarihi = x.KonaklamaBaslangicTarihi,
                    KonaklamaBitisTarihi = x.KonaklamaBitisTarihi,
                    GecmisKatilimYillari = BuildGecmisKatilimYillari(x.KampProgrami!.Yil, lookupBaglami.GetKuralSeti(x.KampProgramiId)),
                    Tesisler = x.TesisAtamalari
                        .Where(y => y.Tesis != null)
                        .Select(y => new KampBasvuruTesisSecenekDto
                        {
                            TesisId = y.TesisId,
                            TesisAd = y.Tesis!.Ad,
                            ToplamKontenjan = y.ToplamKontenjan,
                            Birimler = BuildBirimler(x.KampProgramiId, y.KonaklamaTarifeKodlari)
                        })
                        .Where(y => y.Birimler.Count > 0)
                        .OrderBy(y => y.TesisAd)
                        .ToList()
                })
                .Where(x => x.Tesisler.Count > 0)
                .ToList(),
            BasvuruSahibiTipleri = lookupBaglami.BasvuruSahibiTipleri
                .Select(x => new KampBasvuruSahibiTipSecenekDto
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Ad = x.Ad,
                    VarsayilanKatilimciTipiKodu = x.VarsayilanKatilimciTipiKodu
                })
                .ToList(),
            KatilimciTipleri = lookupBaglami.KatilimciTipleri
                .Select(x => new KampSecenekDto
                {
                    Kod = x.Kod,
                    Ad = x.Ad
                })
                .ToList(),
            AkrabalikTipleri = lookupBaglami.AkrabalikTipleri
                .Select(x => new KampAkrabalikTipiSecenekDto
                {
                    Kod = x.Kod,
                    Ad = x.Ad,
                    BasvuruSahibiAkrabaligiMi = x.BasvuruSahibiAkrabaligiMi
                })
                .ToList()
        };
    }

    public async Task<KampBasvuruOnizlemeDto> OnizleAsync(KampBasvuruRequestDto request, CancellationToken cancellationToken = default)
    {
        await _parametreService.LoadAsync(cancellationToken);
        var lookupBaglami = await LoadLookupBaglamiAsync(cancellationToken);
        var onizleme = new KampBasvuruOnizlemeDto();
        var (kampDonemi, tesis, atama) = await LoadContextAsync(request, cancellationToken);
        await ValidateRequestAsync(request, kampDonemi, tesis, atama, lookupBaglami, onizleme, cancellationToken);
        AddWarnings(request, lookupBaglami, onizleme);

        var basvuruSahibi = GetBasvuruSahibiKatilimci(request);
        var mevcutSahip = basvuruSahibi is null
            ? null
            : await FindBasvuruSahibiAsync(NormalizeNullable(basvuruSahibi.TcKimlikNo), cancellationToken);

        var birlesikGecmisKatilimYillari = await BuildBirlesikGecmisKatilimYillariAsync(
            kampDonemi.KampProgrami!.Yil,
            request.GecmisKatilimYillari,
            mevcutSahip?.Id,
            cancellationToken);

        onizleme.GecmisKatilimYillari = birlesikGecmisKatilimYillari;

        await PopulateKontenjanAsync(request, atama, onizleme, cancellationToken);
        await _puanlamaService.PuanlaAsync(request, onizleme, kampDonemi.KampProgramiId, birlesikGecmisKatilimYillari, cancellationToken);
        await _ucretHesaplamaService.HesaplaAsync(request, kampDonemi, tesis, onizleme, cancellationToken);
        onizleme.BasvuruGecerliMi = onizleme.Hatalar.Count == 0;
        return onizleme;
    }

    public async Task<KampBasvuruDto> BasvuruOlusturAsync(KampBasvuruRequestDto request, CancellationToken cancellationToken = default)
    {
        await _parametreService.LoadAsync(cancellationToken);
        var currentUserId = _currentUserAccessor.GetCurrentUserId();

        var onizleme = await OnizleAsync(request, cancellationToken);
        if (!onizleme.BasvuruGecerliMi)
        {
            throw new BaseException(string.Join(" | ", onizleme.Hatalar), 400);
        }

        var (kampDonemi, tesis, _) = await LoadContextAsync(request, cancellationToken);
        var basvuruSahibi = GetBasvuruSahibiKatilimci(request)
            ?? throw new BaseException("Basvuru sahibi bilgisi bulunamadi.", 400);
        var kampBasvuruSahibi = await ResolveBasvuruSahibiAsync(basvuruSahibi, request, currentUserId, cancellationToken);

        if (kampDonemi.AyniAileIcinTekBasvuruMu)
        {
            var mevcutBasvuruVar = await ExistsAktifBasvuruAsync(request.KampDonemiId, kampBasvuruSahibi, cancellationToken);

            if (mevcutBasvuruVar)
            {
                throw new BaseException("Bu kamp donemi icin ayni aile adina ikinci bir basvuru olusturulamaz.", 400);
            }
        }

        var entity = new KampBasvuru
        {
            KampDonemiId = request.KampDonemiId,
            TesisId = request.TesisId,
            KonaklamaBirimiTipi = request.KonaklamaBirimiTipi,
            BasvuruNo = await GenerateBasvuruNoAsync(kampDonemi.KampProgrami!.Yil, cancellationToken),
            KampBasvuruSahibi = kampBasvuruSahibi,
            BasvuruSahibiAdiSoyadiSnapshot = basvuruSahibi.AdSoyad.Trim(),
            BasvuruSahibiTipiSnapshot = request.BasvuruSahibiTipi,
            HizmetYiliSnapshot = Math.Max(request.HizmetYili, 0),
            EvcilHayvanGetirecekMi = request.EvcilHayvanGetirecekMi,
            Durum = KampBasvuruDurumlari.Beklemede,
            KatilimciSayisi = request.Katilimcilar.Count,
            OncelikSirasi = onizleme.OncelikSirasi,
            Puan = onizleme.Puan,
            GunlukToplamTutar = onizleme.GunlukToplamTutar,
            DonemToplamTutar = onizleme.DonemToplamTutar,
            AvansToplamTutar = onizleme.AvansToplamTutar,
            KalanOdemeTutari = onizleme.KalanOdemeTutari,
            UyariMesajlariJson = onizleme.Uyarilar.Count == 0 ? null : JsonSerializer.Serialize(onizleme.Uyarilar),
            BuzdolabiTalepEdildiMi = request.BuzdolabiTalepEdildiMi,
            TelevizyonTalepEdildiMi = request.TelevizyonTalepEdildiMi,
            KlimaTalepEdildiMi = request.KlimaTalepEdildiMi,
            Katilimcilar = request.Katilimcilar.Select(x => new KampBasvuruKatilimci
            {
                AdSoyad = x.AdSoyad.Trim(),
                TcKimlikNo = NormalizeNullable(x.TcKimlikNo),
                DogumTarihi = x.DogumTarihi.Date,
                BasvuruSahibiMi = x.BasvuruSahibiMi,
                KatilimciTipi = x.KatilimciTipi,
                AkrabalikTipi = x.AkrabalikTipi,
                KimlikBilgileriDogrulandiMi = x.KimlikBilgileriDogrulandiMi,
                YemekTalepEdiyorMu = x.YemekTalepEdiyorMu
            }).ToList()
        };

        await _dbContext.KampBasvurulari.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await EnsureGecmisKatilimKayitlariAsync(kampBasvuruSahibi.Id, onizleme.GecmisKatilimYillari, entity.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity, kampDonemi, tesis, onizleme.Uyarilar, onizleme.GecmisKatilimYillari);
    }

    public async Task<List<KampBasvuruDto>> GetBenimBasvurularimAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            throw new BaseException("Kamp basvurularini gormek icin kullanici bilgisi bulunamadi.", 401);
        }

        var items = await _dbContext.KampBasvurulari
            .Where(x => x.KampBasvuruSahibi != null && x.KampBasvuruSahibi.UserId == currentUserId)
            .Include(x => x.KampDonemi)
            .Include(x => x.Tesis)
            .Include(x => x.Katilimcilar)
            .Include(x => x.KampBasvuruSahibi)
            .ThenInclude(x => x!.GecmisKatilimlar)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(x => MapToDto(x, x.KampDonemi!, x.Tesis!, TryDeserializeWarnings(x.UyariMesajlariJson))).ToList();
    }

    public async Task<KampBasvuruDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.KampBasvurulari
            .Include(x => x.KampDonemi)
            .Include(x => x.Tesis)
            .Include(x => x.Katilimcilar)
            .Include(x => x.KampBasvuruSahibi)
            .ThenInclude(x => x!.GecmisKatilimlar)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("Kamp basvurusu bulunamadi.", 404);

        return MapToDto(item, item.KampDonemi!, item.Tesis!, TryDeserializeWarnings(item.UyariMesajlariJson));
    }

    public async Task<KampBasvuruDto> GetByBasvuruNoAsync(string basvuruNo, CancellationToken cancellationToken = default)
    {
        var normalizedBasvuruNo = NormalizeBasvuruNo(basvuruNo);
        if (string.IsNullOrWhiteSpace(normalizedBasvuruNo))
        {
            throw new BaseException("Basvuru numarasi zorunludur.", 400);
        }

        var item = await _dbContext.KampBasvurulari
            .Include(x => x.KampDonemi)
            .Include(x => x.Tesis)
            .Include(x => x.Katilimcilar)
            .Include(x => x.KampBasvuruSahibi)
            .ThenInclude(x => x!.GecmisKatilimlar)
            .FirstOrDefaultAsync(x => x.BasvuruNo == normalizedBasvuruNo, cancellationToken)
            ?? throw new BaseException("Basvuru numarasi ile eslesen kamp basvurusu bulunamadi.", 404);

        return MapToDto(item, item.KampDonemi!, item.Tesis!, TryDeserializeWarnings(item.UyariMesajlariJson));
    }

    public async Task<KampKatilimciIptalSonucDto> KatilimciIptalEtAsync(int kampBasvuruId, int katilimciId, CancellationToken cancellationToken = default)
    {
        var basvuru = await _dbContext.KampBasvurulari
            .Include(x => x.Katilimcilar)
            .FirstOrDefaultAsync(x => x.Id == kampBasvuruId, cancellationToken)
            ?? throw new BaseException("Kamp basvurusu bulunamadi.", 404);

        if (basvuru.Durum == KampBasvuruDurumlari.IptalEdildi || basvuru.Durum == KampBasvuruDurumlari.Reddedildi)
        {
            throw new BaseException("Iptal veya reddedilmis basvurularda katilimci iptali yapilamaz.", 400);
        }

        var katilimci = basvuru.Katilimcilar.FirstOrDefault(x => x.Id == katilimciId)
            ?? throw new BaseException("Katilimci bulunamadi.", 404);

        if (katilimci.BasvuruSahibiMi)
        {
            throw new BaseException("Basvuru sahibi iptal edilemez. Basvuru sahibinin kampa katilamayacak olmasi halinde basvurunun tamaminin iptal edilmesi gerekmektedir.", 400);
        }

        katilimci.IsDeleted = true;

        var kalanKatilimciSayisi = basvuru.Katilimcilar.Count(x => !x.IsDeleted);
        basvuru.KatilimciSayisi = kalanKatilimciSayisi;

        string? uyariMesaji = null;
        if (kalanKatilimciSayisi == 1)
        {
            uyariMesaji = "Talimat geregi: iptal islemi sonucu tek kisi katilimci kalmasi halinde basvuru listesindeki kisilerin yatak ucreti tahsil edilir.";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new KampKatilimciIptalSonucDto
        {
            KampBasvuruId = kampBasvuruId,
            KatilimciId = katilimciId,
            KalanKatilimciSayisi = kalanKatilimciSayisi,
            TekKisiKaldiMi = kalanKatilimciSayisi == 1,
            UyariMesaji = uyariMesaji
        };
    }

    private async Task<(KampDonemi kampDonemi, Tesis tesis, KampDonemiTesis atama)> LoadContextAsync(KampBasvuruRequestDto request, CancellationToken cancellationToken)
    {
        var kampDonemi = await _dbContext.KampDonemleri.FirstOrDefaultAsync(x => x.Id == request.KampDonemiId, cancellationToken)
            ?? throw new BaseException("Kamp donemi bulunamadi.", 404);

        var tesis = await _dbContext.Tesisler.FirstOrDefaultAsync(x => x.Id == request.TesisId, cancellationToken)
            ?? throw new BaseException("Tesis bulunamadi.", 404);

        var atama = await _dbContext.KampDonemiTesisleri.FirstOrDefaultAsync(
            x => x.KampDonemiId == request.KampDonemiId && x.TesisId == request.TesisId,
            cancellationToken)
            ?? throw new BaseException("Secilen tesis bu kamp donemine atanmamis.", 400);

        return (kampDonemi, tesis, atama);
    }

    private async Task ValidateRequestAsync(
        KampBasvuruRequestDto request,
        KampDonemi kampDonemi,
        Tesis tesis,
        KampDonemiTesis atama,
        KampLookupBaglami lookupBaglami,
        KampBasvuruOnizlemeDto onizleme,
        CancellationToken cancellationToken)
    {
        if (!kampDonemi.AktifMi)
        {
            onizleme.Hatalar.Add("Secilen kamp donemi aktif degil.");
        }

        var programAktifMi = await _dbContext.KampProgramlari
            .AsNoTracking()
            .AnyAsync(x => x.Id == kampDonemi.KampProgramiId && x.AktifMi, cancellationToken);
        if (!programAktifMi)
        {
            onizleme.Hatalar.Add("Secilen kamp doneminin kamp programi aktif degil.");
        }

        var bugun = DateTime.UtcNow.Date;
        if (bugun < kampDonemi.BasvuruBaslangicTarihi.Date)
        {
            onizleme.Hatalar.Add($"Basvuru donemi henuz baslamadi. Basvuru baslangic tarihi: {kampDonemi.BasvuruBaslangicTarihi:dd.MM.yyyy}");
        }

        if (bugun > kampDonemi.BasvuruBitisTarihi.Date)
        {
            onizleme.Hatalar.Add($"Basvuru suresi doldu. Son basvuru tarihi: {kampDonemi.BasvuruBitisTarihi:dd.MM.yyyy}");
        }

        if (!lookupBaglami.BasvuruSahibiTipleriByKod.ContainsKey(request.BasvuruSahibiTipi))
        {
            onizleme.Hatalar.Add("Basvuru sahibi tipi gecersiz.");
        }

        var kuralSeti = lookupBaglami.GetKuralSeti(kampDonemi.KampProgramiId);
        if (kuralSeti is null)
        {
            onizleme.Hatalar.Add("Secili program icin aktif kamp kural seti bulunamadi.");
        }

        if (!atama.AktifMi || !atama.BasvuruyaAcikMi)
        {
            onizleme.Hatalar.Add("Secilen tesis bu kamp donemi icin basvuruya acik degil.");
        }

        KampKonaklamaKonfigurasyonu konfigurasyon;
        try
        {
            konfigurasyon = await ResolveKonaklamaKonfigurasyonuAsync(kampDonemi.KampProgramiId, request.TesisId, request.KonaklamaBirimiTipi, cancellationToken);
        }
        catch (Exception ex)
        {
            onizleme.Hatalar.Add(ex.Message);
            return;
        }

        if (request.EvcilHayvanGetirecekMi)
        {
            onizleme.Hatalar.Add("Talimat geregi evcil hayvan kabul edilmemektedir.");
        }

        if (request.Katilimcilar.Count == 0)
        {
            onizleme.Hatalar.Add("En az bir katilimci girilmelidir.");
            return;
        }

        if (request.Katilimcilar.Count < konfigurasyon.MinimumKisi || request.Katilimcilar.Count > konfigurasyon.MaksimumKisi)
        {
            onizleme.Hatalar.Add($"Secilen birim tipi {konfigurasyon.MinimumKisi}-{konfigurasyon.MaksimumKisi} kisi kabul eder.");
        }

        var basvuruSahibiSayisi = request.Katilimcilar.Count(x => x.BasvuruSahibiMi);
        if (basvuruSahibiSayisi != 1)
        {
            onizleme.Hatalar.Add("Basvuru icinde tam olarak bir basvuru sahibi tanimlanmalidir.");
        }

        var izinliGecmisYillar = kuralSeti is null
            ? []
            : BuildGecmisKatilimYillari(kampDonemi.KampProgrami!.Yil, kuralSeti);

        foreach (var yil in request.GecmisKatilimYillari.Distinct())
        {
            if (!izinliGecmisYillar.Contains(yil))
            {
                onizleme.Hatalar.Add($"{yil} yili secili kamp donemi icin dikkate alinabilecek gecmis katilim yillari arasinda degil.");
            }
        }

        foreach (var katilimci in request.Katilimcilar)
        {
            if (string.IsNullOrWhiteSpace(katilimci.AdSoyad))
            {
                onizleme.Hatalar.Add("Tum katilimcilar icin ad soyad zorunludur.");
                continue;
            }

            if (!lookupBaglami.KatilimciTipleriByKod.ContainsKey(katilimci.KatilimciTipi))
            {
                onizleme.Hatalar.Add($"{katilimci.AdSoyad} icin katilimci tipi gecersiz.");
            }

            if (!lookupBaglami.AkrabalikTipleriByKod.ContainsKey(katilimci.AkrabalikTipi))
            {
                onizleme.Hatalar.Add($"{katilimci.AdSoyad} icin akrabalik tipi gecersiz.");
            }

            if (katilimci.DogumTarihi.Date > DateTime.Today)
            {
                onizleme.Hatalar.Add($"{katilimci.AdSoyad} icin dogum tarihi gelecekte olamaz.");
            }

            if (katilimci.BasvuruSahibiMi && katilimci.AkrabalikTipi != lookupBaglami.BasvuruSahibiAkrabalikKodu)
            {
                onizleme.Hatalar.Add("Basvuru sahibi olarak isaretlenen katilimcinin akrabalik tipi basvuru sahibi olmalidir.");
            }
        }
    }

    private void AddWarnings(KampBasvuruRequestDto request, KampLookupBaglami lookupBaglami, KampBasvuruOnizlemeDto onizleme)
    {
        foreach (var katilimci in request.Katilimcilar)
        {
            if (lookupBaglami.AkrabalikTipleriByKod.TryGetValue(katilimci.AkrabalikTipi, out var akrabalikTipi) && !akrabalikTipi.YakindanDogrulanabilirMi)
            {
                onizleme.Uyarilar.Add($"{katilimci.AdSoyad} icin birinci derece / es iliskisi disinda bir akrabalik bildirildi.");
            }

            if (!katilimci.KimlikBilgileriDogrulandiMi)
            {
                onizleme.Uyarilar.Add($"{katilimci.AdSoyad} icin kimlik bilgileri henuz dogrulanmadi.");
            }

            if (string.IsNullOrWhiteSpace(katilimci.TcKimlikNo))
            {
                onizleme.Uyarilar.Add($"{katilimci.AdSoyad} icin kimlik numarasi eksik.");
            }
        }
    }

    private async Task PopulateKontenjanAsync(KampBasvuruRequestDto request, KampDonemiTesis atama, KampBasvuruOnizlemeDto onizleme, CancellationToken cancellationToken)
    {
        var kullanilanKontenjan = await _dbContext.KampBasvurulari.CountAsync(
            x => x.KampDonemiId == request.KampDonemiId
                && x.TesisId == request.TesisId
                && x.Durum != KampBasvuruDurumlari.Reddedildi
                && x.Durum != KampBasvuruDurumlari.IptalEdildi
                && x.Durum != KampBasvuruDurumlari.TahsisEdilemedi,
            cancellationToken);

        onizleme.KullanilanKontenjan = kullanilanKontenjan;
        onizleme.ToplamKontenjan = atama.ToplamKontenjan;
        onizleme.BosKontenjan = Math.Max(0, atama.ToplamKontenjan - kullanilanKontenjan);

        if (kullanilanKontenjan >= atama.ToplamKontenjan)
        {
            onizleme.Hatalar.Add("Secilen tesis ve donem icin bos kontenjan kalmadi.");
            onizleme.KontenjanMesaji = "Kontenjan dolu";
        }
        else
        {
            onizleme.KontenjanMesaji = $"{onizleme.BosKontenjan} bos kontenjan";
        }
    }

    private async Task<KampLookupBaglami> LoadLookupBaglamiAsync(CancellationToken cancellationToken)
    {
        var basvuruSahibiTipleri = await _dbContext.KampBasvuruSahibiTipleri
            .AsNoTracking()
            .Where(x => x.AktifMi)
            .OrderBy(x => x.OncelikSirasi)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var katilimciTipleri = await _dbContext.KampKatilimciTipleri
            .AsNoTracking()
            .Where(x => x.AktifMi)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var akrabalikTipleri = await _dbContext.KampAkrabalikTipleri
            .AsNoTracking()
            .Where(x => x.AktifMi)
            .OrderByDescending(x => x.BasvuruSahibiAkrabaligiMi)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var kuralSetleri = await _dbContext.KampKuralSetleri
            .AsNoTracking()
            .Where(x => x.AktifMi)
            .ToListAsync(cancellationToken);

        return new KampLookupBaglami(
            basvuruSahibiTipleri,
            katilimciTipleri,
            akrabalikTipleri,
            kuralSetleri);
    }

    private async Task<KampBasvuruSahibi?> FindBasvuruSahibiAsync(string? tcKimlikNo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tcKimlikNo))
        {
            return null;
        }

        return await _dbContext.KampBasvuruSahipleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TcKimlikNo == tcKimlikNo, cancellationToken);
    }

    private async Task<KampBasvuruSahibi> ResolveBasvuruSahibiAsync(
        KampBasvuruKatilimciDto basvuruSahibi,
        KampBasvuruRequestDto request,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var tcKimlikNo = NormalizeNullable(basvuruSahibi.TcKimlikNo);
        if (!string.IsNullOrWhiteSpace(tcKimlikNo))
        {
            var mevcut = await _dbContext.KampBasvuruSahipleri.FirstOrDefaultAsync(x => x.TcKimlikNo == tcKimlikNo, cancellationToken);
            if (mevcut is not null)
            {
                mevcut.AdSoyad = basvuruSahibi.AdSoyad.Trim();
                mevcut.BasvuruSahibiTipi = request.BasvuruSahibiTipi;
                mevcut.HizmetYili = Math.Max(request.HizmetYili, 0);
                mevcut.UserId ??= currentUserId;
                mevcut.AktifMi = true;
                return mevcut;
            }
        }

        var entity = new KampBasvuruSahibi
        {
            TcKimlikNo = tcKimlikNo,
            AdSoyad = basvuruSahibi.AdSoyad.Trim(),
            BasvuruSahibiTipi = request.BasvuruSahibiTipi,
            HizmetYili = Math.Max(request.HizmetYili, 0),
            UserId = currentUserId,
            AktifMi = true
        };

        await _dbContext.KampBasvuruSahipleri.AddAsync(entity, cancellationToken);
        return entity;
    }

    private async Task<bool> ExistsAktifBasvuruAsync(int kampDonemiId, KampBasvuruSahibi kampBasvuruSahibi, CancellationToken cancellationToken)
    {
        var query = _dbContext.KampBasvurulari.Where(x =>
            x.KampDonemiId == kampDonemiId &&
            x.Durum != KampBasvuruDurumlari.IptalEdildi &&
            x.Durum != KampBasvuruDurumlari.Reddedildi);

        if (kampBasvuruSahibi.Id > 0)
        {
            return await query.AnyAsync(x => x.KampBasvuruSahibiId == kampBasvuruSahibi.Id, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(kampBasvuruSahibi.TcKimlikNo))
        {
            return await query.AnyAsync(
                x => x.KampBasvuruSahibi != null && x.KampBasvuruSahibi.TcKimlikNo == kampBasvuruSahibi.TcKimlikNo,
                cancellationToken);
        }

        if (kampBasvuruSahibi.UserId.HasValue)
        {
            return await query.AnyAsync(
                x => x.KampBasvuruSahibi != null && x.KampBasvuruSahibi.UserId == kampBasvuruSahibi.UserId,
                cancellationToken);
        }

        return false;
    }

    private async Task<string> GenerateBasvuruNoAsync(int kampYili, CancellationToken cancellationToken)
    {
        for (var deneme = 0; deneme < 10; deneme++)
        {
            var randomHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
            var basvuruNo = $"KB{kampYili}{randomHex}";
            var exists = await _dbContext.KampBasvurulari.AnyAsync(x => x.BasvuruNo == basvuruNo, cancellationToken);
            if (!exists)
            {
                return basvuruNo;
            }
        }

        throw new BaseException("Kamp basvuru numarasi olusturulamadi.", 500);
    }

    private async Task<List<int>> BuildBirlesikGecmisKatilimYillariAsync(
        int kampYili,
        IEnumerable<int> secilenGecmisKatilimYillari,
        int? kampBasvuruSahibiId,
        CancellationToken cancellationToken)
    {
        var yillar = new HashSet<int>(SanitizeGecmisKatilimYillari(secilenGecmisKatilimYillari, kampYili));
        if (!kampBasvuruSahibiId.HasValue)
        {
            return yillar.OrderByDescending(x => x).ToList();
        }

        var mevcutYillar = await _dbContext.KampBasvuruGecmisKatilimlari
            .AsNoTracking()
            .Where(x => x.KampBasvuruSahibiId == kampBasvuruSahibiId.Value && x.AktifMi)
            .Select(x => x.KatilimYili)
            .ToListAsync(cancellationToken);

        foreach (var yil in SanitizeGecmisKatilimYillari(mevcutYillar, kampYili))
        {
            yillar.Add(yil);
        }

        return yillar.OrderByDescending(x => x).ToList();
    }

    private async Task EnsureGecmisKatilimKayitlariAsync(
        int kampBasvuruSahibiId,
        IEnumerable<int> gecmisKatilimYillari,
        int kaynakBasvuruId,
        CancellationToken cancellationToken)
    {
        var mevcutKayitlar = await _dbContext.KampBasvuruGecmisKatilimlari
            .Where(x => x.KampBasvuruSahibiId == kampBasvuruSahibiId)
            .ToListAsync(cancellationToken);

        foreach (var yil in gecmisKatilimYillari.Distinct())
        {
            var mevcut = mevcutKayitlar.FirstOrDefault(x => x.KatilimYili == yil);
            if (mevcut is null)
            {
                _dbContext.KampBasvuruGecmisKatilimlari.Add(new KampBasvuruGecmisKatilim
                {
                    KampBasvuruSahibiId = kampBasvuruSahibiId,
                    KatilimYili = yil,
                    KaynakBasvuruId = kaynakBasvuruId,
                    BeyanMi = true,
                    AktifMi = true
                });

                continue;
            }

            mevcut.AktifMi = true;
            mevcut.BeyanMi = true;
            mevcut.KaynakBasvuruId ??= kaynakBasvuruId;
        }
    }

    private static KampBasvuruKatilimciDto? GetBasvuruSahibiKatilimci(KampBasvuruRequestDto request)
        => request.Katilimcilar.FirstOrDefault(x => x.BasvuruSahibiMi);

    private static List<int> BuildGecmisKatilimYillari(int kampYili, KampKuralSeti? kuralSeti)
    {
        if (kuralSeti is null || kuralSeti.OncekiYilSayisi <= 0)
        {
            return [];
        }

        return Enumerable.Range(kampYili - kuralSeti.OncekiYilSayisi, kuralSeti.OncekiYilSayisi)
            .Where(x => x > 0 && x < kampYili)
            .OrderByDescending(x => x)
            .ToList();
    }

    private static List<int> SanitizeGecmisKatilimYillari(IEnumerable<int> yillar, int kampYili)
        => yillar
            .Where(x => x > 0 && x < kampYili)
            .Distinct()
            .OrderByDescending(x => x)
            .ToList();

    private static string NormalizeBasvuruNo(string? basvuruNo)
        => string.IsNullOrWhiteSpace(basvuruNo)
            ? string.Empty
            : basvuruNo.Trim().ToUpperInvariant();

    private List<KampKonaklamaBirimiSecenekDto> BuildBirimler(int kampProgramiId, IReadOnlyList<string> tarifeKodlari)
    {
        var aktifTarifeler = GetAktifKonaklamaTarifeleri(kampProgramiId);

        // Tarife kodu listesi varsa filtrele; yoksa tüm aktif tarifeleri getir (fallback)
        var tarifeler = tarifeKodlari.Count > 0
            ? aktifTarifeler.Where(x => tarifeKodlari.Contains(x.Kod, StringComparer.OrdinalIgnoreCase)).ToList()
            : aktifTarifeler;

        return tarifeler.Select(x => new KampKonaklamaBirimiSecenekDto
        {
            Kod = x.Kod,
            Ad = _parametreService.GetString(
                KampKonaklamaBirimiTipleri.BuildParametreKodu(x.Kod, KampKonaklamaBirimiTipleri.AlanAd),
                x.Kod),
            MinimumKisi = x.MinimumKisi,
            MaksimumKisi = x.MaksimumKisi
        }).ToList();
    }

    private async Task<KampKonaklamaKonfigurasyonu> ResolveKonaklamaKonfigurasyonuAsync(int kampProgramiId, int tesisId, string secilenBirim, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secilenBirim))
        {
            throw new BaseException("Konaklama birimi secimi zorunludur.", 400);
        }

        var normalizedSecim = secilenBirim.Trim();

        var tarifeler = GetAktifKonaklamaTarifeleri(kampProgramiId);
        var seciliTarife = tarifeler.FirstOrDefault(x => string.Equals(x.Kod, normalizedSecim, StringComparison.OrdinalIgnoreCase));
        if (seciliTarife is not null)
        {
            return seciliTarife;
        }

        var binaKapasiteleri = await _dbContext.Binalar
            .AsNoTracking()
            .Where(x => x.AktifMi && x.TesisId == tesisId && x.Ad.ToLower() == normalizedSecim.ToLower())
            .Select(x => x.Odalar
                .Where(o => o.AktifMi && o.TesisOdaTipi != null && o.TesisOdaTipi.AktifMi)
                .Select(o => o.TesisOdaTipi!.Kapasite))
            .FirstOrDefaultAsync(cancellationToken);

        var kapasiteListesi = binaKapasiteleri?.ToList() ?? [];
        if (kapasiteListesi.Count == 0)
        {
            throw new BaseException("Secilen konaklama birimi bu tesiste aktif oda tipleriyle eslesmiyor.", 400);
        }

        var minimum = kapasiteListesi.Min();
        var maksimum = kapasiteListesi.Max();

        var secilen = SelectBestTarifeByKapasite(tarifeler, minimum, maksimum);
        if (secilen is null)
        {
            throw new BaseException($"Konaklama birimi icin uygun ucret konfigurasyonu bulunamadi ({minimum}-{maksimum}).", 400);
        }

        return secilen;
    }

    private List<KampKonaklamaKonfigurasyonu> GetAktifKonaklamaTarifeleri(int kampProgramiId)
    {
        var tarifeler = _dbContext.KampKonaklamaTarifeleri
            .AsNoTracking()
            .Where(x => x.AktifMi && x.KampProgramiId == kampProgramiId)
            .OrderByDescending(x => x.Id)
            .Select(x => new KampKonaklamaKonfigurasyonu(
                x.Kod,
                x.MinimumKisi,
                x.MaksimumKisi,
                x.KamuGunlukUcret,
                x.DigerGunlukUcret,
                x.BuzdolabiGunlukUcret,
                x.TelevizyonGunlukUcret,
                x.KlimaGunlukUcret))
            .ToList();

        if (tarifeler.Count > 0)
        {
            return tarifeler;
        }

        // Gecis donemi fallback: migration uygulanmamis ortamlarda eski parametrelerden devam et.
        var byPrefix = _parametreService.GetByPrefix(KampKonaklamaBirimiTipleri.ParametrePrefix);
        if (byPrefix.Count == 0)
        {
            return [];
        }

        var birimKodlari = byPrefix.Keys
            .Select(x => x.Split('.', StringSplitOptions.RemoveEmptyEntries))
            .Where(x => x.Length >= 3)
            .Select(x => x[1])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<KampKonaklamaKonfigurasyonu>();
        foreach (var birimKodu in birimKodlari)
        {
            try
            {
                result.Add(KampBasvuruKurallari.ResolveKonaklama(_parametreService, birimKodu));
            }
            catch
            {
                // Eksik/gecersiz parametreli birimler atlanir.
            }
        }

        return result;
    }

    private static KampKonaklamaKonfigurasyonu? SelectBestTarifeByKapasite(
        IReadOnlyCollection<KampKonaklamaKonfigurasyonu> tarifeler,
        int minimum,
        int maksimum)
    {
        var exact = tarifeler.FirstOrDefault(x => x.MinimumKisi == minimum && x.MaksimumKisi == maksimum);
        if (exact is not null)
        {
            return exact;
        }

        var kapsayan = tarifeler
            .Where(x => x.MinimumKisi <= minimum && x.MaksimumKisi >= maksimum)
            .OrderBy(x => (x.MaksimumKisi - x.MinimumKisi))
            .ThenBy(x => Math.Abs(x.MinimumKisi - minimum) + Math.Abs(x.MaksimumKisi - maksimum))
            .FirstOrDefault();
        if (kapsayan is not null)
        {
            return kapsayan;
        }

        return tarifeler
            .Where(x => x.MinimumKisi <= maksimum && x.MaksimumKisi >= minimum)
            .OrderBy(x => Math.Abs(x.MinimumKisi - minimum) + Math.Abs(x.MaksimumKisi - maksimum))
            .FirstOrDefault();
    }

    private KampBasvuruDto MapToDto(
        KampBasvuru entity,
        KampDonemi kampDonemi,
        Tesis tesis,
        List<string> uyarilar,
        List<int>? gecmisKatilimYillari = null)
        => new()
        {
            Id = entity.Id,
            BasvuruNo = entity.BasvuruNo,
            KampDonemiId = entity.KampDonemiId,
            KampDonemiAd = kampDonemi.Ad,
            KonaklamaBaslangicTarihi = kampDonemi.KonaklamaBaslangicTarihi,
            KonaklamaBitisTarihi = kampDonemi.KonaklamaBitisTarihi,
            TesisId = entity.TesisId,
            TesisAd = tesis.Ad,
            KonaklamaBirimiTipi = entity.KonaklamaBirimiTipi,
            BasvuruSahibiAdiSoyadi = entity.BasvuruSahibiAdiSoyadiSnapshot,
            BasvuruSahibiTipi = entity.BasvuruSahibiTipiSnapshot,
            HizmetYili = entity.HizmetYiliSnapshot,
            GecmisKatilimYillari = (gecmisKatilimYillari ?? entity.KampBasvuruSahibi?.GecmisKatilimlar
                    .Where(x => x.AktifMi && x.KatilimYili < kampDonemi.KampProgrami!.Yil)
                    .Select(x => x.KatilimYili)
                    .Distinct()
                    .OrderByDescending(x => x)
                    .ToList())
                ?? [],
            EvcilHayvanGetirecekMi = entity.EvcilHayvanGetirecekMi,
            Durum = entity.Durum,
            KatilimciSayisi = entity.KatilimciSayisi,
            OncelikSirasi = entity.OncelikSirasi,
            Puan = entity.Puan,
            GunlukToplamTutar = entity.GunlukToplamTutar,
            DonemToplamTutar = entity.DonemToplamTutar,
            AvansToplamTutar = entity.AvansToplamTutar,
            KalanOdemeTutari = entity.KalanOdemeTutari,
            Uyarilar = uyarilar,
            BuzdolabiTalepEdildiMi = entity.BuzdolabiTalepEdildiMi,
            TelevizyonTalepEdildiMi = entity.TelevizyonTalepEdildiMi,
            KlimaTalepEdildiMi = entity.KlimaTalepEdildiMi,
            CreatedAt = entity.CreatedAt,
            Katilimcilar = entity.Katilimcilar.Select(x => new KampBasvuruKatilimciDto
            {
                Id = x.Id,
                AdSoyad = x.AdSoyad,
                TcKimlikNo = x.TcKimlikNo,
                DogumTarihi = x.DogumTarihi,
                BasvuruSahibiMi = x.BasvuruSahibiMi,
                KatilimciTipi = x.KatilimciTipi,
                AkrabalikTipi = x.AkrabalikTipi,
                KimlikBilgileriDogrulandiMi = x.KimlikBilgileriDogrulandiMi,
                YemekTalepEdiyorMu = x.YemekTalepEdiyorMu
            }).ToList()
        };

    private static List<string> TryDeserializeWarnings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class KampLookupBaglami
    {
        public KampLookupBaglami(
            List<KampBasvuruSahibiTipi> basvuruSahibiTipleri,
            List<KampKatilimciTipi> katilimciTipleri,
            List<KampAkrabalikTipi> akrabalikTipleri,
            List<KampKuralSeti> kuralSetleri)
        {
            BasvuruSahibiTipleri = basvuruSahibiTipleri;
            KatilimciTipleri = katilimciTipleri;
            AkrabalikTipleri = akrabalikTipleri;
            KuralSetleriById = kuralSetleri
                .GroupBy(x => x.KampProgramiId)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Id).First());
            BasvuruSahibiTipleriByKod = basvuruSahibiTipleri.ToDictionary(x => x.Kod, x => x);
            KatilimciTipleriByKod = katilimciTipleri.ToDictionary(x => x.Kod, x => x);
            AkrabalikTipleriByKod = akrabalikTipleri.ToDictionary(x => x.Kod, x => x);
            BasvuruSahibiAkrabalikKodu = akrabalikTipleri.FirstOrDefault(x => x.BasvuruSahibiAkrabaligiMi)?.Kod;
        }

        public List<KampBasvuruSahibiTipi> BasvuruSahibiTipleri { get; }

        public List<KampKatilimciTipi> KatilimciTipleri { get; }

        public List<KampAkrabalikTipi> AkrabalikTipleri { get; }

        public Dictionary<string, KampBasvuruSahibiTipi> BasvuruSahibiTipleriByKod { get; }

        public Dictionary<string, KampKatilimciTipi> KatilimciTipleriByKod { get; }

        public Dictionary<string, KampAkrabalikTipi> AkrabalikTipleriByKod { get; }

        public Dictionary<int, KampKuralSeti> KuralSetleriById { get; }

        public string? BasvuruSahibiAkrabalikKodu { get; }

        public KampKuralSeti? GetKuralSeti(int kampProgramiId)
            => KuralSetleriById.GetValueOrDefault(kampProgramiId);
    }
}
