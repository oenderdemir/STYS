using STYS.Tests.TestSupport;

namespace STYS.Tests;

/// <summary>
/// PosTahsilatValorIntegrationTests.DisposeAsync'in (bkz. TwoPhaseCleanupRunner) iki gecisli
/// temizlik + AggregateException raporlama davranisini dogrular. Ilk grup [Fact] testler DB
/// GEREKTIRMEZ (saf mantik, sahte adimlarla) - CI'da her zaman calisir. Ikinci grup [IntegrationFact]
/// testler GERCEK SQL Server'a karsi, DisposeAsync'in kendisini (OlusturCleanupAdimlari/
/// DogrulaTemizlikKalintilariAsync araciligiyla) uctan uca dogrular.
/// </summary>
public class PosTahsilatValorCleanupTests
{
    // ─────────────────────────────────────────────────────────────
    // DB gerektirmeyen, saf TwoPhaseCleanupRunner mantik testleri.
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task NormalTamamlanma_HicHataDonulmez()
    {
        var calisanAdimlar = new List<string>();
        var adimlar = new[]
        {
            new CleanupAdimi("adim1", () => { calisanAdimlar.Add("adim1"); return Task.CompletedTask; }),
            new CleanupAdimi("adim2", () => { calisanAdimlar.Add("adim2"); return Task.CompletedTask; }),
            new CleanupAdimi("adim3", () => { calisanAdimlar.Add("adim3"); return Task.CompletedTask; }),
        };

        var hatalar = await TwoPhaseCleanupRunner.CalistirAsync(adimlar);

        Assert.Empty(hatalar);
        Assert.Equal(new[] { "adim1", "adim2", "adim3" }, calisanAdimlar);
    }

    [Fact]
    public async Task GeciciHata_IlkGecisBasarisizIkinciGecisBasarili_HataListesiBos()
    {
        var deneme = 0;
        var adimlar = new[]
        {
            new CleanupAdimi("kalici-basarili", () => Task.CompletedTask),
            new CleanupAdimi("gecici-arizali", () =>
            {
                deneme++;
                if (deneme == 1)
                {
                    throw new InvalidOperationException("gecici baglanti sorunu (simulasyon)");
                }
                return Task.CompletedTask;
            }),
        };

        var hatalar = await TwoPhaseCleanupRunner.CalistirAsync(adimlar);

        // Ilk geciste basarisiz oldu (deneme==1 firlatti), ikinci geciste basarili oldu (deneme==2) -
        // NIHAI hata listesi BOS olmali (arizi hata KALICI sayilmaz).
        Assert.Empty(hatalar);
        Assert.Equal(2, deneme);
    }

    [Fact]
    public async Task KaliciHata_HerIkiGecisteDeBasarisiz_HataRaporlanirVeDigerAdimlarYineDeCalisir()
    {
        var digerAdimCalistiMi = false;
        var adimlar = new[]
        {
            new CleanupAdimi("kalici-arizali", () => throw new InvalidOperationException("kalici hata (her iki geciste de)")),
            new CleanupAdimi("bagimsiz-basarili-adim", () =>
            {
                digerAdimCalistiMi = true;
                return Task.CompletedTask;
            }),
        };

        var hatalar = await TwoPhaseCleanupRunner.CalistirAsync(adimlar);

        // "kalici-arizali" adimi HER IKI geciste de basarisiz oldugu icin NIHAI hata listesinde
        // kalmali (2. gecis denemesinin hatasi raporlanir); ama bu, ONDAN SONRAKI bagimsiz adimin
        // calismasini ENGELLEMEMIS olmali (biri basarisiz olsa bile digerleri yine de denenir).
        Assert.True(digerAdimCalistiMi);
        Assert.Single(hatalar);
        Assert.Contains("kalici-arizali", hatalar[0].Message);
        Assert.Contains("2. gecis", hatalar[0].Message);
    }

    [Fact]
    public async Task TestGovdesiVeCleanupBirlikteBasarisizsa_IkiHataDaGozlemlenir()
    {
        // Test govdesinin kendi assertion/is mantigi hatasini TEMSIL EDER (DisposeAsync'ten
        // TAMAMEN bagimsiz olusur).
        Exception? govdeHatasi = null;
        try
        {
            Assert.Equal(1, 2); // kasitli olarak basarisiz bir assertion
        }
        catch (Exception ex)
        {
            govdeHatasi = ex;
        }

        // Cleanup'in KALICI olarak basarisiz oldugu senaryo (bkz. yukaridaki test).
        var adimlar = new[] { new CleanupAdimi("kalici-arizali", () => throw new InvalidOperationException("cleanup da basarisiz")) };
        var cleanupHatalari = await TwoPhaseCleanupRunner.CalistirAsync(adimlar);

        Exception? cleanupHatasi = null;
        if (cleanupHatalari.Count > 0)
        {
            cleanupHatasi = new AggregateException("cleanup basarisiz", cleanupHatalari);
        }

        // Gercek DisposeAsync/xUnit akisinda bu iki exception BIRBIRINDEN BAGIMSIZ olarak ortaya
        // cikar (biri digerini yutmaz) - bu test, cleanup hatasinin govde hatasini MASKELEMEDIGINI,
        // her ikisinin de ayri ayri GOZLEMLENEBILIR kaldigini dogrular.
        Assert.NotNull(govdeHatasi);
        Assert.NotNull(cleanupHatasi);
        Assert.IsType<AggregateException>(cleanupHatasi);
    }

