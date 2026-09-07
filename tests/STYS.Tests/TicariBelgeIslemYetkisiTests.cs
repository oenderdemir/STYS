using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// TicariBelgeIslemYetkisi'nin üç otoriter durumdan (TicariDurum, MuhasebeDurumu,
/// FaturalamaDurumu) + MuhasebeFisId'den türettiği işlem yeteneklerini (özellikle bu turda
/// eklenen MuhasebeOnaylanabilirMi/ReddedilebilirMi/MuhasebeFisiOlusturulabilirMi) doğrulayan
/// hızlı, DB gerektirmeyen birim testleri.
/// </summary>
public class TicariBelgeIslemYetkisiTests
{
    [Theory]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onayda, true)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Bekliyor, false)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onaylandi, false)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Reddedildi, false)]
    [InlineData(TicariBelgeDurumu.IptalEdildi, TicariBelgeMuhasebeDurumu.Onayda, false)]
    public void MuhasebeOnaylanabilirMi_YalnizcaOnaydaVeIptalEdilmemisKombinasyonlardaTrueDoner(
        TicariBelgeDurumu ticariDurum, TicariBelgeMuhasebeDurumu muhasebeDurumu, bool beklenen)
    {
        Assert.Equal(beklenen, TicariBelgeIslemYetkisi.MuhasebeOnaylanabilirMi(ticariDurum, muhasebeDurumu));
    }

    [Theory]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onayda, true)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Bekliyor, false)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onaylandi, false)]
    [InlineData(TicariBelgeDurumu.IptalEdildi, TicariBelgeMuhasebeDurumu.Onayda, false)]
    public void ReddedilebilirMi_YalnizcaOnaydaVeIptalEdilmemisKombinasyonlardaTrueDoner(
        TicariBelgeDurumu ticariDurum, TicariBelgeMuhasebeDurumu muhasebeDurumu, bool beklenen)
    {
        Assert.Equal(beklenen, TicariBelgeIslemYetkisi.ReddedilebilirMi(ticariDurum, muhasebeDurumu));
    }

    [Theory]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, true)]
    [InlineData(SatisBelgesiTipi.AlisFaturasi, true)]
    [InlineData(SatisBelgesiTipi.SatisIadeFaturasi, true)]
    [InlineData(SatisBelgesiTipi.AlisIadeFaturasi, true)]
    [InlineData(SatisBelgesiTipi.FaturaTaslagi, false)]
    [InlineData(SatisBelgesiTipi.Proforma, false)]
    [InlineData(SatisBelgesiTipi.IadeFaturasi, false)]
    public void MuhasebeFisiOlusturulabilirMi_OnaylandiVeFisYokkenBelgeTipineGoreDogruSonucVerir(
        SatisBelgesiTipi belgeTipi, bool beklenen)
    {
        var sonuc = TicariBelgeIslemYetkisi.MuhasebeFisiOlusturulabilirMi(
            TicariBelgeMuhasebeDurumu.Onaylandi, muhasebeFisId: null, belgeTipi);

        Assert.Equal(beklenen, sonuc);
    }

    [Fact]
    public void MuhasebeFisiOlusturulabilirMi_MuhasebeDurumuOnaylandiDegilseHicbirBelgeTipindeTrueDonmez()
    {
        Assert.False(TicariBelgeIslemYetkisi.MuhasebeFisiOlusturulabilirMi(
            TicariBelgeMuhasebeDurumu.Onayda, muhasebeFisId: null, SatisBelgesiTipi.SatisFaturasi));
        Assert.False(TicariBelgeIslemYetkisi.MuhasebeFisiOlusturulabilirMi(
            TicariBelgeMuhasebeDurumu.Bekliyor, muhasebeFisId: null, SatisBelgesiTipi.SatisFaturasi));
    }

    [Fact]
    public void MuhasebeFisiOlusturulabilirMi_MuhasebeFisIdDoluysaDesteklenenTipteBileFalseDoner()
    {
        Assert.False(TicariBelgeIslemYetkisi.MuhasebeFisiOlusturulabilirMi(
            TicariBelgeMuhasebeDurumu.Onaylandi, muhasebeFisId: 42, SatisBelgesiTipi.SatisFaturasi));
    }

    // ── Rezervasyon check-out gelir belgesi (source-aware) istisnası ──

    [Fact]
    public void MuhasebeFisiOlusturulabilirMi_RezervasyonCheckoutFaturaTaslagi_IstisnaOlarakTrueDoner()
    {
        var sonuc = TicariBelgeIslemYetkisi.MuhasebeFisiOlusturulabilirMi(
            TicariBelgeMuhasebeDurumu.Onaylandi, muhasebeFisId: null, SatisBelgesiTipi.FaturaTaslagi,
            SatisKaynakModulu.Otel, "RezervasyonCheckout");

        Assert.True(sonuc);
    }

    [Fact]
    public void MuhasebeFisiOlusturulabilirMi_GenelFaturaTaslagi_KaynakRezervasyonCheckoutDegilseFalseDoner()
    {
        // Manuel kaynaklı (kullanıcı) FaturaTaslagi — istisna UYGULANMAZ.
        Assert.False(TicariBelgeIslemYetkisi.MuhasebeFisiOlusturulabilirMi(
            TicariBelgeMuhasebeDurumu.Onaylandi, muhasebeFisId: null, SatisBelgesiTipi.FaturaTaslagi,
            SatisKaynakModulu.Manuel, null));

        // Otel kaynaklı ama farklı KaynakTipi (ör. Proforma değil ama başka bir otel taslağı) — istisna UYGULANMAZ.
        Assert.False(TicariBelgeIslemYetkisi.MuhasebeFisiOlusturulabilirMi(
            TicariBelgeMuhasebeDurumu.Onaylandi, muhasebeFisId: null, SatisBelgesiTipi.FaturaTaslagi,
            SatisKaynakModulu.Otel, "BasKasiyerTaslagi"));

        // 3 parametreli (kaynak bilgisi taşımayan) overload FaturaTaslagi için fail-closed kalır.
        Assert.False(TicariBelgeIslemYetkisi.MuhasebeFisiOlusturulabilirMi(
            TicariBelgeMuhasebeDurumu.Onaylandi, muhasebeFisId: null, SatisBelgesiTipi.FaturaTaslagi));
    }

    [Fact]
    public void MuhasebeFisiOlusturulabilirMi_RezervasyonCheckoutFaturaTaslagi_OnaylandiDegilseFalseDoner()
    {
        Assert.False(TicariBelgeIslemYetkisi.MuhasebeFisiOlusturulabilirMi(
            TicariBelgeMuhasebeDurumu.Onayda, muhasebeFisId: null, SatisBelgesiTipi.FaturaTaslagi,
            SatisKaynakModulu.Otel, "RezervasyonCheckout"));
        Assert.False(TicariBelgeIslemYetkisi.MuhasebeFisiOlusturulabilirMi(
            TicariBelgeMuhasebeDurumu.Bekliyor, muhasebeFisId: null, SatisBelgesiTipi.FaturaTaslagi,
            SatisKaynakModulu.Otel, "RezervasyonCheckout"));
    }

    [Fact]
    public void MuhasebeFisiOlusturulabilirMi_RezervasyonCheckoutFaturaTaslagi_FisIdDoluysaFalseDoner()
    {
        Assert.False(TicariBelgeIslemYetkisi.MuhasebeFisiOlusturulabilirMi(
            TicariBelgeMuhasebeDurumu.Onaylandi, muhasebeFisId: 99, SatisBelgesiTipi.FaturaTaslagi,
            SatisKaynakModulu.Otel, "RezervasyonCheckout"));
    }
}
