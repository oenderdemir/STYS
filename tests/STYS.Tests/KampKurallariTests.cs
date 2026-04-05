using STYS.Kamp;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Kamp.Services;
using STYS.Tesisler.Entities;

namespace STYS.Tests;

public class KampKurallariTests
{
    [Fact]
    public void Puanlama_TarimOrmanPersoneliIcinTalimatPuanlariniUygular()
    {
        var service = new KampPuanlamaService();
        var onizleme = new KampBasvuruOnizlemeDto();
        var request = new KampBasvuruRequestDto
        {
            BasvuruSahibiTipi = KampBasvuruSahibiTipleri.TarimOrmanPersoneli,
            HizmetYili = 12,
            Kamp2023tenFaydalandiMi = true,
            Kamp2024tenFaydalandiMi = false,
            Katilimcilar =
            [
                new KampBasvuruKatilimciDto { AdSoyad = "A", DogumTarihi = new DateTime(1990, 1, 1), BasvuruSahibiMi = true },
                new KampBasvuruKatilimciDto { AdSoyad = "B", DogumTarihi = new DateTime(1992, 1, 1) },
                new KampBasvuruKatilimciDto { AdSoyad = "C", DogumTarihi = new DateTime(2015, 1, 1) }
            ]
        };

        service.Puanla(request, onizleme);

        Assert.Equal(1, onizleme.OncelikSirasi);
        Assert.Equal(62, onizleme.Puan);
    }

    [Fact]
    public void UcretHesaplama_CocukKurallariVeEkHizmetleriUygular()
    {
        var service = new KampUcretHesaplamaService();
        var onizleme = new KampBasvuruOnizlemeDto();
        var donem = new KampDonemi
        {
            KonaklamaBaslangicTarihi = new DateTime(2025, 6, 2),
            KonaklamaBitisTarihi = new DateTime(2025, 6, 6)
        };
        var tesis = new Tesis { Ad = "Foça" };
        var request = new KampBasvuruRequestDto
        {
            KonaklamaBirimiTipi = KampKonaklamaBirimiTipleri.FocaPrefabrik,
            KlimaTalepEdildiMi = true,
            Katilimcilar =
            [
                new KampBasvuruKatilimciDto { AdSoyad = "Yetiskin Kamu", KatilimciTipi = KampKatilimciTipleri.Kamu, DogumTarihi = new DateTime(1985, 1, 1), BasvuruSahibiMi = true },
                new KampBasvuruKatilimciDto { AdSoyad = "Yetiskin Diger", KatilimciTipi = KampKatilimciTipleri.Diger, DogumTarihi = new DateTime(1988, 1, 1) },
                new KampBasvuruKatilimciDto { AdSoyad = "Cocuk", KatilimciTipi = KampKatilimciTipleri.Diger, DogumTarihi = new DateTime(2020, 6, 1) },
                new KampBasvuruKatilimciDto { AdSoyad = "Bebek", KatilimciTipi = KampKatilimciTipleri.Diger, DogumTarihi = new DateTime(2023, 6, 1), YemekTalepEdiyorMu = true }
            ]
        };

        service.Hesapla(request, donem, tesis, onizleme);

        Assert.Equal(5175m, onizleme.GunlukToplamTutar);
        Assert.Equal(25875m, onizleme.DonemToplamTutar);
        Assert.Equal(6800m, onizleme.AvansToplamTutar);
        Assert.Equal(19075m, onizleme.KalanOdemeTutari);
    }

    [Fact]
    public void IadeHesaplama_BirHaftadanAzKalaGunlukKesintiUygular()
    {
        var service = new KampIadeService();
        var result = service.Hesapla(new KampIadeHesaplamaRequestDto
        {
            BasvuruDurumu = KampBasvuruDurumlari.Beklemede,
            KampBaslangicTarihi = new DateTime(2025, 6, 10),
            VazgecmeTarihi = new DateTime(2025, 6, 5),
            AvansTutari = 6800m,
            DonemToplamTutari = 25875m,
            OdenenToplamTutar = 6800m,
            ToplamGunSayisi = 5,
            KullanilmayanGunSayisi = 0,
            MazeretliZorunluAyrilisMi = false
        });

        Assert.True(result.IadeVarMi);
        Assert.Equal(1700m, result.KesintiTutari);
        Assert.Equal(5100m, result.IadeTutari);
        Assert.Equal(KampIadeNedenleri.GecBildirimMazeretsiz, result.Gerekce);
    }
}
