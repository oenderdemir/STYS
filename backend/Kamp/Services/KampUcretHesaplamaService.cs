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
        var konfigurasyon = KampBasvuruKurallari.ResolveKonaklama(_params, request.KonaklamaBirimiTipi);
        var gunSayisi = (kampDonemi.KonaklamaBitisTarihi.Date - kampDonemi.KonaklamaBaslangicTarihi.Date).Days + 1;
        var toplamGunluk = 0m;
        var avansToplami = 0m;
        var katilimciTipleri = await _dbContext.KampKatilimciTipleri
            .AsNoTracking()
            .Where(x => x.AktifMi)
            .ToDictionaryAsync(x => x.Kod, x => x.KamuTarifesiUygulanirMi, cancellationToken);

        var kamuAvans = _params.GetDecimal(KampParametreKodlari.KamuAvansKisiBasi, KampBasvuruKurallari.KamuAvansKisiBasi);
        var digerAvans = _params.GetDecimal(KampParametreKodlari.DigerAvansKisiBasi, KampBasvuruKurallari.DigerAvansKisiBasi);
        var yemekOrani = _params.GetDecimal(KampParametreKodlari.YemekOrani, KampBasvuruKurallari.YemekOrani);
        var ucretsizSinir = _params.GetDate(KampParametreKodlari.UcretsizCocukSiniri, KampBasvuruKurallari.UcretsizCocukSiniri);
        var yarimUcretSinir = _params.GetDate(KampParametreKodlari.YarimUcretliCocukSiniri, KampBasvuruKurallari.YarimUcretliCocukSiniri);

        foreach (var katilimci in request.Katilimcilar)
        {
            var kamuTarifesiMi = katilimciTipleri.GetValueOrDefault(katilimci.KatilimciTipi);
            var tamGunlukTutar = kamuTarifesiMi ? konfigurasyon.KamuGunlukUcret : konfigurasyon.DigerGunlukUcret;
            var katilimciGunlukTutari = HesaplaKatilimciGunlukTutari(katilimci, tamGunlukTutar, yemekOrani, ucretsizSinir, yarimUcretSinir);
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

    private static decimal HesaplaKatilimciGunlukTutari(KampBasvuruKatilimciDto katilimci, decimal tamGunlukTutar, decimal yemekOrani, DateTime ucretsizSinir, DateTime yarimUcretSinir)
    {
        var dogumTarihi = katilimci.DogumTarihi.Date;
        if (dogumTarihi > ucretsizSinir)
        {
            return katilimci.YemekTalepEdiyorMu
                ? decimal.Round(tamGunlukTutar * yemekOrani / 2m, 2, MidpointRounding.AwayFromZero)
                : 0m;
        }

        if (dogumTarihi >= yarimUcretSinir && dogumTarihi <= ucretsizSinir)
        {
            return decimal.Round(tamGunlukTutar / 2m, 2, MidpointRounding.AwayFromZero);
        }

        return tamGunlukTutar;
    }
}
