using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Tesisler.Entities;

namespace STYS.Kamp.Services;

public class KampUcretHesaplamaService : IKampUcretHesaplamaService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IKampParametreService _params;

    public KampUcretHesaplamaService(StysAppDbContext dbContext, IKampParametreService kampParametreService)
    {
        _dbContext = dbContext;
        _params = kampParametreService;
    }

    public async Task HesaplaAsync(
        KampBasvuruRequestDto request,
        KampDonemi kampDonemi,
        Tesis tesis,
        KampBasvuruOnizlemeDto onizleme,
        CancellationToken cancellationToken = default)
    {
        var konfigurasyon = await ResolveKonaklamaKonfigurasyonuAsync(tesis.Id, request.KonaklamaBirimiTipi, cancellationToken);
        var gunSayisi = (kampDonemi.KonaklamaBitisTarihi.Date - kampDonemi.KonaklamaBaslangicTarihi.Date).Days + 1;
        var toplamGunluk = 0m;
        var avansToplami = 0m;
        var katilimciTipleri = await _dbContext.KampKatilimciTipleri
            .AsNoTracking()
            .Where(x => x.AktifMi)
            .ToDictionaryAsync(x => x.Kod, x => x.KamuTarifesiUygulanirMi, cancellationToken);

        var kamuAvans = _params.GetDecimal(KampParametreKodlari.KamuAvansKisiBasi, KampBasvuruKurallari.KamuAvansKisiBasi);
        var digerAvans = _params.GetDecimal(KampParametreKodlari.DigerAvansKisiBasi, KampBasvuruKurallari.DigerAvansKisiBasi);
        var yasKurali = await ResolveYasKuraliAsync(cancellationToken);

        foreach (var katilimci in request.Katilimcilar)
        {
            var kamuTarifesiMi = katilimciTipleri.GetValueOrDefault(katilimci.KatilimciTipi);
            var tamGunlukTutar = kamuTarifesiMi ? konfigurasyon.KamuGunlukUcret : konfigurasyon.DigerGunlukUcret;
            var katilimciGunlukTutari = HesaplaKatilimciGunlukTutari(
                katilimci,
                kampDonemi.KonaklamaBaslangicTarihi.Date,
                tamGunlukTutar,
                yasKurali.YemekOrani,
                yasKurali.UcretsizCocukMaxYas,
                yasKurali.YarimUcretliCocukMaxYas);
            toplamGunluk += katilimciGunlukTutari;
            avansToplami += Math.Min(kamuTarifesiMi ? kamuAvans : digerAvans, katilimciGunlukTutari * gunSayisi);
        }

        if (request.BuzdolabiTalepEdildiMi)
        {
            toplamGunluk += konfigurasyon.BuzdolabiGunlukUcret;
        }

        if (request.TelevizyonTalepEdildiMi)
        {
            toplamGunluk += konfigurasyon.TelevizyonGunlukUcret;
        }

        if (request.KlimaTalepEdildiMi)
        {
            toplamGunluk += konfigurasyon.KlimaGunlukUcret;
        }

        var donemToplami = toplamGunluk * gunSayisi;
        onizleme.GunlukToplamTutar = decimal.Round(toplamGunluk, 2, MidpointRounding.AwayFromZero);
        onizleme.DonemToplamTutar = decimal.Round(donemToplami, 2, MidpointRounding.AwayFromZero);
        onizleme.AvansToplamTutar = decimal.Round(avansToplami, 2, MidpointRounding.AwayFromZero);
        onizleme.KalanOdemeTutari = decimal.Round(Math.Max(0m, donemToplami - avansToplami), 2, MidpointRounding.AwayFromZero);
    }

    private async Task<KampYasUcretKurali> ResolveYasKuraliAsync(CancellationToken cancellationToken)
    {
        var entity = await _dbContext.KampYasUcretKurallari
            .AsNoTracking()
            .Where(x => x.AktifMi)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is not null)
        {
            return entity;
        }

        return new KampYasUcretKurali
        {
            UcretsizCocukMaxYas = KampBasvuruKurallari.UcretsizCocukMaxYas,
            YarimUcretliCocukMaxYas = KampBasvuruKurallari.YarimUcretliCocukMaxYas,
            YemekOrani = KampBasvuruKurallari.YemekOrani
        };
    }

    private async Task<KampKonaklamaKonfigurasyonu> ResolveKonaklamaKonfigurasyonuAsync(int tesisId, string secilenBirim, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secilenBirim))
        {
            throw new InvalidOperationException("Konaklama birimi secimi zorunludur.");
        }

        var normalizedSecim = secilenBirim.Trim();

        var tarifeler = GetAktifKonaklamaTarifeleri();
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
            throw new InvalidOperationException("Secilen konaklama birimi bu tesiste aktif oda tipleriyle eslesmiyor.");
        }

        var minimum = kapasiteListesi.Min();
        var maksimum = kapasiteListesi.Max();
        var secilen = tarifeler
            .FirstOrDefault(x => x.MinimumKisi == minimum && x.MaksimumKisi == maksimum);
        if (secilen is null)
        {
            throw new InvalidOperationException($"Konaklama birimi icin uygun ucret konfigurasyonu bulunamadi ({minimum}-{maksimum}).");
        }

        return secilen;
    }

    private List<KampKonaklamaKonfigurasyonu> GetAktifKonaklamaTarifeleri()
    {
        var tarifeler = _dbContext.KampKonaklamaTarifeleri
            .AsNoTracking()
            .Where(x => x.AktifMi)
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
        var byPrefix = _params.GetByPrefix(KampKonaklamaBirimiTipleri.ParametrePrefix);
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
                result.Add(KampBasvuruKurallari.ResolveKonaklama(_params, birimKodu));
            }
            catch
            {
                // Eksik/gecersiz parametreli birimler atlanir.
            }
        }

        return result;
    }

    private static decimal HesaplaKatilimciGunlukTutari(
        KampBasvuruKatilimciDto katilimci,
        DateTime referansTarih,
        decimal tamGunlukTutar,
        decimal yemekOrani,
        int ucretsizCocukMaxYas,
        int yarimUcretliCocukMaxYas)
    {
        var yas = YasHesapla(katilimci.DogumTarihi.Date, referansTarih);
        if (yas <= ucretsizCocukMaxYas)
        {
            return katilimci.YemekTalepEdiyorMu
                ? decimal.Round(tamGunlukTutar * yemekOrani / 2m, 2, MidpointRounding.AwayFromZero)
                : 0m;
        }

        if (yas <= yarimUcretliCocukMaxYas)
        {
            return decimal.Round(tamGunlukTutar / 2m, 2, MidpointRounding.AwayFromZero);
        }

        return tamGunlukTutar;
    }

    private static int YasHesapla(DateTime dogumTarihi, DateTime referansTarih)
    {
        var yas = referansTarih.Year - dogumTarihi.Year;
        if (dogumTarihi.Date > referansTarih.Date.AddYears(-yas))
        {
            yas--;
        }

        return Math.Max(0, yas);
    }
}
