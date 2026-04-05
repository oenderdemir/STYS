using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Tesisler.Entities;

namespace STYS.Kamp.Services;

public class KampUcretHesaplamaService : IKampUcretHesaplamaService
{
    public void Hesapla(KampBasvuruRequestDto request, KampDonemi kampDonemi, Tesis tesis, KampBasvuruOnizlemeDto onizleme)
    {
        var konfigurasyon = KampBasvuruKurallari.ResolveKonaklama(tesis, request.KonaklamaBirimiTipi);
        var gunSayisi = (kampDonemi.KonaklamaBitisTarihi.Date - kampDonemi.KonaklamaBaslangicTarihi.Date).Days + 1;
        var toplamGunluk = 0m;
        var avansToplami = 0m;

        foreach (var katilimci in request.Katilimcilar)
        {
            var kamuTarifesiMi = KampKatilimciTipleri.KamuTarifesiUygulanirMi(katilimci.KatilimciTipi);
            var tamGunlukTutar = kamuTarifesiMi ? konfigurasyon.KamuGunlukUcret : konfigurasyon.DigerGunlukUcret;
            var katilimciGunlukTutari = HesaplaKatilimciGunlukTutari(katilimci, tamGunlukTutar);
            toplamGunluk += katilimciGunlukTutari;
            avansToplami += Math.Min(kamuTarifesiMi ? KampBasvuruKurallari.KamuAvansKisiBasi : KampBasvuruKurallari.DigerAvansKisiBasi, katilimciGunlukTutari * gunSayisi);
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

    private static decimal HesaplaKatilimciGunlukTutari(KampBasvuruKatilimciDto katilimci, decimal tamGunlukTutar)
    {
        var dogumTarihi = katilimci.DogumTarihi.Date;
        if (dogumTarihi > KampBasvuruKurallari.UcretsizCocukSiniri)
        {
            return katilimci.YemekTalepEdiyorMu
                ? decimal.Round(tamGunlukTutar * KampBasvuruKurallari.YemekOrani / 2m, 2, MidpointRounding.AwayFromZero)
                : 0m;
        }

        if (dogumTarihi >= KampBasvuruKurallari.YarimUcretliCocukSiniri && dogumTarihi <= KampBasvuruKurallari.UcretsizCocukSiniri)
        {
            return decimal.Round(tamGunlukTutar / 2m, 2, MidpointRounding.AwayFromZero);
        }

        return tamGunlukTutar;
    }
}
