using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;

namespace STYS.Tests;

/// <summary>
/// MuhasebeFisDogrulama SAF bir bilesendir - "MuhasebeFisId dolu oldugu icin fis gecerlidir"
/// varsayiminin yapilmadigini dogrudan ve hizlica dogrular.
/// </summary>
public class MuhasebeFisDogrulamaTests
{
    private static DogrulanmisFis Gecerli(Action<Ayar>? ayarla = null)
    {
        var a = new Ayar();
        ayarla?.Invoke(a);
        return new DogrulanmisFis(
            FisId: 100, Bulundu: a.Bulundu, SoftDeleteEdilmis: a.SoftDelete, Durum: a.Durum,
            TesisId: a.TesisId, MaliYil: 2026, Donem: 7, FisTarihi: a.FisTarihi,
            BeklenenKasaBankaHesabiEtkilenmisMi: a.HesapEtkilenmis);
    }

    private sealed class Ayar
    {
        public bool Bulundu = true;
        public bool SoftDelete;
        public string? Durum = MuhasebeFisDurumlari.Onayli;
        public int? TesisId = 1;
        public DateTime? FisTarihi = new(2026, 7, 15);
        public bool? HesapEtkilenmis = true;
    }

    [Fact]
    public void GecerliFis_GecerliSayilir()
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(Gecerli(), beklenenTesisId: 1, kasaBankaHesabiKontrolEdilsinMi: true);