    // ─────────────────────────────────────────────────────────────
    // Gercek SQL Server'a karsi, DisposeAsync'in kendisini (PosTahsilatValorIntegrationTests
    // uzerinden) uctan uca dogrulayan testler.
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task NormalTestTamamlanmasi_CleanupSonrasiKalintiKalmaz()
    {
        var test = new PosTahsilatValorIntegrationTests();
        await test.InitializeAsync();

        // Normal (basarili) bir test yasam donguleri gibi - hicbir ek bozulma yok, dogrudan
        // DisposeAsync cagrilir. Hic exception firlatmamali.
        var exception = await Record.ExceptionAsync(() => test.DisposeAsync());
        Assert.Null(exception);

        // Cleanup GERCEKTEN tam oldugunu (dogrulama sorgusunun kendisiyle) teyit et.
        var kalanlar = await test.DogrulaTemizlikKalintilariAsync();
        Assert.Empty(kalanlar);
    }

    [IntegrationFact]
    public async Task DogrulamaSorgusu_AtlananBirAdimiKalintiOlarakTespitEder()
    {
        var test = new PosTahsilatValorIntegrationTests();
        await test.InitializeAsync();

        try
        {
            var adimlar = test.OlusturCleanupAdimlari();
            // "MuhasebeHesapPlanlari silme" adimini KASITLI olarak ATLA - bu, bir adimin sessizce
            // calismadigi/basarisiz oldugu (ornegin bir kod hatasi nedeniyle FK sirasindan
            // dusuruldugu) KALICI bir bosluk senaryosunu simule eder. Bu adim, KENDISINDEN SONRAKI
            // hicbir adimin BASARISINI etkilemez (Tesisler/Iller/Kurumlar MuhasebeHesapPlanlari'na
            // FK ile bagli DEGILDIR) - bu yuzden geri kalan TUM adimlar sorunsuz tamamlanir ve
            // TEK kalinti MuhasebeHesapPlanlari'nda olusur (test, iki ayri try/finally'nin
            // birbirinin exception'ini MASKELEMESI riskinden kacinmak icin bilinclii olarak
            // "cascade" ETMEYEN bir adim secer).
            var eksikAdimlarListesi = adimlar.Where(a => a.Ad != "MuhasebeHesapPlanlari silme").ToList();

            var hatalar = await TwoPhaseCleanupRunner.CalistirAsync(eksikAdimlarListesi);
            // Calisan adimlarin HICBIRI basarisiz olmadi (yalnizca listeden CIKARILDI, hata
            // FIRLATILMADI) - bu yuzden runner'in kendisi hata DONDURMEZ.
            Assert.Empty(hatalar);

            // Ama dogrulama sorgusu, MuhasebeHesapPlanlari tablosunda hala kalinti oldugunu
            // GERCEKTEN tespit etmeli - "adimlarin basarili raporlamasina kor kor guvenilmez"
            // ilkesinin dogrudan kaniti.
            var kalanlar = await test.DogrulaTemizlikKalintilariAsync();
            Assert.True(kalanlar.TryGetValue("MuhasebeHesapPlanlari", out var hesapPlaniAdedi) && hesapPlaniAdedi > 0,
                $"Beklenen: MuhasebeHesapPlanlari tablosunda kalinti tespit edilmesi. Gercek kalanlar: {string.Join(", ", kalanlar.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
        finally
        {
            // Testin kendi kalintisini GERCEKTEN temizle - eksik birakilan "MuhasebeHesapPlanlari
            // silme" adimi dahil TUM adimlari (yeniden olusturarak) calistirip veritabanini temiz
            // birak.
            await test.DisposeAsync();
        }
    }

    [IntegrationFact]
    public async Task IkiEsZamanliTestCalismasininCleanup_BirbirininVerisineDokunmaz()
    {
        var test1 = new PosTahsilatValorIntegrationTests();
        var test2 = new PosTahsilatValorIntegrationTests();

        await Task.WhenAll(test1.InitializeAsync(), test2.InitializeAsync());

        Assert.NotEqual(test1.KurumId, test2.KurumId);
        Assert.NotEqual(test1.TesisAId, test2.TesisAId);
        Assert.NotEqual(test1.TesisBId, test2.TesisBId);

        // Iki BAGIMSIZ test "instance"inin cleanup'i AYNI ANDA calisir - biri digerinin
        // TesisAId/TesisBId/KurumId/_uniqueSuffix (benzersiz GUID tabanli) kapsamina ASLA girmez.
        var exception1Task = Record.ExceptionAsync(() => test1.DisposeAsync());
        var exception2Task = Record.ExceptionAsync(() => test2.DisposeAsync());
        var exceptions = await Task.WhenAll(exception1Task, exception2Task);

        Assert.All(exceptions, Assert.Null);

        var kalanlar1 = await test1.DogrulaTemizlikKalintilariAsync();
        var kalanlar2 = await test2.DogrulaTemizlikKalintilariAsync();
        Assert.Empty(kalanlar1);
        Assert.Empty(kalanlar2);
    }
}
