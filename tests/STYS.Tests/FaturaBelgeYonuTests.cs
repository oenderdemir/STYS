using STYS.Muhasebe.SatisBelgeleri.Enums;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// SatisBelgesiTipiExtensions'a eklenen belge-düzenleme-yönü sınıflandırmasının (STYS mi düzenler,
/// karşı taraf mı düzenler, otomatik resmî numara üretilebilir mi) doğruluğunu ve mevcut
/// IsSatisBelgesi/IsAlisBelgesi/IsIadeBelgesi davranışlarının DEĞİŞMEDİĞİNİ doğrulayan birim
/// testleri. DB gerektirmez.
/// </summary>
public class FaturaBelgeYonuTests
{
    public static IEnumerable<object[]> TumBelgeTipleri() =>
        Enum.GetValues<SatisBelgesiTipi>().Select(x => new object[] { x });

    [Theory]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, true)]
    [InlineData(SatisBelgesiTipi.AlisIadeFaturasi, true)]
    [InlineData(SatisBelgesiTipi.AlisFaturasi, false)]
    [InlineData(SatisBelgesiTipi.SatisIadeFaturasi, false)]
    [InlineData(SatisBelgesiTipi.FaturaTaslagi, false)]
    [InlineData(SatisBelgesiTipi.Proforma, false)]
    [InlineData(SatisBelgesiTipi.IadeFaturasi, false)]
    public void StysTarafindanDuzenlenirMi_OtoriterSiniflandirmayaUyar(SatisBelgesiTipi belgeTipi, bool beklenen)
    {
        Assert.Equal(beklenen, belgeTipi.StysTarafindanDuzenlenirMi());
    }

    [Theory]
    [InlineData(SatisBelgesiTipi.AlisFaturasi, true)]
    [InlineData(SatisBelgesiTipi.SatisIadeFaturasi, true)]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, false)]
    [InlineData(SatisBelgesiTipi.AlisIadeFaturasi, false)]
    [InlineData(SatisBelgesiTipi.FaturaTaslagi, false)]
    [InlineData(SatisBelgesiTipi.Proforma, false)]
    [InlineData(SatisBelgesiTipi.IadeFaturasi, false)]
    public void KarsiTarafTarafindanDuzenlenirMi_OtoriterSiniflandirmayaUyar(SatisBelgesiTipi belgeTipi, bool beklenen)
    {
        Assert.Equal(beklenen, belgeTipi.KarsiTarafTarafindanDuzenlenirMi());
    }

    [Theory]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, true)]
    [InlineData(SatisBelgesiTipi.AlisIadeFaturasi, true)]
    [InlineData(SatisBelgesiTipi.AlisFaturasi, false)]
    [InlineData(SatisBelgesiTipi.SatisIadeFaturasi, false)]
    [InlineData(SatisBelgesiTipi.FaturaTaslagi, false)]
    [InlineData(SatisBelgesiTipi.Proforma, false)]
    [InlineData(SatisBelgesiTipi.IadeFaturasi, false)]
    public void OtomatikResmiNumaraUretilebilirMi_YalnizcaStysTarafindanDuzenlenenGidenBelgelerdeTrue(
        SatisBelgesiTipi belgeTipi, bool beklenen)
    {
        Assert.Equal(beklenen, belgeTipi.OtomatikResmiNumaraUretilebilirMi());
    }

    [Theory]
    [MemberData(nameof(TumBelgeTipleri))]
    public void StysVeKarsiTarafSiniflandirmalari_AslaAyniAndaTrueOlamaz(SatisBelgesiTipi belgeTipi)
    {
        // Bir belge aynı anda hem "STYS tarafından düzenlenen" hem "karşı taraf tarafından
        // düzenlenen" olamaz - iki sınıflandırma birbirini dışlar (kesişim boş küme).
        Assert.False(belgeTipi.StysTarafindanDuzenlenirMi() && belgeTipi.KarsiTarafTarafindanDuzenlenirMi());
    }

    [Theory]
    [MemberData(nameof(TumBelgeTipleri))]
    public void OtomatikResmiNumaraUretilebilirMi_HicbirZamanKarsiTarafBelgesindeTrueOlamaz(SatisBelgesiTipi belgeTipi)
    {
        if (belgeTipi.KarsiTarafTarafindanDuzenlenirMi())
        {
            Assert.False(belgeTipi.OtomatikResmiNumaraUretilebilirMi());
        }
    }

    [Fact]
    public void IsSatisBelgesi_MevcutDavranisDegismedi()
    {
        Assert.True(SatisBelgesiTipi.FaturaTaslagi.IsSatisBelgesi());
        Assert.True(SatisBelgesiTipi.SatisFaturasi.IsSatisBelgesi());
        Assert.True(SatisBelgesiTipi.SatisIadeFaturasi.IsSatisBelgesi());
        Assert.True(SatisBelgesiTipi.IadeFaturasi.IsSatisBelgesi());
        Assert.True(SatisBelgesiTipi.Proforma.IsSatisBelgesi());
        Assert.False(SatisBelgesiTipi.AlisFaturasi.IsSatisBelgesi());
        Assert.False(SatisBelgesiTipi.AlisIadeFaturasi.IsSatisBelgesi());
    }

    [Fact]
    public void IsAlisBelgesi_MevcutDavranisDegismedi()
    {
        Assert.True(SatisBelgesiTipi.AlisFaturasi.IsAlisBelgesi());
        Assert.True(SatisBelgesiTipi.AlisIadeFaturasi.IsAlisBelgesi());
        Assert.False(SatisBelgesiTipi.SatisFaturasi.IsAlisBelgesi());
        Assert.False(SatisBelgesiTipi.SatisIadeFaturasi.IsAlisBelgesi());
        Assert.False(SatisBelgesiTipi.FaturaTaslagi.IsAlisBelgesi());
        Assert.False(SatisBelgesiTipi.Proforma.IsAlisBelgesi());
        Assert.False(SatisBelgesiTipi.IadeFaturasi.IsAlisBelgesi());
    }

    [Fact]
    public void IsIadeBelgesi_MevcutDavranisDegismedi()
    {
        Assert.True(SatisBelgesiTipi.IadeFaturasi.IsIadeBelgesi());
        Assert.True(SatisBelgesiTipi.SatisIadeFaturasi.IsIadeBelgesi());
        Assert.True(SatisBelgesiTipi.AlisIadeFaturasi.IsIadeBelgesi());
        Assert.False(SatisBelgesiTipi.FaturaTaslagi.IsIadeBelgesi());
        Assert.False(SatisBelgesiTipi.SatisFaturasi.IsIadeBelgesi());
        Assert.False(SatisBelgesiTipi.Proforma.IsIadeBelgesi());
        Assert.False(SatisBelgesiTipi.AlisFaturasi.IsIadeBelgesi());
    }
}
