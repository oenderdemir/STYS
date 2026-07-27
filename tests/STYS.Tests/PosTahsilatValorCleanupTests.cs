using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Tests.TestSupport;

namespace STYS.Tests;

/// <summary>
/// PosTahsilatValorIntegrationTests.DisposeAsync'in (bkz. TwoPhaseCleanupRunner) iki gecisli
/// temizlik + AggregateException raporlama davranisini dogrular. Ilk grup [Fact] testler DB
/// GEREKTIRMEZ (saf mantik, sahte adimlarla) - CI'da her zaman calisir. Ikinci grup [IntegrationFact]
/// testler GERCEK SQL Server'a karsi, DisposeAsync'in kendisini (OlusturCleanupAdimlari/
/// DogrulaTemizlikKalintilariAsync araciligiyla) uctan uca dogrular.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
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
    public async Task TestGovdesiVeCleanupBirlikteBasarisizsa_AggregateExceptionIkisiniDeTasir()
    {
        // TestLifecycleHarness, xUnit'in GERCEK calisma sirasini (test govdesi -> HER ZAMAN
        // cagrilan DisposeAsync) ve hatalarin nasil BIRLESTIRILDIGINI (tek hata -> oldugu gibi,
        // birden fazla -> AggregateException) modeller - bu test iki AYRI degiskende hata TUTMAZ,
        // GERCEK orkestrasyon davranisini (govde + cleanup ayni yasam donguSunde calistirilir)
        // dogrudan calistirir.
        var govdeIstisnasi = new InvalidOperationException("test govdesi basarisiz (ornegin bir assertion hatasi)");

        Exception? sonuc = await TestLifecycleHarness.CalistirAsync(
            testGovdesi: () => throw govdeIstisnasi,
            cleanup: async () =>
            {
                // Gercek DisposeAsync'teki gibi: cleanup adimlari calisir, KALICI olarak basarisiz
                // olan bir adim AggregateException olarak firlatilir (bkz.
                // PosTahsilatValorIntegrationTests.DisposeAsync).
                var adimlar = new[] { new CleanupAdimi("kalici-arizali-adim", () => throw new InvalidOperationException("cleanup adimi basarisiz")) };
                var cleanupHatalari = await TwoPhaseCleanupRunner.CalistirAsync(adimlar);
                if (cleanupHatalari.Count > 0)
                {
                    throw new AggregateException("cleanup basarisiz", cleanupHatalari);
                }
            });

        // Hem test govdesi HEM cleanup basarisiz oldugu icin NIHAI sonuc bir AggregateException
        // olmali - biri digerini MASKELEMEMIS, ikisi de InnerExceptions icinde ayri ayri
        // GOZLEMLENEBILIR kalmali.
        var aggregate = Assert.IsType<AggregateException>(sonuc);
        Assert.Equal(2, aggregate.InnerExceptions.Count);

        // 1) Asil test govdesi exception'i KAYBOLMAMIS, TAM olarak (referans esitligiyle) mevcut.
        Assert.Contains(govdeIstisnasi, aggregate.InnerExceptions);

        // 2) Cleanup exception'i da (kendi ic yapisiyla - hangi ADIMIN basarisiz oldugu bilgisi
        // DAHIL) ayrica gorulebilir olmali.
        var cleanupExceptionGorulduMu = aggregate.InnerExceptions.Any(ex =>
            ex is AggregateException cleanupAggregate
            && cleanupAggregate.InnerExceptions.Any(inner => inner.Message.Contains("kalici-arizali-adim")));
        Assert.True(cleanupExceptionGorulduMu,
            $"Beklenen: cleanup exception'i adim adiyla birlikte gorulebilir olmali. Gercek InnerExceptions: {string.Join(" | ", aggregate.InnerExceptions.Select(e => e.Message))}");
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

    // ─────────────────────────────────────────────────────────────
    // Soft-delete senaryolari: StysAppDbContext TUM BaseEntity turlerine global IsDeleted=false
    // query filter'i uyguladigi icin, cleanup/dogrulama sorgulari IgnoreQueryFilters + ID-tabanli
    // alt sorgular KULLANMAZSA soft-delete edilmis test kayitlari GORULMEZ ve kalinti kalir. Bu
    // testler, OlusturCleanupAdimlari/DogrulaTemizlikKalintilariAsync'in bu kayitlari da
    // GORDUGUNU ve fiziksel olarak TEMIZLEDIGINI raw SQL/IgnoreQueryFilters ile dogrudan dogrular.
    // ─────────────────────────────────────────────────────────────

    private static Task SoftDeleteAsync(StysAppDbContext dbContext, string tabloAdi, int id)
    {
        // tabloAdi bu dosyanin kendi sabit picklist'inden gelir (kullanici girdisi DEGIL, string
        // interpolation ILE degil, concatenation ile birlestirilir ki EF'in FormattableString
        // parametrelemesi tabloAdi'ni YANLISLIKLA bir SQL parametresi/tanimlayicisi olarak
        // yorumlamasin); yalnizca Id gercek bir SQL parametresi olarak gecirilir.
        var sql = "UPDATE [muhasebe].[" + tabloAdi + "] SET [IsDeleted] = 1 WHERE [Id] = {0}";
        return dbContext.Database.ExecuteSqlRawAsync(sql, id);
    }

    [IntegrationFact]
    public async Task PosTahsilatValorSoftDeleteEdilmiskenCleanup_FizikselKayitKalmaz()
    {
        var test = new PosTahsilatValorIntegrationTests();
        await test.InitializeAsync();

        int valorId;
        await using (var dbContext = PosTahsilatValorIntegrationTests.CreateDbContext())
        {
            valorId = await test.SeedValorKaydiAsync(
                dbContext, test.TesisAId, test.CariKartAId, test.KasaBankaPosAId, test.KasaBankaBankaAId,
                100m, 0m, test.HesapPlaniKomisyonId, "SDEL-POSVALOR");
            await SoftDeleteAsync(dbContext, "PosTahsilatValorleri", valorId);
        }

        var disposeHatasi = await Record.ExceptionAsync(() => test.DisposeAsync());
        Assert.Null(disposeHatasi);

        await using var verifyContext = PosTahsilatValorIntegrationTests.CreateDbContext();
        var fizikselVarMi = await verifyContext.PosTahsilatValorleri.IgnoreQueryFilters().AnyAsync(x => x.Id == valorId);
        Assert.False(fizikselVarMi, "Soft-delete edilmis PosTahsilatValor cleanup sonrasi HALA fiziksel olarak var.");

        var kalanlar = await test.DogrulaTemizlikKalintilariAsync();
        Assert.Empty(kalanlar);
    }

    [IntegrationFact]
    public async Task TahsilatOdemeBelgesiSoftDeleteEdilmiskenCleanup_FizikselKayitKalmaz()
    {
        var test = new PosTahsilatValorIntegrationTests();
        await test.InitializeAsync();

        int belgeId;
        await using (var dbContext = PosTahsilatValorIntegrationTests.CreateDbContext())
        {
            var valorId = await test.SeedValorKaydiAsync(
                dbContext, test.TesisAId, test.CariKartAId, test.KasaBankaPosAId, test.KasaBankaBankaAId,
                100m, 0m, test.HesapPlaniKomisyonId, "SDEL-BELGE");
            belgeId = await dbContext.PosTahsilatValorleri.Where(x => x.Id == valorId).Select(x => x.TahsilatOdemeBelgesiId).SingleAsync();
            await SoftDeleteAsync(dbContext, "TahsilatOdemeBelgeleri", belgeId);
        }

        var disposeHatasi = await Record.ExceptionAsync(() => test.DisposeAsync());
        Assert.Null(disposeHatasi);

        await using var verifyContext = PosTahsilatValorIntegrationTests.CreateDbContext();
        var fizikselVarMi = await verifyContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().AnyAsync(x => x.Id == belgeId);
        Assert.False(fizikselVarMi, "Soft-delete edilmis TahsilatOdemeBelgesi cleanup sonrasi HALA fiziksel olarak var.");

        var kalanlar = await test.DogrulaTemizlikKalintilariAsync();
        Assert.Empty(kalanlar);
    }

    [IntegrationFact]
    public async Task CariKartSoftDeleteEdilmisFakatBaglBocukKayitVarkenCleanup_HepsiFizikselTemizlenir()
    {
        var test = new PosTahsilatValorIntegrationTests();
        await test.InitializeAsync();

        int belgeId;
        await using (var dbContext = PosTahsilatValorIntegrationTests.CreateDbContext())
        {
            // CariKartAId (test.CariKartAId) HALA aktif detay/hareket kayitlarina sahipken
            // (ornegin bu valor kaydinin bagli oldugu TahsilatOdemeBelgesi) KENDISI soft-delete
            // edilir - gercek dunyada "bir cari kart yanlislikla soft-delete edildi ama ona bagli
            // is verisi hala orada" senaryosunu modeller.
            var valorId = await test.SeedValorKaydiAsync(
                dbContext, test.TesisAId, test.CariKartAId, test.KasaBankaPosAId, test.KasaBankaBankaAId,
                100m, 0m, test.HesapPlaniKomisyonId, "SDEL-CARIKART");
            belgeId = await dbContext.PosTahsilatValorleri.Where(x => x.Id == valorId).Select(x => x.TahsilatOdemeBelgesiId).SingleAsync();
            await SoftDeleteAsync(dbContext, "CariKartlar", test.CariKartAId);
        }

        var disposeHatasi = await Record.ExceptionAsync(() => test.DisposeAsync());
        Assert.Null(disposeHatasi);

        await using var verifyContext = PosTahsilatValorIntegrationTests.CreateDbContext();
        Assert.False(await verifyContext.CariKartlar.IgnoreQueryFilters().AnyAsync(x => x.Id == test.CariKartAId),
            "Soft-delete edilmis CariKart cleanup sonrasi HALA fiziksel olarak var.");
        Assert.False(await verifyContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().AnyAsync(x => x.Id == belgeId),
            "Soft-delete edilmis CariKart'a bagli TahsilatOdemeBelgesi cleanup sonrasi HALA fiziksel olarak var (navigation tabanli filtrelemenin YETERSIZ kaldigi tam senaryo).");

        var kalanlar = await test.DogrulaTemizlikKalintilariAsync();
        Assert.Empty(kalanlar);
    }

    [IntegrationFact]
    public async Task MuhasebeFisSoftDeleteEdilmiskenCleanup_FizikselKayitKalmaz()
    {
        var test = new PosTahsilatValorIntegrationTests();
        await test.InitializeAsync();

        int fisId;
        await using (var dbContext = PosTahsilatValorIntegrationTests.CreateDbContext())
        {
            var fis = new STYS.Muhasebe.MuhasebeFisleri.Entities.MuhasebeFis
            {
                TesisId = test.TesisAId,
                MaliYil = 2026,
                Donem = 1,
                FisNo = $"{test._uniqueSuffix}-SDEL-FIS",
                FisTarihi = DateTime.UtcNow.Date,
                FisTipi = STYS.Muhasebe.Common.Constants.MuhasebeFisTipleri.Mahsup,
                Durum = STYS.Muhasebe.Common.Constants.MuhasebeFisDurumlari.Onayli,
                ToplamBorc = 10m,
                ToplamAlacak = 10m
            };
            dbContext.MuhasebeFisler.Add(fis);
            await dbContext.SaveChangesAsync();
            fisId = fis.Id;
            await SoftDeleteAsync(dbContext, "MuhasebeFisler", fisId);
        }

        var disposeHatasi = await Record.ExceptionAsync(() => test.DisposeAsync());
        Assert.Null(disposeHatasi);

        await using var verifyContext = PosTahsilatValorIntegrationTests.CreateDbContext();
        var fizikselVarMi = await verifyContext.MuhasebeFisler.IgnoreQueryFilters().AnyAsync(x => x.Id == fisId);
        Assert.False(fizikselVarMi, "Soft-delete edilmis MuhasebeFis cleanup sonrasi HALA fiziksel olarak var.");

        var kalanlar = await test.DogrulaTemizlikKalintilariAsync();
        Assert.Empty(kalanlar);
    }

    [IntegrationFact]
    public async Task HemParentHemChildSoftDeleteEdilmiskenCleanup_IkisiDeFizikselTemizlenir()
    {
        var test = new PosTahsilatValorIntegrationTests();
        await test.InitializeAsync();

        int valorId, degisiklikGecmisiId;
        await using (var dbContext = PosTahsilatValorIntegrationTests.CreateDbContext())
        {
            valorId = await test.SeedValorKaydiAsync(
                dbContext, test.TesisAId, test.CariKartAId, test.KasaBankaPosAId, test.KasaBankaBankaAId,
                100m, 0m, test.HesapPlaniKomisyonId, "SDEL-PARENTCHILD");

            var gecmis = new PosTahsilatValorDegisiklikGecmisi
            {
                PosTahsilatValorId = valorId,
                IslemTipi = "ManuelKomisyonDuzenleme",
                Aciklama = "test - parent+child soft-delete senaryosu",
                OncekiDegerJson = "{}",
                YeniDegerJson = "{}"
            };
            dbContext.PosTahsilatValorDegisiklikGecmisleri.Add(gecmis);
            await dbContext.SaveChangesAsync();
            degisiklikGecmisiId = gecmis.Id;

            // Hem PARENT (PosTahsilatValor) hem CHILD (PosTahsilatValorDegisiklikGecmisi) soft-delete
            // edilir - navigation tabanli bir cleanup sorgusu (`x.PosTahsilatValor.TesisId`) bu
            // durumda CHILD'i asla BULAMAZDI (parent'in KENDI query filter'i navigation'i da
            // etkiler).
            await SoftDeleteAsync(dbContext, "PosTahsilatValorleri", valorId);
            await SoftDeleteAsync(dbContext, "PosTahsilatValorDegisiklikGecmisleri", degisiklikGecmisiId);
        }

        var disposeHatasi = await Record.ExceptionAsync(() => test.DisposeAsync());
        Assert.Null(disposeHatasi);

        await using var verifyContext = PosTahsilatValorIntegrationTests.CreateDbContext();
        Assert.False(await verifyContext.PosTahsilatValorleri.IgnoreQueryFilters().AnyAsync(x => x.Id == valorId),
            "Soft-delete edilmis (parent) PosTahsilatValor cleanup sonrasi HALA fiziksel olarak var.");
        Assert.False(await verifyContext.PosTahsilatValorDegisiklikGecmisleri.IgnoreQueryFilters().AnyAsync(x => x.Id == degisiklikGecmisiId),
            "Soft-delete edilmis (child) PosTahsilatValorDegisiklikGecmisi cleanup sonrasi HALA fiziksel olarak var.");

        var kalanlar = await test.DogrulaTemizlikKalintilariAsync();
        Assert.Empty(kalanlar);
    }

    [IntegrationFact]
    public async Task CleanupAdimiAtlandiginda_DogrulamaSoftDeleteKalintisiniDaYakalar()
    {
        var test = new PosTahsilatValorIntegrationTests();
        await test.InitializeAsync();

        await using (var dbContext = PosTahsilatValorIntegrationTests.CreateDbContext())
        {
            // Global sablon hesaplarindan biri (HesapPlaniKomisyonId) soft-delete edilir - normal
            // (query-filter'li) bir sorgu bu satiri hic GOREMEZ; yalnizca IgnoreQueryFilters
            // kullanan dogrulama bunu kalinti olarak tespit edebilir.
            await SoftDeleteAsync(dbContext, "MuhasebeHesapPlanlari", test.HesapPlaniKomisyonId);
        }

        try
        {
            var adimlar = test.OlusturCleanupAdimlari();
            var eksikAdimlarListesi = adimlar.Where(a => a.Ad != "MuhasebeHesapPlanlari silme").ToList();

            var hatalar = await TwoPhaseCleanupRunner.CalistirAsync(eksikAdimlarListesi);
            Assert.Empty(hatalar);

            var kalanlar = await test.DogrulaTemizlikKalintilariAsync();
            Assert.True(kalanlar.TryGetValue("MuhasebeHesapPlanlari", out var adet) && adet > 0,
                $"Beklenen: soft-delete edilmis MuhasebeHesapPlani kaydinin da kalinti olarak tespit edilmesi. Gercek kalanlar: {string.Join(", ", kalanlar.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
        finally
        {
            await test.DisposeAsync();
        }
    }
}
