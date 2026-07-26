using STYS.Muhasebe.NakitBankaPozisyonu.Dtos;
using STYS.Muhasebe.NakitBankaPozisyonu.Services;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;

namespace STYS.Tests;

/// <summary>
/// PosValorFinansalSiniflandirici SAF bir bilesendir (DB/EF gerektirmez) - bu yuzden finansal
/// siniflandirma kurallari burada DOGRUDAN, hizli ve eksiksiz sekilde dogrulanabilir. Kritik
/// guvence: ALLOWLIST davranisi - yalnizca acikca "normal bekleyen" sayilan tek durum toplama
/// girer, geri kalan HER SEY (tanimadigi durumlar dahil) toplamin disinda kalir.
/// </summary>
public class PosValorFinansalSiniflandiriciTests
{
    private static PosValorSiniflandirmaGirdisi Gecerli(string durum, Action<Ayar>? ayarla = null)
    {
        var a = new Ayar
        {
            Durum = durum,
            BeklenenValorTarihi = new DateOnly(2026, 7, 24),
            BrutTutar = 1000m,
            KomisyonTutari = 20m,
            NetTutar = 980m,
            ValorParaBirimi = "TRY",
            BankaHesabiParaBirimi = "TRY",
            MuhasebeFisId = durum == PosTahsilatValorDurumlari.Aktarildi ? 5 : null,
            TersKayitMuhasebeFisId = durum == PosTahsilatValorDurumlari.AktarimFisiIptalEdildi ? 9 : null,
            BankaHesabiGecerliMi = true,
            MuhasebeHesabiGecerliMi = true
        };
        ayarla?.Invoke(a);
        return new PosValorSiniflandirmaGirdisi(
            a.Durum, a.BeklenenValorTarihi, a.BrutTutar, a.KomisyonTutari, a.NetTutar,
            a.ValorParaBirimi, a.BankaHesabiParaBirimi, a.MuhasebeFisId, a.TersKayitMuhasebeFisId,
            a.BankaHesabiGecerliMi, a.MuhasebeHesabiGecerliMi);
    }

    private sealed class Ayar
    {
        public string Durum = string.Empty;
        public DateOnly BeklenenValorTarihi;
        public decimal BrutTutar;
        public decimal KomisyonTutari;
        public decimal NetTutar;
        public string? ValorParaBirimi;
        public string? BankaHesabiParaBirimi;
        public int? MuhasebeFisId;
        public int? TersKayitMuhasebeFisId;
        public bool BankaHesabiGecerliMi;
        public bool MuhasebeHesabiGecerliMi;
    }

    // ─────────────────────────────────────────────────────────────
    // Durum allowlist'i
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void ValorBekliyor_GecerliVeriyle_NormalBekleyenSayilir()
    {
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(Gecerli(PosTahsilatValorDurumlari.ValorBekliyor));

        Assert.Equal(PosValorKategori.NormalBekleyen, sonuc.Kategori);
        Assert.True(sonuc.NormalToplamaDahilMi);
    }

    [Theory]
    [InlineData(PosTahsilatValorDurumlari.MutabakatBekliyor, PosValorKategori.MutabakatBekliyor)]
    [InlineData(PosTahsilatValorDurumlari.Hata, PosValorKategori.Hatali)]
    [InlineData(PosTahsilatValorDurumlari.Iptal, PosValorKategori.IptalEdilmis)]
    [InlineData(PosTahsilatValorDurumlari.Aktariliyor, PosValorKategori.AktarimSurecinde)]
    [InlineData(PosTahsilatValorDurumlari.TersKayitOlusturuluyor, PosValorKategori.TersKayitSurecinde)]
    [InlineData(PosTahsilatValorDurumlari.Aktarildi, PosValorKategori.Aktarilmis)]
    [InlineData(PosTahsilatValorDurumlari.AktarimFisiIptalEdildi, PosValorKategori.TersKayitSurecinde)]
    public void BilinenDurumlar_DogruKategoriyeDuserVeNormalToplamaGIRMEZ(string durum, PosValorKategori beklenen)
    {
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(Gecerli(durum));

        Assert.Equal(beklenen, sonuc.Kategori);
        Assert.False(sonuc.NormalToplamaDahilMi);
    }

    [Fact]
    public void ProjedekiTumDurumlar_TekBirKategoriyeEslesir_VeYalnizcaValorBekliyorToplamaGirer()
    {
        // Bu test, projeye YENI bir durum sabiti eklendiginde bu siniflandiricinin guncellenmesi
        // gerektigini gorunur kilar - yeni durum TaninmayanDurum'a duserse burada yakalanir.
        foreach (var durum in PosTahsilatValorDurumlari.Hepsi)
        {
            var sonuc = PosValorFinansalSiniflandirici.Siniflandir(Gecerli(durum));

            Assert.NotEqual(PosValorKategori.TaninmayanDurum, sonuc.Kategori);
            Assert.Equal(durum == PosTahsilatValorDurumlari.ValorBekliyor, sonuc.NormalToplamaDahilMi);
        }
    }

    [Fact]
    public void TaninmayanDurum_NormalToplamaGIRMEZ_VeUyariUretir()
    {
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(Gecerli("GelecekteEklenenYeniDurum"));

        Assert.Equal(PosValorKategori.TaninmayanDurum, sonuc.Kategori);
        Assert.False(sonuc.NormalToplamaDahilMi);
        Assert.Equal(NakitBankaPozisyonuUyariTipleri.TaninmayanValorDurumu, sonuc.UyariTipi);
    }

