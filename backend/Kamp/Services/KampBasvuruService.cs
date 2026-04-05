using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;
using TOD.Platform.Security.Auth.Services;

namespace STYS.Kamp.Services;

public class KampBasvuruService : IKampBasvuruService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IKampPuanlamaService _puanlamaService;
    private readonly IKampUcretHesaplamaService _ucretHesaplamaService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public KampBasvuruService(
        StysAppDbContext dbContext,
        IKampPuanlamaService puanlamaService,
        IKampUcretHesaplamaService ucretHesaplamaService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _dbContext = dbContext;
        _puanlamaService = puanlamaService;
        _ucretHesaplamaService = ucretHesaplamaService;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<KampBasvuruBaglamDto> GetBaglamAsync(CancellationToken cancellationToken = default)
    {
        var donemler = await _dbContext.KampDonemleri
            .Where(x => x.AktifMi)
            .Include(x => x.TesisAtamalari.Where(y => y.AktifMi && y.BasvuruyaAcikMi))
            .ThenInclude(x => x.Tesis)
            .OrderBy(x => x.KonaklamaBaslangicTarihi)
            .ToListAsync(cancellationToken);

        return new KampBasvuruBaglamDto
        {
            Donemler = donemler
                .Where(x => x.TesisAtamalari.Any(y => y.Tesis != null && BuildBirimler(y.Tesis).Count > 0))
                .Select(x => new KampBasvuruDonemSecenekDto
                {
                    Id = x.Id,
                    Ad = x.Ad,
                    KonaklamaBaslangicTarihi = x.KonaklamaBaslangicTarihi,
                    KonaklamaBitisTarihi = x.KonaklamaBitisTarihi,
                    Tesisler = x.TesisAtamalari
                        .Where(y => y.Tesis != null)
                        .Select(y => new KampBasvuruTesisSecenekDto
                        {
                            TesisId = y.TesisId,
                            TesisAd = y.Tesis!.Ad,
                            ToplamKontenjan = y.ToplamKontenjan,
                            Birimler = BuildBirimler(y.Tesis!)
                        })
                        .Where(y => y.Birimler.Count > 0)
                        .OrderBy(y => y.TesisAd)
                        .ToList()
                })
                .Where(x => x.Tesisler.Count > 0)
                .ToList()
        };
    }

    public async Task<KampBasvuruOnizlemeDto> OnizleAsync(KampBasvuruRequestDto request, CancellationToken cancellationToken = default)
    {
        var onizleme = new KampBasvuruOnizlemeDto();
        var (kampDonemi, tesis, atama) = await LoadContextAsync(request, cancellationToken);
        ValidateRequest(request, kampDonemi, tesis, atama, onizleme);
        AddWarnings(request, onizleme);
        await PopulateKontenjanAsync(request, atama, onizleme, cancellationToken);
        _puanlamaService.Puanla(request, onizleme);
        _ucretHesaplamaService.Hesapla(request, kampDonemi, tesis, onizleme);
        onizleme.BasvuruGecerliMi = onizleme.Hatalar.Count == 0;
        return onizleme;
    }

    public async Task<KampBasvuruDto> BasvuruOlusturAsync(KampBasvuruRequestDto request, CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            throw new BaseException("Kamp basvurusu icin giris yapan kullanici bilgisi bulunamadi.", 401);
        }

        var onizleme = await OnizleAsync(request, cancellationToken);
        if (!onizleme.BasvuruGecerliMi)
        {
            throw new BaseException(string.Join(" | ", onizleme.Hatalar), 400);
        }

        var (kampDonemi, tesis, _) = await LoadContextAsync(request, cancellationToken);
        var basvuruSahibi = request.Katilimcilar.Single(x => x.BasvuruSahibiMi);

        if (kampDonemi.AyniAileIcinTekBasvuruMu)
        {
            var mevcutBasvuruVar = await _dbContext.KampBasvurulari.AnyAsync(x =>
                x.KampDonemiId == request.KampDonemiId &&
                x.BasvuruSahibiUserId == currentUserId &&
                x.Durum != KampBasvuruDurumlari.IptalEdildi &&
                x.Durum != KampBasvuruDurumlari.Reddedildi,
                cancellationToken);

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
            BasvuruSahibiUserId = currentUserId,
            BasvuruSahibiAdiSoyadi = basvuruSahibi.AdSoyad.Trim(),
            BasvuruSahibiTipi = request.BasvuruSahibiTipi,
            HizmetYili = Math.Max(request.HizmetYili, 0),
            Kamp2023tenFaydalandiMi = request.Kamp2023tenFaydalandiMi,
            Kamp2024tenFaydalandiMi = request.Kamp2024tenFaydalandiMi,
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

        return MapToDto(entity, kampDonemi, tesis, onizleme.Uyarilar);
    }

    public async Task<List<KampBasvuruDto>> GetBenimBasvurularimAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            throw new BaseException("Kamp basvurularini gormek icin kullanici bilgisi bulunamadi.", 401);
        }

        var items = await _dbContext.KampBasvurulari
            .Where(x => x.BasvuruSahibiUserId == currentUserId)
            .Include(x => x.KampDonemi)
            .Include(x => x.Tesis)
            .Include(x => x.Katilimcilar)
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
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("Kamp basvurusu bulunamadi.", 404);

        return MapToDto(item, item.KampDonemi!, item.Tesis!, TryDeserializeWarnings(item.UyariMesajlariJson));
    }

    public async Task<KampKatilimciIptalSonucDto> KatilimciIptalEtAsync(int kampBasvuruId, int katilimciId, CancellationToken cancellationToken = default)
    {
        var basvuru = await _dbContext.KampBasvurulari
            .Include(x => x.Katilimcilar)
            .FirstOrDefaultAsync(x => x.Id == kampBasvuruId, cancellationToken)
            ?? throw new BaseException("Kamp basvurusu bulunamadi.", 404);

        if (basvuru.Durum == KampBasvuruDurumlari.IptalEdildi || basvuru.Durum == KampBasvuruDurumlari.Reddedildi)
            throw new BaseException("Iptal veya reddedilmis basvurularda katilimci iptali yapilamaz.", 400);

        var katilimci = basvuru.Katilimcilar.FirstOrDefault(x => x.Id == katilimciId)
            ?? throw new BaseException("Katilimci bulunamadi.", 404);

        if (katilimci.BasvuruSahibiMi)
            throw new BaseException("Basvuru sahibi iptal edilemez. Basvuru sahibinin kampa katilamayacak olmasi halinde basvurunun tamaminin iptal edilmesi gerekmektedir.", 400);

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

    private void ValidateRequest(KampBasvuruRequestDto request, KampDonemi kampDonemi, Tesis tesis, KampDonemiTesis atama, KampBasvuruOnizlemeDto onizleme)
    {
        if (!kampDonemi.AktifMi)
        {
            onizleme.Hatalar.Add("Secilen kamp donemi aktif degil.");
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

        if (!KampBasvuruSahibiTipleri.Hepsi.Contains(request.BasvuruSahibiTipi))
        {
            onizleme.Hatalar.Add("Basvuru sahibi tipi gecersiz.");
        }

        if (!atama.AktifMi || !atama.BasvuruyaAcikMi)
        {
            onizleme.Hatalar.Add("Secilen tesis bu kamp donemi icin basvuruya acik degil.");
        }

        KampKonaklamaKonfigurasyonu konfigurasyon;
        try
        {
            konfigurasyon = KampBasvuruKurallari.ResolveKonaklama(tesis, request.KonaklamaBirimiTipi);
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

        foreach (var katilimci in request.Katilimcilar)
        {
            if (string.IsNullOrWhiteSpace(katilimci.AdSoyad))
            {
                onizleme.Hatalar.Add("Tum katilimcilar icin ad soyad zorunludur.");
                continue;
            }

            if (!KampKatilimciTipleri.Hepsi.Contains(katilimci.KatilimciTipi))
            {
                onizleme.Hatalar.Add($"{katilimci.AdSoyad} icin katilimci tipi gecersiz.");
            }

            if (!KampAkrabalikTipleri.Hepsi.Contains(katilimci.AkrabalikTipi))
            {
                onizleme.Hatalar.Add($"{katilimci.AdSoyad} icin akrabalik tipi gecersiz.");
            }

            if (katilimci.DogumTarihi.Date > DateTime.Today)
            {
                onizleme.Hatalar.Add($"{katilimci.AdSoyad} icin dogum tarihi gelecekte olamaz.");
            }

            if (katilimci.BasvuruSahibiMi && katilimci.AkrabalikTipi != KampAkrabalikTipleri.BasvuruSahibi)
            {
                onizleme.Hatalar.Add("Basvuru sahibi olarak isaretlenen katilimcinin akrabalik tipi BasvuruSahibi olmalidir.");
            }
        }
    }

    private void AddWarnings(KampBasvuruRequestDto request, KampBasvuruOnizlemeDto onizleme)
    {
        foreach (var katilimci in request.Katilimcilar)
        {
            if (!KampAkrabalikTipleri.IsYakindanDogrulanabilir(katilimci.AkrabalikTipi))
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

    private static List<KampKonaklamaBirimiSecenekDto> BuildBirimler(Tesis tesis)
    {
        var tesisAd = (tesis.Ad ?? string.Empty).Trim().ToLowerInvariant();
        if (tesisAd.Contains("alata"))
        {
            return
            [
                new KampKonaklamaBirimiSecenekDto
                {
                    Kod = KampKonaklamaBirimiTipleri.AlataStandart,
                    Ad = "Alata Standart",
                    MinimumKisi = 3,
                    MaksimumKisi = 4
                }
            ];
        }

        if (tesisAd.Contains("foca") || tesisAd.Contains("foça"))
        {
            return
            [
                new KampKonaklamaBirimiSecenekDto
                {
                    Kod = KampKonaklamaBirimiTipleri.FocaPrefabrik,
                    Ad = "Foça Prefabrik",
                    MinimumKisi = 4,
                    MaksimumKisi = 5
                },
                new KampKonaklamaBirimiSecenekDto
                {
                    Kod = KampKonaklamaBirimiTipleri.FocaOtel,
                    Ad = "Foça Otel",
                    MinimumKisi = 4,
                    MaksimumKisi = 5
                },
                new KampKonaklamaBirimiSecenekDto
                {
                    Kod = KampKonaklamaBirimiTipleri.FocaBetonarme,
                    Ad = "Foça Betonarme",
                    MinimumKisi = 4,
                    MaksimumKisi = 5
                }
            ];
        }

        return [];
    }

    private static KampBasvuruDto MapToDto(KampBasvuru entity, KampDonemi kampDonemi, Tesis tesis, List<string> uyarilar)
        => new()
        {
            Id = entity.Id,
            KampDonemiId = entity.KampDonemiId,
            KampDonemiAd = kampDonemi.Ad,
            KonaklamaBaslangicTarihi = kampDonemi.KonaklamaBaslangicTarihi,
            KonaklamaBitisTarihi = kampDonemi.KonaklamaBitisTarihi,
            TesisId = entity.TesisId,
            TesisAd = tesis.Ad,
            KonaklamaBirimiTipi = entity.KonaklamaBirimiTipi,
            BasvuruSahibiAdiSoyadi = entity.BasvuruSahibiAdiSoyadi,
            BasvuruSahibiTipi = entity.BasvuruSahibiTipi,
            HizmetYili = entity.HizmetYili,
            Kamp2023tenFaydalandiMi = entity.Kamp2023tenFaydalandiMi,
            Kamp2024tenFaydalandiMi = entity.Kamp2024tenFaydalandiMi,
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
}
