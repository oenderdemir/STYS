using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// SatisBelgesiDurumProjection'ın saf eşleme mantığını doğrulayan, DB GEREKTİRMEYEN birim
/// testleri. Bu sınıf hiçbir DB/servis bağımlılığı içermediğinden testler doğrudan statik
/// metotları çağırır - IntegrationFact/SQL Server gerekmez.
/// </summary>
public class SatisBelgesiDurumProjectionTests
{
    // ─────────────────────────────────────────────────────────────
    // ProjeTicariDurum — 7 eski SatisBelgesiDurumu değerinin TAMAMI
    // ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SatisBelgesiDurumu.Taslak, TicariBelgeDurumu.Taslak)]
    [InlineData(SatisBelgesiDurumu.MuhasebeOnayinda, TicariBelgeDurumu.Hazir)]
    [InlineData(SatisBelgesiDurumu.MuhasebeOnaylandi, TicariBelgeDurumu.Hazir)]
    [InlineData(SatisBelgesiDurumu.Reddedildi, TicariBelgeDurumu.Hazir)]
    [InlineData(SatisBelgesiDurumu.FaturaKesildi, TicariBelgeDurumu.Hazir)]
    [InlineData(SatisBelgesiDurumu.MusteriyeGonderildi, TicariBelgeDurumu.Hazir)]
    [InlineData(SatisBelgesiDurumu.IptalEdildi, TicariBelgeDurumu.IptalEdildi)]
    public void ProjeTicariDurum_YediEskiDegerinTamamiDogruEslenir(SatisBelgesiDurumu durum, TicariBelgeDurumu beklenen)
    {
        Assert.Equal(beklenen, SatisBelgesiDurumProjection.ProjeTicariDurum(durum));
    }

    [Fact]
    public void ProjeTicariDurum_MuhasebeReddiTicariBelgeReddiSayilmaz()
    {
        // Muhasebe reddi (SatisBelgesiDurumu.Reddedildi) TicariBelgeDurumu'nda bir "Reddedildi"
        // değeri OLUŞTURMAZ - ticari belge Hazir kabul edilir; ret yalnızca
        // TicariBelgeMuhasebeDurumu.Reddedildi ile ifade edilir.
        Assert.Equal(TicariBelgeDurumu.Hazir, SatisBelgesiDurumProjection.ProjeTicariDurum(SatisBelgesiDurumu.Reddedildi));
        Assert.DoesNotContain("Reddedildi", Enum.GetNames<TicariBelgeDurumu>());
    }

    // ─────────────────────────────────────────────────────────────
    // ProjeMuhasebeDurumu — 7 eski SatisBelgesiDurumu değerinin TAMAMI
    // ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SatisBelgesiDurumu.Taslak, TicariBelgeMuhasebeDurumu.Bekliyor)]
    [InlineData(SatisBelgesiDurumu.MuhasebeOnayinda, TicariBelgeMuhasebeDurumu.Onayda)]
    [InlineData(SatisBelgesiDurumu.MuhasebeOnaylandi, TicariBelgeMuhasebeDurumu.Onaylandi)]
    [InlineData(SatisBelgesiDurumu.Reddedildi, TicariBelgeMuhasebeDurumu.Reddedildi)]
    [InlineData(SatisBelgesiDurumu.FaturaKesildi, TicariBelgeMuhasebeDurumu.Onaylandi)]
    [InlineData(SatisBelgesiDurumu.MusteriyeGonderildi, TicariBelgeMuhasebeDurumu.Onaylandi)]
    [InlineData(SatisBelgesiDurumu.IptalEdildi, TicariBelgeMuhasebeDurumu.IptalEdildi)]
    public void ProjeMuhasebeDurumu_YediEskiDegerinTamamiDogruEslenir(SatisBelgesiDurumu durum, TicariBelgeMuhasebeDurumu beklenen)
    {
        Assert.Equal(beklenen, SatisBelgesiDurumProjection.ProjeMuhasebeDurumu(durum));
    }

