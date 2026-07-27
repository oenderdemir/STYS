using STYS.Muhasebe.Common.Services;

namespace STYS.Tests;

/// <summary>
/// TersKayitIliskisiDogrulama SAF bilesenidir - "TersKayitMuhasebeFisId dolu oldugu icin
/// iliski gecerlidir" varsayiminin yapilmadigini dogrudan dogrular. Otoriter iliski
/// IptalEdilenFisId'dir, sadece bir referansin var olmasi yetmez.
/// </summary>
public class TersKayitIliskisiDogrulamaTests
{
    private static TersKayitIliskisi Gecerli(Action<Ayar>? ayarla = null)
    {
        var a = new Ayar();
        ayarla?.Invoke(a);
        return new TersKayitIliskisi(
            TersKayitFisId: 200,
            AsilFisId: a.AsilFisId,
            TersKayitIptalEdilenFisId: a.TersKayitIptalEdilenFisId,
            TersKayitTesisId: a.TersKayitTesisId,
            AsilFisTesisId: a.AsilFisTesisId,
            TersKayitToplamBorc: a.TersKayitToplamBorc,
            AsilFisToplamBorc: a.AsilFisToplamBorc,
            AyniAsilFiseBagliTersKayitSayisi: a.TersKayitAdedi);
    }

    private sealed class Ayar
    {
        public int? AsilFisId = 100;
        public int? TersKayitIptalEdilenFisId = 100;
        public int? TersKayitTesisId = 1;
        public int? AsilFisTesisId = 1;
        public decimal? TersKayitToplamBorc = 500m;
        public decimal? AsilFisToplamBorc = 500m;
        public int TersKayitAdedi = 1;
    }

    [Fact]
    public void GecerliIliski_DOGRULANIR()
    {
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli());

        Assert.True(sonuc.DogrulandiMi);
        Assert.Empty(sonuc.NedenKodlari);
    }

    [Fact]
    public void IliskiVerisiToplanamadi_Null_DOGRULANAMAZ()
    {
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(null);

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.TersKayitIliskisiDogrulanamadi, sonuc.NedenKodlari);
    }

    [Fact]
    public void IptalEdilenFisIdYok_AsilFisIliskisiKanitlanamaz()
    {
        // Kritik: TersKayitMuhasebeFisId dolu olsa bile, IptalEdilenFisId olmadan
        // hangi fisi terslediği kanitlanamaz.
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.TersKayitIptalEdilenFisId = null));

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.AsilFisIliskisiYok, sonuc.NedenKodlari);
    }

    [Fact]
    public void IptalEdilenFisId_BaskaFisiTersliyor_ILISKISIZ_kabul_edilmez()
    {
        // Ters kayit fisi baska bir fisi (300) tersliyor, beklenen asil fis (100) degil.
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.TersKayitIptalEdilenFisId = 300));

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.AsilFisIliskisiYok, sonuc.NedenKodlari);
    }

    [Fact]
    public void FarkliTesis_REDDEDILIR()
    {
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.TersKayitTesisId = 2));

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.FarkliTesisVeyaKurum, sonuc.NedenKodlari);
    }

    [Fact]
    public void TutarUyumsuzlugu_UYARIR()
    {
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.TersKayitToplamBorc = 400m));

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.TutarUyumsuz, sonuc.NedenKodlari);
    }

    [Fact]
    public void KucukYuvarlamaFarki_ToleransIcindeGecerli()
    {
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.TersKayitToplamBorc = 500.005m));

        Assert.True(sonuc.DogrulandiMi);
    }

    [Fact]
    public void MukerrerTersKayit_UYARIR()
    {
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.TersKayitAdedi = 2));

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.BirdenFazlaTersKayit, sonuc.NedenKodlari);
    }

    [Fact]
    public void BirdenFazlaSorun_TumNedenKodlariDoner()
    {
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a =>
        {
            a.TersKayitIptalEdilenFisId = null;
            a.TersKayitTesisId = 2;
            a.TersKayitAdedi = 3;
        }));

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.TersKayitIliskisiDogrulanamadi, sonuc.NedenKodlari);
        Assert.Contains(TersKayitIliskisiNedenKodlari.AsilFisIliskisiYok, sonuc.NedenKodlari);
        Assert.Contains(TersKayitIliskisiNedenKodlari.FarkliTesisVeyaKurum, sonuc.NedenKodlari);
        Assert.Contains(TersKayitIliskisiNedenKodlari.BirdenFazlaTersKayit, sonuc.NedenKodlari);
        // Genel ozet kodu (TersKayitIliskisiDogrulanamadi) kendi aciklamasini tasimaz, bu yuzden
        // NedenKodlari sayisi Aciklamalar sayisindan tam olarak 1 fazladir.
        Assert.Equal(sonuc.NedenKodlari.Count - 1, sonuc.Aciklamalar.Count);
    }
}
