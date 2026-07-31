using STYS.Muhasebe.SatisBelgeleri;
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
}