    [Fact]
    public void ProjeMuhasebeDurumu_BilinmeyenDeger_FailClosedFirlatir()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => SatisBelgesiDurumProjection.ProjeMuhasebeDurumu((SatisBelgesiDurumu)999));
        Assert.Contains("999", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // ProjeFaturalamaDurumu — belge tipi x durum matrisi
    // ─────────────────────────────────────────────────────────────

    // StysTarafindanDuzenlenirMi == true (SatisFaturasi, AlisIadeFaturasi): faturalama yönü UYGULANIR.
    [Theory]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, SatisBelgesiDurumu.Taslak, TicariBelgeFaturalamaDurumu.Baslatilmadi)]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, SatisBelgesiDurumu.MuhasebeOnayinda, TicariBelgeFaturalamaDurumu.Baslatilmadi)]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, SatisBelgesiDurumu.Reddedildi, TicariBelgeFaturalamaDurumu.Baslatilmadi)]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, SatisBelgesiDurumu.MuhasebeOnaylandi, TicariBelgeFaturalamaDurumu.KesimBekliyor)]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, SatisBelgesiDurumu.FaturaKesildi, TicariBelgeFaturalamaDurumu.Kesildi)]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, SatisBelgesiDurumu.MusteriyeGonderildi, TicariBelgeFaturalamaDurumu.MusteriyeGonderildi)]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, SatisBelgesiDurumu.IptalEdildi, TicariBelgeFaturalamaDurumu.IptalEdildi)]
    [InlineData(SatisBelgesiTipi.AlisIadeFaturasi, SatisBelgesiDurumu.Taslak, TicariBelgeFaturalamaDurumu.Baslatilmadi)]
    [InlineData(SatisBelgesiTipi.AlisIadeFaturasi, SatisBelgesiDurumu.MuhasebeOnaylandi, TicariBelgeFaturalamaDurumu.KesimBekliyor)]
    [InlineData(SatisBelgesiTipi.AlisIadeFaturasi, SatisBelgesiDurumu.FaturaKesildi, TicariBelgeFaturalamaDurumu.Kesildi)]
    [InlineData(SatisBelgesiTipi.AlisIadeFaturasi, SatisBelgesiDurumu.MusteriyeGonderildi, TicariBelgeFaturalamaDurumu.MusteriyeGonderildi)]
    [InlineData(SatisBelgesiTipi.AlisIadeFaturasi, SatisBelgesiDurumu.IptalEdildi, TicariBelgeFaturalamaDurumu.IptalEdildi)]
    // StysTarafindanDuzenlenirMi == false (AlisFaturasi, SatisIadeFaturasi, legacy IadeFaturasi,
    // Proforma, FaturaTaslagi): faturalama süreci daima Uygulanamaz - TEK istisna, mevcut durum
    // geçmişinin (FaturaKesildi/MusteriyeGonderildi/IptalEdildi) ÖNCELİKLİ korunmasıdır.
    [InlineData(SatisBelgesiTipi.AlisFaturasi, SatisBelgesiDurumu.Taslak, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    [InlineData(SatisBelgesiTipi.AlisFaturasi, SatisBelgesiDurumu.MuhasebeOnaylandi, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    [InlineData(SatisBelgesiTipi.AlisFaturasi, SatisBelgesiDurumu.IptalEdildi, TicariBelgeFaturalamaDurumu.IptalEdildi)] // öncelik istisnası
    [InlineData(SatisBelgesiTipi.SatisIadeFaturasi, SatisBelgesiDurumu.Taslak, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    [InlineData(SatisBelgesiTipi.SatisIadeFaturasi, SatisBelgesiDurumu.MuhasebeOnaylandi, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    [InlineData(SatisBelgesiTipi.SatisIadeFaturasi, SatisBelgesiDurumu.IptalEdildi, TicariBelgeFaturalamaDurumu.IptalEdildi)] // öncelik istisnası
    [InlineData(SatisBelgesiTipi.IadeFaturasi, SatisBelgesiDurumu.Taslak, TicariBelgeFaturalamaDurumu.Uygulanamaz)] // legacy - tahmin edilmez
    [InlineData(SatisBelgesiTipi.IadeFaturasi, SatisBelgesiDurumu.MuhasebeOnaylandi, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    [InlineData(SatisBelgesiTipi.IadeFaturasi, SatisBelgesiDurumu.IptalEdildi, TicariBelgeFaturalamaDurumu.IptalEdildi)]
    [InlineData(SatisBelgesiTipi.Proforma, SatisBelgesiDurumu.Taslak, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    [InlineData(SatisBelgesiTipi.Proforma, SatisBelgesiDurumu.MuhasebeOnaylandi, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    [InlineData(SatisBelgesiTipi.FaturaTaslagi, SatisBelgesiDurumu.Taslak, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    public void ProjeFaturalamaDurumu_BelgeTipiVeDurumMatrisiDogruEslenir(
        SatisBelgesiTipi belgeTipi, SatisBelgesiDurumu durum, TicariBelgeFaturalamaDurumu beklenen)
    {
        Assert.Equal(beklenen, SatisBelgesiDurumProjection.ProjeFaturalamaDurumu(belgeTipi, durum));
    }

    // ─────────────────────────────────────────────────────────────
    // Proje(...) — üç projeksiyonu tek çağrıda üretir
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void Proje_UcAlaniTekCagridaTutarliUretir()
    {
        var (ticari, muhasebe, faturalama) = SatisBelgesiDurumProjection.Proje(
            SatisBelgesiTipi.SatisFaturasi, SatisBelgesiDurumu.MuhasebeOnaylandi);

        Assert.Equal(TicariBelgeDurumu.Hazir, ticari);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, muhasebe);
        Assert.Equal(TicariBelgeFaturalamaDurumu.KesimBekliyor, faturalama);
    }

    // ─────────────────────────────────────────────────────────────
    // ProjeLegacyDurum — OTORİTER "yeni → eski" yön. Görev tanımındaki TÜM (7) geçerli kombinasyon
    // (FaturalamaDurumu'nun Baslatilmadi/Uygulanamaz ve KesimBekliyor/Uygulanamaz ikili kabul
    // ettiği kollar dahil, toplam 11 kabul edilen üçlü) + fail-closed exception.
    // ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TicariBelgeDurumu.Taslak, TicariBelgeMuhasebeDurumu.Bekliyor, TicariBelgeFaturalamaDurumu.Baslatilmadi, SatisBelgesiDurumu.Taslak)]
    [InlineData(TicariBelgeDurumu.Taslak, TicariBelgeMuhasebeDurumu.Bekliyor, TicariBelgeFaturalamaDurumu.Uygulanamaz, SatisBelgesiDurumu.Taslak)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onayda, TicariBelgeFaturalamaDurumu.Baslatilmadi, SatisBelgesiDurumu.MuhasebeOnayinda)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onayda, TicariBelgeFaturalamaDurumu.Uygulanamaz, SatisBelgesiDurumu.MuhasebeOnayinda)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onaylandi, TicariBelgeFaturalamaDurumu.KesimBekliyor, SatisBelgesiDurumu.MuhasebeOnaylandi)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onaylandi, TicariBelgeFaturalamaDurumu.Uygulanamaz, SatisBelgesiDurumu.MuhasebeOnaylandi)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Reddedildi, TicariBelgeFaturalamaDurumu.Baslatilmadi, SatisBelgesiDurumu.Reddedildi)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Reddedildi, TicariBelgeFaturalamaDurumu.Uygulanamaz, SatisBelgesiDurumu.Reddedildi)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onaylandi, TicariBelgeFaturalamaDurumu.Kesildi, SatisBelgesiDurumu.FaturaKesildi)]
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onaylandi, TicariBelgeFaturalamaDurumu.MusteriyeGonderildi, SatisBelgesiDurumu.MusteriyeGonderildi)]
    [InlineData(TicariBelgeDurumu.IptalEdildi, TicariBelgeMuhasebeDurumu.IptalEdildi, TicariBelgeFaturalamaDurumu.IptalEdildi, SatisBelgesiDurumu.IptalEdildi)]
    public void ProjeLegacyDurum_GecerliTumKombinasyonlarDogruEslenir(
        TicariBelgeDurumu ticari, TicariBelgeMuhasebeDurumu muhasebe, TicariBelgeFaturalamaDurumu faturalama, SatisBelgesiDurumu beklenen)
    {
        Assert.Equal(beklenen, SatisBelgesiDurumProjection.ProjeLegacyDurum(ticari, muhasebe, faturalama));
    }

    [Theory]
    [InlineData(TicariBelgeDurumu.Taslak, TicariBelgeMuhasebeDurumu.Onayda, TicariBelgeFaturalamaDurumu.Baslatilmadi)] // Taslak asla Onayda ile bir arada olamaz
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Bekliyor, TicariBelgeFaturalamaDurumu.Uygulanamaz)] // Hazir + Bekliyor tanımsız
    [InlineData(TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onaylandi, TicariBelgeFaturalamaDurumu.Baslatilmadi)] // Onaylandi + Baslatilmadi tanımsız (KesimBekliyor/Uygulanamaz olmalı)
    [InlineData(TicariBelgeDurumu.IptalEdildi, TicariBelgeMuhasebeDurumu.Onaylandi, TicariBelgeFaturalamaDurumu.IptalEdildi)] // IptalEdildi kısmi/çelişkili
    public void ProjeLegacyDurum_TanimsizVeyaCelisikKombinasyon_FailClosedFirlatir(
        TicariBelgeDurumu ticari, TicariBelgeMuhasebeDurumu muhasebe, TicariBelgeFaturalamaDurumu faturalama)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => SatisBelgesiDurumProjection.ProjeLegacyDurum(ticari, muhasebe, faturalama));
        Assert.Contains("Tanımsız veya çelişkili", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // OtoriterDurumlariAta — ÜRETİMİN kullandığı tek atomik yazım yardımcısı
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void OtoriterDurumlariAta_GecerliKombinasyondaDortAlaniBirlikteAtar()
    {
        var belge = new SatisBelgesi();

        SatisBelgesiDurumProjection.OtoriterDurumlariAta(
            belge, TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onaylandi, TicariBelgeFaturalamaDurumu.Kesildi);

        Assert.Equal(TicariBelgeDurumu.Hazir, belge.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, belge.MuhasebeDurumu);
        Assert.Equal(TicariBelgeFaturalamaDurumu.Kesildi, belge.FaturalamaDurumu);
        Assert.Equal(SatisBelgesiDurumu.FaturaKesildi, belge.Durum);
    }

    [Fact]
    public void OtoriterDurumlariAta_GecersizKombinasyondaHicbirAlanaDokunmadanFirlatir()
    {
        var belge = new SatisBelgesi
        {
            TicariDurum = TicariBelgeDurumu.Taslak,
            MuhasebeDurumu = TicariBelgeMuhasebeDurumu.Bekliyor,
            FaturalamaDurumu = TicariBelgeFaturalamaDurumu.Baslatilmadi,
            Durum = SatisBelgesiDurumu.Taslak
        };

        Assert.Throws<InvalidOperationException>(() => SatisBelgesiDurumProjection.OtoriterDurumlariAta(
            belge, TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Bekliyor, TicariBelgeFaturalamaDurumu.Uygulanamaz));

        // Geçersiz kombinasyon ÖNCE doğrulanır (legacy Durum hesaplanırken) - dört alandan
        // HİÇBİRİ kısmen/yarım atanmış olarak KALMAMALIDIR.
        Assert.Equal(TicariBelgeDurumu.Taslak, belge.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Bekliyor, belge.MuhasebeDurumu);
        Assert.Equal(TicariBelgeFaturalamaDurumu.Baslatilmadi, belge.FaturalamaDurumu);
        Assert.Equal(SatisBelgesiDurumu.Taslak, belge.Durum);
    }

    // ─────────────────────────────────────────────────────────────
    // ProjeBaslangicFaturalamaDurumu / ProjeOnaylandiFaturalamaDurumu — belge tipine göre
    // ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, TicariBelgeFaturalamaDurumu.Baslatilmadi)]
    [InlineData(SatisBelgesiTipi.AlisIadeFaturasi, TicariBelgeFaturalamaDurumu.Baslatilmadi)]
    [InlineData(SatisBelgesiTipi.AlisFaturasi, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    [InlineData(SatisBelgesiTipi.SatisIadeFaturasi, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    [InlineData(SatisBelgesiTipi.Proforma, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    public void ProjeBaslangicFaturalamaDurumu_BelgeTipineGoreDogruDoner(SatisBelgesiTipi belgeTipi, TicariBelgeFaturalamaDurumu beklenen)
    {
        Assert.Equal(beklenen, SatisBelgesiDurumProjection.ProjeBaslangicFaturalamaDurumu(belgeTipi));
    }

    [Theory]
    [InlineData(SatisBelgesiTipi.SatisFaturasi, TicariBelgeFaturalamaDurumu.KesimBekliyor)]
    [InlineData(SatisBelgesiTipi.AlisIadeFaturasi, TicariBelgeFaturalamaDurumu.KesimBekliyor)]
    [InlineData(SatisBelgesiTipi.AlisFaturasi, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    [InlineData(SatisBelgesiTipi.SatisIadeFaturasi, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    [InlineData(SatisBelgesiTipi.Proforma, TicariBelgeFaturalamaDurumu.Uygulanamaz)]
    public void ProjeOnaylandiFaturalamaDurumu_BelgeTipineGoreDogruDoner(SatisBelgesiTipi belgeTipi, TicariBelgeFaturalamaDurumu beklenen)
    {
        Assert.Equal(beklenen, SatisBelgesiDurumProjection.ProjeOnaylandiFaturalamaDurumu(belgeTipi));
    }
}
