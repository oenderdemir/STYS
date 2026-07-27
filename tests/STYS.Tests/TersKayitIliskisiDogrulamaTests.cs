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
            TersKayitKurumId: a.TersKayitKurumId,
            AsilFisKurumId: a.AsilFisKurumId,
            TersKayitToplamBorc: a.TersKayitToplamBorc,
            AsilFisToplamBorc: a.AsilFisToplamBorc,
            TersKayitParaBirimi: a.TersKayitParaBirimi,
            AsilFisParaBirimi: a.AsilFisParaBirimi,
            TersYonluHesapEtkisiUyumluMu: a.TersYonluHesapEtkisiUyumluMu,
            AyniAsilFiseBagliTersKayitSayisi: a.TersKayitAdedi);
    }

    private sealed class Ayar
    {
        public int? AsilFisId = 100;
        public int? TersKayitIptalEdilenFisId = 100;
        public int? TersKayitTesisId = 1;
        public int? AsilFisTesisId = 1;
        public int? TersKayitKurumId = 10;
        public int? AsilFisKurumId = 10;
        public decimal? TersKayitToplamBorc = 500m;
        public decimal? AsilFisToplamBorc = 500m;
        public string? TersKayitParaBirimi = "TRY";
        public string? AsilFisParaBirimi = "TRY";
        public bool? TersYonluHesapEtkisiUyumluMu = true;
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
    public void AsilFisIdBilinmiyor_IptalEdilenFisIdDoluOlsaBileGECERSIZ()
    {
        // Kritik (madde 7): asil fis ID'si bilinmiyorsa, ters fiste HERHANGI BIR IptalEdilenFisId
        // bulunmasi TEK BASINA gecerli iliski sayilmaz.
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.AsilFisId = null));

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.AsilFisBilinmiyor, sonuc.NedenKodlari);
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
        Assert.Contains(TersKayitIliskisiNedenKodlari.TersKayitTesisUyusmazligi, sonuc.NedenKodlari);
    }

    [Fact]
    public void FarkliKurum_REDDEDILIR()
    {
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.TersKayitKurumId = 20));

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.TersKayitKurumUyusmazligi, sonuc.NedenKodlari);
    }

    [Fact]
    public void TutarUyumsuzlugu_UYARIR()
    {
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.TersKayitToplamBorc = 400m));

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.TersKayitTutarUyusmazligi, sonuc.NedenKodlari);
    }

    [Fact]
    public void KucukYuvarlamaFarki_ToleransIcindeGecerli()
    {
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.TersKayitToplamBorc = 500.005m));

        Assert.True(sonuc.DogrulandiMi);
    }

    [Fact]
    public void ParaBirimiUyumsuzlugu_UYARIR()
    {
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.TersKayitParaBirimi = "USD"));

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.TersKayitParaBirimiUyusmazligi, sonuc.NedenKodlari);
    }

    [Fact]
    public void TersYonluHesapEtkisiDogrulanamadi_Null_GECERSIZ()
    {
        // Kritik: veri modeli ters yonlu hesap etkisini KANITLAYAMIYORSA "dogrulandi" URETILMEZ.
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.TersYonluHesapEtkisiUyumluMu = null));

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.TersYonluHesapEtkisiDogrulanamadi, sonuc.NedenKodlari);
    }

    [Fact]
    public void TersYonluHesapEtkisiUyumsuz_False_GECERSIZ()
    {
        var sonuc = TersKayitIliskisiDogrulama.Degerlendir(Gecerli(a => a.TersYonluHesapEtkisiUyumluMu = false));

        Assert.False(sonuc.DogrulandiMi);
        Assert.Contains(TersKayitIliskisiNedenKodlari.TersYonluHesapEtkisiDogrulanamadi, sonuc.NedenKodlari);
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
        Assert.Contains(TersKayitIliskisiNedenKodlari.TersKayitTesisUyusmazligi, sonuc.NedenKodlari);
        Assert.Contains(TersKayitIliskisiNedenKodlari.BirdenFazlaTersKayit, sonuc.NedenKodlari);
        // Genel ozet kodu (TersKayitIliskisiDogrulanamadi) kendi aciklamasini tasimaz, bu yuzden
        // NedenKodlari sayisi Aciklamalar sayisindan tam olarak 1 fazladir.
        Assert.Equal(sonuc.NedenKodlari.Count - 1, sonuc.Aciklamalar.Count);
    }
}