        Assert.True(sonuc.GecerliMi);
        Assert.Empty(sonuc.NedenKodlari);
    }

    [Fact]
    public void FisNull_YaniIdDoluAmaFisBulunamadi_GECERSIZ()
    {
        // Kritik regresyon: MuhasebeFisId dolu oldugu halde fis bulunamiyorsa GECERLI SAYILMAMALI.
        var sonuc = MuhasebeFisDogrulama.Degerlendir(null);

        Assert.False(sonuc.GecerliMi);
        Assert.Contains(FisGecersizlikNedenKodlari.FisBulunamadi, sonuc.NedenKodlari);
    }

    [Fact]
    public void FisBulunamadi_GECERSIZ()
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(Gecerli(a => a.Bulundu = false));

        Assert.False(sonuc.GecerliMi);
        Assert.Contains(FisGecersizlikNedenKodlari.FisBulunamadi, sonuc.NedenKodlari);
    }

    [Fact]
    public void SoftDeleteEdilmisFis_GECERSIZ()
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(Gecerli(a => a.SoftDelete = true));

        Assert.False(sonuc.GecerliMi);
        Assert.Contains(FisGecersizlikNedenKodlari.FisSoftDeleteEdilmis, sonuc.NedenKodlari);
    }

    [Theory]
    [InlineData(MuhasebeFisDurumlari.Taslak)]
    [InlineData(MuhasebeFisDurumlari.Iptal)]
    public void MaliEtkiOlusturmayanDurum_GECERSIZ(string durum)
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(Gecerli(a => a.Durum = durum));

        Assert.False(sonuc.GecerliMi);
        Assert.Contains(FisGecersizlikNedenKodlari.FisDurumuMaliEtkiOlusturmuyor, sonuc.NedenKodlari);
    }

    [Theory]
    [InlineData(MuhasebeFisDurumlari.Onayli)]
    [InlineData(MuhasebeFisDurumlari.TersKayit)]
    public void MaliEtkiOlusturanDurumlar_GECERLI(string durum)
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(Gecerli(a => a.Durum = durum));

        Assert.True(sonuc.GecerliMi);
    }

    [Fact]
    public void FarkliTesiseAitFis_GECERSIZ()
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(Gecerli(a => a.TesisId = 99), beklenenTesisId: 1);

        Assert.False(sonuc.GecerliMi);
        Assert.Contains(FisGecersizlikNedenKodlari.FisFarkliTesiseAit, sonuc.NedenKodlari);
    }

    [Fact]
    public void DonemDisiFisTarihi_GECERSIZ()
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(
            Gecerli(a => a.FisTarihi = new DateTime(2026, 9, 1)),
            donemBaslangic: new DateTime(2026, 7, 1),
            donemBitis: new DateTime(2026, 7, 31));

        Assert.False(sonuc.GecerliMi);
        Assert.Contains(FisGecersizlikNedenKodlari.FisDonemiUyumsuz, sonuc.NedenKodlari);
    }

    [Fact]
    public void FisSatirindaBeklenenHesapEtkilenmemis_GECERSIZ()
    {
        // Fis var, aktif, dogru tesiste - AMA hicbir satiri beklenen kasa/banka hesabini
        // etkilemiyor: baska bir hesaba islenmis olabilir.
        var sonuc = MuhasebeFisDogrulama.Degerlendir(
            Gecerli(a => a.HesapEtkilenmis = false), beklenenTesisId: 1, kasaBankaHesabiKontrolEdilsinMi: true);

        Assert.False(sonuc.GecerliMi);
        Assert.Contains(FisGecersizlikNedenKodlari.FisSatirindaBeklenenHesapYok, sonuc.NedenKodlari);
    }

    [Fact]
    public void HesapKontroluKapaliysa_HesapEtkilenmemisOlsaBileGecerli()
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(
            Gecerli(a => a.HesapEtkilenmis = false), beklenenTesisId: 1, kasaBankaHesabiKontrolEdilsinMi: false);

        Assert.True(sonuc.GecerliMi);
    }

    // ─────────────────────────────────────────────────────────────
    // Mali yil / donem / nullable hesap kontrolu (madde 6)
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void FarkliMaliYil_GECERSIZ()
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(Gecerli(), beklenenMaliYil: 2025);

        Assert.False(sonuc.GecerliMi);
        Assert.Contains(FisGecersizlikNedenKodlari.FisMaliYiliUyumsuz, sonuc.NedenKodlari);
    }

    [Fact]
    public void FarkliDonem_GECERSIZ()
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(Gecerli(), beklenenDonem: 3);

        Assert.False(sonuc.GecerliMi);
        Assert.Contains(FisGecersizlikNedenKodlari.FisDonemNoUyumsuz, sonuc.NedenKodlari);
    }

    [Fact]
    public void AyniMaliYilVeDonem_GECERLI()
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(Gecerli(), beklenenMaliYil: 2026, beklenenDonem: 7);

        Assert.True(sonuc.GecerliMi);
    }

    [Fact]
    public void BeklenenTesisDoluAmaFisTesisiNull_GECERSIZ()
    {
        // Kritik: "farkli degil" diye sessizce gecerli SAYILMAMALI.
        var sonuc = MuhasebeFisDogrulama.Degerlendir(Gecerli(a => a.TesisId = null), beklenenTesisId: 1);

        Assert.False(sonuc.GecerliMi);
        Assert.Contains(FisGecersizlikNedenKodlari.FisTesisiBelirsiz, sonuc.NedenKodlari);
    }

    [Fact]
    public void HesapKontroluZorunluAmaSonucNull_BASARILI_SAYILMAZ()
    {
        // Kritik: `== false` yerine acik `true` sarti - null "dogrulanamadi" demektir.
        var sonuc = MuhasebeFisDogrulama.Degerlendir(
            Gecerli(a => a.HesapEtkilenmis = null), kasaBankaHesabiKontrolEdilsinMi: true);

        Assert.False(sonuc.GecerliMi);
        Assert.Contains(FisGecersizlikNedenKodlari.FisHesapKontroluYapilamadi, sonuc.NedenKodlari);
    }

    [Fact]
    public void DonemAraligiVerildiFakatFisTarihiYok_GECERSIZ()
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(
            Gecerli(a => a.FisTarihi = null),
            donemBaslangic: new DateTime(2026, 7, 1),
            donemBitis: new DateTime(2026, 7, 31));

        Assert.False(sonuc.GecerliMi);
        Assert.Contains(FisGecersizlikNedenKodlari.FisTarihiYok, sonuc.NedenKodlari);
    }

    [Fact]
    public void BirdenFazlaSorun_TumNedenKodlariDoner()
    {
        var sonuc = MuhasebeFisDogrulama.Degerlendir(
            Gecerli(a => { a.SoftDelete = true; a.Durum = MuhasebeFisDurumlari.Iptal; a.TesisId = 99; }),
            beklenenTesisId: 1);

        Assert.False(sonuc.GecerliMi);
        Assert.Equal(3, sonuc.NedenKodlari.Count);
        Assert.Equal(sonuc.NedenKodlari.Count, sonuc.Aciklamalar.Count);
    }
}
