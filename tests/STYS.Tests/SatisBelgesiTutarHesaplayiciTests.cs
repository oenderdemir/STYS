using STYS.Muhasebe.SatisBelgeleri;
using Xunit;

namespace STYS.Tests;

public class SatisBelgesiTutarHesaplayiciTests
{
    [Theory]
    [InlineData(0.005, 0.01)]
    [InlineData(-0.005, -0.01)]
    [InlineData(0.015, 0.02)]
    [InlineData(0.025, 0.03)]
    [InlineData(1.005, 1.01)]
    public void MidpointDegerlerdeAwayFromZeroUygulanir(decimal girdi, decimal beklenen)
    {
        Assert.Equal(beklenen, SatisBelgesiTutarHesaplayici.Yuvarla(girdi));
    }

    [Fact]
    public void MatrahKdvHesaplanmadanOnceYuvarlanir()
    {
        // brutMatrah - indirimTutari 2 ondalikten fazla uretebilir (ör. Miktar*BirimFiyat);
        // HesaplaMatrah bunu KDV'den ONCE 2 ondalik basamaga yuvarlamali.
        var matrah = SatisBelgesiTutarHesaplayici.HesaplaMatrah(brutMatrah: 10.0349m, indirimTutari: 0m);

        Assert.Equal(10.03m, matrah);
    }

    [Fact]
    public void KdvTutariYuvarlanmisMatrahUzerindenHesaplanir()
    {
        var kdvTutari = SatisBelgesiTutarHesaplayici.HesaplaKdvTutari(matrah: 10.03m, kdvOrani: 18m);

        Assert.Equal(1.81m, kdvTutari);
    }

    [Fact]
    public void SatirBazliYuvarlananKdvToplamiTopluMatrahtanYenidenHesaplananandanFarklidir()
    {
        const decimal kdvOrani = 18m;
        const decimal matrah1 = 10.03m;
        const decimal matrah2 = 10.04m;

        var kdv1 = SatisBelgesiTutarHesaplayici.HesaplaKdvTutari(matrah1, kdvOrani);
        var kdv2 = SatisBelgesiTutarHesaplayici.HesaplaKdvTutari(matrah2, kdvOrani);
        var satirBazliToplam = kdv1 + kdv2;

        var pooledMatrah = matrah1 + matrah2;
        var hataliPooledKdv = SatisBelgesiTutarHesaplayici.HesaplaKdvTutari(pooledMatrah, kdvOrani);

        // Doğru (satır bazlı yuvarlanmış) toplam ile hatalı (toplu matrahtan yeniden
        // hesaplanmış) toplam FARKLI çıkar - bu senaryonun var olduğunu doğrular.
        Assert.Equal(1.81m, kdv1);
        Assert.Equal(1.81m, kdv2);
        Assert.Equal(3.62m, satirBazliToplam);
        Assert.Equal(3.61m, hataliPooledKdv);
        Assert.NotEqual(satirBazliToplam, hataliPooledKdv);

        var satirlar = new[]
        {
            new SatisBelgesiTutarHesaplayici.SatirTutarKatkisi(matrah1, kdv1, matrah1 + kdv1),
            new SatisBelgesiTutarHesaplayici.SatirTutarKatkisi(matrah2, kdv2, matrah2 + kdv2)
        };

        // Belge toplam KDV'si SATIR BAZLI toplamla (3.62) kaydedilmişse tutarlı kabul edilir.
        var gecerliSonuc = SatisBelgesiTutarHesaplayici.DogrulaBelgeToplamlari(
            satirlar, pooledMatrah, satirBazliToplam, pooledMatrah + satirBazliToplam);
        Assert.Empty(gecerliSonuc);

        // Belge toplam KDV'si TOPLU MATRAHTAN yeniden hesaplanmış (hatalı, 3.61) değeri
        // taşıyorsa reddedilir - grup toplamı satır değerlerinden gelir, yeniden hesaplanmaz.
        var gecersizSonuc = SatisBelgesiTutarHesaplayici.DogrulaBelgeToplamlari(
            satirlar, pooledMatrah, hataliPooledKdv, pooledMatrah + hataliPooledKdv);
        var kdvUyusmazligi = Assert.Single(gecersizSonuc, u => u.Alan == "ToplamKdv");
        Assert.Equal(satirBazliToplam, kdvUyusmazligi.HesaplananDeger);
        Assert.Equal(hataliPooledKdv, kdvUyusmazligi.MevcutDeger);
    }

    [Fact]
    public void CokSatirliBelgedeToplamYuvarlanmisSatirlarinDuzToplamidir()
    {
        var satirlar = new[]
        {
            new SatisBelgesiTutarHesaplayici.SatirTutarKatkisi(Matrah: 100.00m, KdvTutari: 18.00m, SatirToplami: 118.00m),
            new SatisBelgesiTutarHesaplayici.SatirTutarKatkisi(Matrah: 50.25m, KdvTutari: 9.05m, SatirToplami: 59.30m),
            new SatisBelgesiTutarHesaplayici.SatirTutarKatkisi(Matrah: 33.33m, KdvTutari: 6.00m, SatirToplami: 39.33m)
        };

        var toplamMatrah = 100.00m + 50.25m + 33.33m;
        var toplamKdv = 18.00m + 9.05m + 6.00m;
        var genelToplam = 118.00m + 59.30m + 39.33m;

        var sonuc = SatisBelgesiTutarHesaplayici.DogrulaBelgeToplamlari(satirlar, toplamMatrah, toplamKdv, genelToplam);

        Assert.Empty(sonuc);
    }

    [Fact]
    public void BelgeToplamlariSatirToplamlariylaUyusmuyorsaUyusmazlikRaporlanir()
    {
        var satirlar = new[]
        {
            new SatisBelgesiTutarHesaplayici.SatirTutarKatkisi(Matrah: 100.00m, KdvTutari: 18.00m, SatirToplami: 118.00m)
        };

        var sonuc = SatisBelgesiTutarHesaplayici.DogrulaBelgeToplamlari(
            satirlar, toplamMatrah: 999m, toplamKdv: 18.00m, genelToplam: 118.00m);

        var uyusmazlik = Assert.Single(sonuc);
        Assert.Equal("ToplamMatrah", uyusmazlik.Alan);
        Assert.Equal(100.00m, uyusmazlik.HesaplananDeger);
        Assert.Equal(999m, uyusmazlik.MevcutDeger);
    }
}