    // ─────────────────────────────────────────────────────────────
    // Aktarim durumu / fis iliskisi tutarliligi
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void Aktarildi_FakatMuhasebeFisiYok_VeriKalitesiUyarisiUretir()
    {
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.Aktarildi, a => a.MuhasebeFisId = null));

        Assert.Equal(PosValorKategori.VeriKalitesiUyarisi, sonuc.Kategori);
        Assert.False(sonuc.NormalToplamaDahilMi);
        Assert.Equal(NakitBankaPozisyonuUyariTipleri.AktarimDurumuFisIliskisiTutarsiz, sonuc.UyariTipi);
    }

    [Fact]
    public void AktarimFisiIptalEdildi_FakatTersKayitFisiYok_VeriKalitesiUyarisiUretir()
    {
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.AktarimFisiIptalEdildi, a => a.TersKayitMuhasebeFisId = null));

        Assert.Equal(PosValorKategori.VeriKalitesiUyarisi, sonuc.Kategori);
        Assert.Equal(NakitBankaPozisyonuUyariTipleri.AktarimDurumuFisIliskisiTutarsiz, sonuc.UyariTipi);
    }

    [Fact]
    public void ValorBekliyor_FakatMuhasebeFisineBagli_NormalToplamaGIRMEZ()
    {
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.ValorBekliyor, a => a.MuhasebeFisId = 42));

        Assert.Equal(PosValorKategori.VeriKalitesiUyarisi, sonuc.Kategori);
        Assert.False(sonuc.NormalToplamaDahilMi);
    }

    [Fact]
    public void ValorBekliyor_FakatTersKayitFisineBagli_NormalToplamaGIRMEZ()
    {
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.ValorBekliyor, a => a.TersKayitMuhasebeFisId = 7));

        Assert.Equal(PosValorKategori.VeriKalitesiUyarisi, sonuc.Kategori);
        Assert.False(sonuc.NormalToplamaDahilMi);
    }

    // ─────────────────────────────────────────────────────────────
    // Baglanti gecerliligi
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void BankaHesabiGecersiz_NormalToplamaGIRMEZ()
    {
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.ValorBekliyor, a => a.BankaHesabiGecerliMi = false));

        Assert.False(sonuc.NormalToplamaDahilMi);
        Assert.Equal(NakitBankaPozisyonuUyariTipleri.BankaHesabiBulunamadiVeyaPasif, sonuc.UyariTipi);
    }

    [Fact]
    public void MuhasebeHesabiGecersiz_NormalToplamaGIRMEZ()
    {
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.ValorBekliyor, a => a.MuhasebeHesabiGecerliMi = false));

        Assert.False(sonuc.NormalToplamaDahilMi);
        Assert.Equal(NakitBankaPozisyonuUyariTipleri.BankaHesabininMuhasebeBaglantisiGecersiz, sonuc.UyariTipi);
    }

    // ─────────────────────────────────────────────────────────────
    // Para birimi ve tutar dogrulamasi
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void ParaBirimiUyusmuyor_NormalToplamaGIRMEZ()
    {
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.ValorBekliyor, a => a.ValorParaBirimi = "USD"));

        Assert.False(sonuc.NormalToplamaDahilMi);
        Assert.Equal(NakitBankaPozisyonuUyariTipleri.ParaBirimiUyusmuyor, sonuc.UyariTipi);
    }

    [Fact]
    public void ParaBirimiBos_NormalToplamaGIRMEZ()
    {
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.ValorBekliyor, a => a.ValorParaBirimi = null));

        Assert.False(sonuc.NormalToplamaDahilMi);
        Assert.Equal(NakitBankaPozisyonuUyariTipleri.ParaBirimiUyusmuyor, sonuc.UyariTipi);
    }

    [Fact]
    public void ValorTarihiBos_NormalToplamaGIRMEZ()
    {
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.ValorBekliyor, a => a.BeklenenValorTarihi = default));

        Assert.False(sonuc.NormalToplamaDahilMi);
        Assert.Equal(NakitBankaPozisyonuUyariTipleri.ValorTarihiBos, sonuc.UyariTipi);
    }

    [Fact]
    public void NetTutarBrutEksiKomisyonaEsitDegil_NormalToplamaGIRMEZ()
    {
        // 1000 - 20 = 980 olmali; 900 verildiginde tutarsizlik yakalanmali.
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.ValorBekliyor, a => a.NetTutar = 900m));

        Assert.False(sonuc.NormalToplamaDahilMi);
        Assert.Equal(NakitBankaPozisyonuUyariTipleri.NetVeyaKomisyonBilgisiEksik, sonuc.UyariTipi);
    }

    [Fact]
    public void KurusFarki_ToleransIcinde_NormalBekleyenSayilir()
    {
        // 1000.00 - 20.005 ~ 979.99/980.00 gibi yuvarlama farklari toleransla (0.01) kabul edilir.
        var sonuc = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.ValorBekliyor, a => a.NetTutar = 979.99m));

        Assert.Equal(PosValorKategori.NormalBekleyen, sonuc.Kategori);
    }

    [Fact]
    public void NetTutarSifirVeyaNegatif_NormalToplamaGIRMEZ()
    {
        var sifir = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.ValorBekliyor, a => { a.BrutTutar = 0m; a.KomisyonTutari = 0m; a.NetTutar = 0m; }));
        var negatif = PosValorFinansalSiniflandirici.Siniflandir(
            Gecerli(PosTahsilatValorDurumlari.ValorBekliyor, a => { a.BrutTutar = 0m; a.KomisyonTutari = 50m; a.NetTutar = -50m; }));

        Assert.False(sifir.NormalToplamaDahilMi);
        Assert.False(negatif.NormalToplamaDahilMi);
    }
}
