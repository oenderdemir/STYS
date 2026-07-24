using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Infrastructure.EntityFramework.Migrations;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// 20260723205909_FixKrediKartlariAnaHesapHiyerarsisi migrationinin (bkz. FixKrediKartlariAnaHesapHiyerarsisi.FixSql)
/// dogrulugunu GERCEK SQL Server'a karsi test eder.
///
/// ONEMLI - IZOLASYON: "1.10"/"1.10.109" GERCEK, PAYLASILAN (TesisId IS NULL) global sablon
/// hesaplaridir - baska hicbir test/gelistirici bunlari BOZAMAZ. Bu yuzden HER test, ayni
/// StysAppDbContext uzerinde ACIK bir transaction ACAR, gercek "1.10.109" satirini o transaction
/// icinde GECICI olarak istenen "on-kosul" durumuna getirir, FixSql'i AYNI transaction icinde
/// calistirir, sonuclari dogrular, ve en SONUNDA (basarili da olsa) transaction'i HER ZAMAN
/// ROLLBACK eder (finally). Boylece migration'in GERCEK SQL'i gercek bir SQL Server'a karsi
/// calistirilmis olur, ama testin yaptigi GECICI bozulma hicbir zaman KALICI hale gelmez ve
/// paylasilan dev veritabaninda IZ BIRAKMAZ.
/// </summary>
[Trait("Category", "Integration")]
public class FixKrediKartlariAnaHesapHiyerarsisiMigrationTests
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);

    private const string TestMarker = "FKKH-970";

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>().UseSqlServer(ConnectionString).Options;
        return new StysAppDbContext(options, currentUserAccessor: null, currentTenantAccessor: null);
    }

    private sealed record HazirHesaplar(int HazirDegerlerId, int HazirDegerlerSeviye, int KrediKartlariId);

    /// <summary>Gercek global "1.10" ve "1.10.109" hesaplarinin Id/SeviyeNo bilgisini okur -
    /// hicbir sey degistirmez.</summary>
    private static async Task<HazirHesaplar> OkuGercekHesaplariAsync(StysAppDbContext dbContext)
    {
        var hazirDegerler = await dbContext.MuhasebeHesapPlanlari.AsNoTracking()
            .Where(x => !x.IsDeleted && x.TesisId == null && x.TamKod == "1.10")
            .Select(x => new { x.Id, x.SeviyeNo })
            .SingleAsync();
        var krediKartlari = await dbContext.MuhasebeHesapPlanlari.AsNoTracking()
            .Where(x => !x.IsDeleted && x.TesisId == null && x.TamKod == "1.10.109")
            .Select(x => x.Id)
            .SingleAsync();

        return new HazirHesaplar(hazirDegerler.Id, hazirDegerler.SeviyeNo, krediKartlari);
    }

    /// <summary>Gercek "1.10.109" satirini (transaction icinde GECICI olarak) verilen SeviyeNo/
    /// UstHesapId degerlerine getirir.</summary>
    private static async Task BozAnaHesabiAsync(StysAppDbContext dbContext, int krediKartlariId, int seviyeNo, int? ustHesapId)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [muhasebe].[MuhasebeHesapPlanlari]
SET [SeviyeNo] = {seviyeNo}, [UstHesapId] = {ustHesapId}
WHERE [Id] = {krediKartlariId}");
    }

    private static async Task<(int SeviyeNo, int? UstHesapId)> OkuAnaHesapDurumuAsync(StysAppDbContext dbContext, int krediKartlariId)
    {
        return await dbContext.MuhasebeHesapPlanlari.AsNoTracking()
            .Where(x => x.Id == krediKartlariId)
            .Select(x => new ValueTuple<int, int?>(x.SeviyeNo, x.UstHesapId))
            .SingleAsync();
    }

    private static async Task<int> EkleDetayHesapAsync(
        StysAppDbContext dbContext, int krediKartlariId, int seviyeNo, string adSuffix, bool isDeleted = false)
    {
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        var detay = new MuhasebeHesapPlani
        {
            Kod = uniqueSuffix,
            TamKod = "1.10.109." + uniqueSuffix,
            Ad = "Test Detay " + adSuffix + " " + uniqueSuffix,
            SeviyeNo = seviyeNo,
            HesapTipi = HesapTipi.DetayHesap,
            UstHesapId = krediKartlariId,
            AktifMi = true,
            DetayHesapMi = true,
            HareketGorebilirMi = true,
            IsDeleted = isDeleted
        };
        dbContext.MuhasebeHesapPlanlari.Add(detay);
        await dbContext.SaveChangesAsync();

        if (isDeleted)
        {
            // StysAppDbContext.ApplyAuditInfo, EKLENEN (Added) her satirin IsDeleted'ini
            // SaveChangesAsync sirasinda KOSULSUZ false'a zorlar (normal insert akisinin dogal
            // davranisi) - bu yuzden "zaten soft-delete edilmis" bir kaydi simule etmek icin,
            // eklendikten SONRA ayri bir raw SQL UPDATE ile IsDeleted=1 yapilir (ApplyAuditInfo'yu
            // atlar).
            await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [muhasebe].[MuhasebeHesapPlanlari] SET [IsDeleted] = 1 WHERE [Id] = {detay.Id}");
        }

        return detay.Id;
    }

    /// <summary>Testte "yanlis ust hesap" olarak kullanilacak, GERCEKTEN veritabaninda var olan,
    /// aktif ve Hazir Degerler/Kredi Kartlari hesaplarindan FARKLI bir MuhasebeHesapPlani Id'si
    /// bulur (Identity degerlerinin ARDISIK oldugu VARSAYILMAZ - "HazirDegerlerId + 1" gibi bir Id
    /// gercekte var olmayabilir, bu da testin "yanlis ama var olan bir ust hesap" senaryosunu
    /// yanlislikla "var OLMAYAN bir ust hesap" senaryosuna donusturebilirdi). Boyle bir hesap
    /// bulunamazsa (beklenmedik ama teorik olarak mumkun), transaction icinde GECICI bir template
    /// hesap olusturup onun Id'sini dondurur - transaction sonunda rollback ile iz birakmaz.</summary>
    private static async Task<int> BulYanlisUstHesapIdAsync(StysAppDbContext dbContext, HazirHesaplar gercek)
    {
        var mevcut = await dbContext.MuhasebeHesapPlanlari.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Id != gercek.HazirDegerlerId && x.Id != gercek.KrediKartlariId)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        if (mevcut.HasValue)
        {
            return mevcut.Value;
        }

        var uniqueSuffix = $"{TestMarker}-YANLISUST-{Guid.NewGuid():N}"[..28];
        var gecici = new MuhasebeHesapPlani
        {
            Kod = uniqueSuffix,
            TamKod = "9.99." + uniqueSuffix,
            Ad = "Test Gecici Yanlis Ust Hesap " + uniqueSuffix,
            SeviyeNo = 1,
            HesapTipi = HesapTipi.AltHesap,
            AktifMi = true,
            DetayHesapMi = false,
            HareketGorebilirMi = false
        };
        dbContext.MuhasebeHesapPlanlari.Add(gecici);
        await dbContext.SaveChangesAsync();
        return gecici.Id;
    }

    private static async Task<int> EkleKasaBankaHesabiAsync(StysAppDbContext dbContext, int hesapPlaniId, string tip, bool isDeleted = false)
    {
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..20];
        var kasaBanka = new KasaBankaHesap
        {
            TesisId = null,
            Tip = tip,
            Kod = uniqueSuffix,
            Ad = "Test KasaBanka " + uniqueSuffix,
            ParaBirimi = "TRY",
            AktifMi = true,
            MuhasebeHesapPlaniId = hesapPlaniId
        };
        dbContext.KasaBankaHesaplari.Add(kasaBanka);
        await dbContext.SaveChangesAsync();

        if (isDeleted)
        {
            // ApplyAuditInfo, EKLENEN (Added) her satirin IsDeleted'ini SaveChangesAsync sirasinda
            // KOSULSUZ false'a zorlar (bkz. EkleDetayHesapAsync'teki ayni notu) - "zaten soft-delete
            // edilmis bir KasaBankaHesap baglantisi" simule etmek icin ayri bir raw UPDATE gerekir.
            await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [muhasebe].[KasaBankaHesaplari] SET [IsDeleted] = 1 WHERE [Id] = {kasaBanka.Id}");
        }

        return kasaBanka.Id;
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 1 — Orijinal hatali durum: ana=4, detay=5, yanlis ust hesap.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task OrijinalHataliDurum_AnaVeDetayDuzeltilir()
    {
        await using var dbContext = CreateDbContext();
        var gercek = await OkuGercekHesaplariAsync(dbContext);
        var beklenenAnaSeviye = gercek.HazirDegerlerSeviye + 1;
        var beklenenDetaySeviye = beklenenAnaSeviye + 1;

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            // Yanlis ust hesap: Hazir Degerler DEGIL, GERCEKTEN var olan baska bir hesap (Identity
            // degerlerinin ardisik oldugu varsayilmaz, bkz. BulYanlisUstHesapIdAsync).
            var yanlisUstHesapId = await BulYanlisUstHesapIdAsync(dbContext, gercek);
            await BozAnaHesabiAsync(dbContext, gercek.KrediKartlariId, beklenenAnaSeviye + 1, yanlisUstHesapId);
            var detayId = await EkleDetayHesapAsync(dbContext, gercek.KrediKartlariId, beklenenDetaySeviye + 1, "orijinal-hatali");

            await dbContext.Database.ExecuteSqlRawAsync(FixKrediKartlariAnaHesapHiyerarsisi.FixSql);

            var anaDurum = await OkuAnaHesapDurumuAsync(dbContext, gercek.KrediKartlariId);
            Assert.Equal(beklenenAnaSeviye, anaDurum.SeviyeNo);
            Assert.Equal(gercek.HazirDegerlerId, anaDurum.UstHesapId);

            var detaySeviye = await dbContext.MuhasebeHesapPlanlari.AsNoTracking().Where(x => x.Id == detayId).Select(x => x.SeviyeNo).SingleAsync();
            Assert.Equal(beklenenDetaySeviye, detaySeviye);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 2 — Tamamen dogru durum: hicbir sey degismemeli (idempotent no-op).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task TamamenDogruDurum_HicbirSeyDegismez()
    {
        await using var dbContext = CreateDbContext();
        var gercek = await OkuGercekHesaplariAsync(dbContext);
        var dogruAnaSeviye = gercek.HazirDegerlerSeviye + 1;
        var dogruDetaySeviye = dogruAnaSeviye + 1;

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await BozAnaHesabiAsync(dbContext, gercek.KrediKartlariId, dogruAnaSeviye, gercek.HazirDegerlerId);
            var detayId = await EkleDetayHesapAsync(dbContext, gercek.KrediKartlariId, dogruDetaySeviye, "zaten-dogru");

            await dbContext.Database.ExecuteSqlRawAsync(FixKrediKartlariAnaHesapHiyerarsisi.FixSql);

            var anaDurum = await OkuAnaHesapDurumuAsync(dbContext, gercek.KrediKartlariId);
            Assert.Equal(dogruAnaSeviye, anaDurum.SeviyeNo);
            Assert.Equal(gercek.HazirDegerlerId, anaDurum.UstHesapId);

            var detaySeviye = await dbContext.MuhasebeHesapPlanlari.AsNoTracking().Where(x => x.Id == detayId).Select(x => x.SeviyeNo).SingleAsync();
            Assert.Equal(dogruDetaySeviye, detaySeviye);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 3 — Kismen duzeltilmis durum: ana=DOGRU, detay=ESKI (yanlis), ust hesap YANLIS.
    // Bulgu #1/#2'nin dogrudan hedefledigi senaryo: ana hesabin SeviyeNo'su daha once elle
    // duzeltilmis olabilir ama detay hesaplar hala eski seviyede kalmis olabilir - eski (goreli
    // kaydirma yapan, "ana eski<>yeni" kosuluyla korunan) tasarimda bu durumda detay hesaplar HIC
    // duzeltilmezdi. Yeni deterministik tasarimda detay hesaplar HER ZAMAN dogru degere sabitlenir.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task KismenDuzeltilmisDurum_AnaSeviyeDogruAmaDetayEskiKalmis_DetayYineDeDuzeltilir()
    {
        await using var dbContext = CreateDbContext();
        var gercek = await OkuGercekHesaplariAsync(dbContext);
        var dogruAnaSeviye = gercek.HazirDegerlerSeviye + 1;
        var dogruDetaySeviye = dogruAnaSeviye + 1;

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            // Ana hesabin SeviyeNo'su ZATEN dogru (dogruAnaSeviye) ama UstHesapId hala YANLIS -
            // "kismen duzeltilmis" durumu boyle simule edilir (yalnizca SeviyeNo elle duzeltilmis,
            // UstHesapId unutulmus).
            var yanlisUstHesapId = await BulYanlisUstHesapIdAsync(dbContext, gercek);
            await BozAnaHesabiAsync(dbContext, gercek.KrediKartlariId, dogruAnaSeviye, yanlisUstHesapId);
            // Detay hesap ESKI (yanlis) seviyede birakilmis.
            var detayId = await EkleDetayHesapAsync(dbContext, gercek.KrediKartlariId, dogruDetaySeviye + 1, "kismen-duzeltilmis");

            await dbContext.Database.ExecuteSqlRawAsync(FixKrediKartlariAnaHesapHiyerarsisi.FixSql);

            var anaDurum = await OkuAnaHesapDurumuAsync(dbContext, gercek.KrediKartlariId);
            Assert.Equal(dogruAnaSeviye, anaDurum.SeviyeNo);
            Assert.Equal(gercek.HazirDegerlerId, anaDurum.UstHesapId);

            var detaySeviye = await dbContext.MuhasebeHesapPlanlari.AsNoTracking().Where(x => x.Id == detayId).Select(x => x.SeviyeNo).SingleAsync();
            Assert.Equal(dogruDetaySeviye, detaySeviye);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 4 — Ana SeviyeNo dogru fakat UstHesapId NULL (bulgu #4'un NULL karsilastirma hatasi).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task AnaSeviyeDogruAmaUstHesapIdNull_DuzeltilirNull()
    {
        await using var dbContext = CreateDbContext();
        var gercek = await OkuGercekHesaplariAsync(dbContext);
        var dogruAnaSeviye = gercek.HazirDegerlerSeviye + 1;

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await BozAnaHesabiAsync(dbContext, gercek.KrediKartlariId, dogruAnaSeviye, ustHesapId: null);

            await dbContext.Database.ExecuteSqlRawAsync(FixKrediKartlariAnaHesapHiyerarsisi.FixSql);

            var anaDurum = await OkuAnaHesapDurumuAsync(dbContext, gercek.KrediKartlariId);
            Assert.Equal(dogruAnaSeviye, anaDurum.SeviyeNo);
            Assert.Equal(gercek.HazirDegerlerId, anaDurum.UstHesapId);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 5 — Soft-delete edilmis bir detay hesap: yanlis seviyede olsa bile DOKUNULMAZ.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task SoftDeleteEdilmisDetayHesap_Dokunulmaz()
    {
        await using var dbContext = CreateDbContext();
        var gercek = await OkuGercekHesaplariAsync(dbContext);
        var dogruAnaSeviye = gercek.HazirDegerlerSeviye + 1;
        var dogruDetaySeviye = dogruAnaSeviye + 1;

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await BozAnaHesabiAsync(dbContext, gercek.KrediKartlariId, dogruAnaSeviye + 1, await BulYanlisUstHesapIdAsync(dbContext, gercek));
            var yanlisSeviye = dogruDetaySeviye + 5;
            var silinmisDetayId = await EkleDetayHesapAsync(dbContext, gercek.KrediKartlariId, yanlisSeviye, "soft-deleted", isDeleted: true);

            await dbContext.Database.ExecuteSqlRawAsync(FixKrediKartlariAnaHesapHiyerarsisi.FixSql);

            // Soft-delete edilmis satir HALA yanlis seviyede kalmali - fix bu satiri gormezden gelmeli.
            var silinmisSeviye = await dbContext.MuhasebeHesapPlanlari.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.Id == silinmisDetayId).Select(x => x.SeviyeNo).SingleAsync();
            Assert.Equal(yanlisSeviye, silinmisSeviye);

            // Ama ana hesap yine de duzeltilmis olmali.
            var anaDurum = await OkuAnaHesapDurumuAsync(dbContext, gercek.KrediKartlariId);
            Assert.Equal(dogruAnaSeviye, anaDurum.SeviyeNo);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 6 — Ilgisiz baska bir dogrudan alt hesap: KasaBankaHesaplari'nda BASKA bir Tip'e
    // (Banka) bagli oldugu icin fix tarafindan KASITLI olarak korunmali/atlanmali.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task BaskaTipeBagliAlrHesap_Korunur()
    {
        await using var dbContext = CreateDbContext();
        var gercek = await OkuGercekHesaplariAsync(dbContext);
        var dogruAnaSeviye = gercek.HazirDegerlerSeviye + 1;
        var dogruDetaySeviye = dogruAnaSeviye + 1;

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await BozAnaHesabiAsync(dbContext, gercek.KrediKartlariId, dogruAnaSeviye + 1, await BulYanlisUstHesapIdAsync(dbContext, gercek));
            var yanlisSeviye = dogruDetaySeviye + 5;
            var ilgisizDetayId = await EkleDetayHesapAsync(dbContext, gercek.KrediKartlariId, yanlisSeviye, "ilgisiz-banka");
            // Bu detay hesap, KrediKarti DEGIL, Banka tipi bir KasaBankaHesap'a bagli - "gercekten
            // kredi karti detay hesabi" dogrulamasini GECEMEZ, bu yuzden fix tarafindan atlanmali.
            await EkleKasaBankaHesabiAsync(dbContext, ilgisizDetayId, KasaBankaHesapTipleri.Banka);

            await dbContext.Database.ExecuteSqlRawAsync(FixKrediKartlariAnaHesapHiyerarsisi.FixSql);

            var ilgisizSeviyeSonra = await dbContext.MuhasebeHesapPlanlari.AsNoTracking()
                .Where(x => x.Id == ilgisizDetayId).Select(x => x.SeviyeNo).SingleAsync();
            Assert.Equal(yanlisSeviye, ilgisizSeviyeSonra);

            // Ana hesap yine de duzeltilmis olmali.
            var anaDurum = await OkuAnaHesapDurumuAsync(dbContext, gercek.KrediKartlariId);
            Assert.Equal(dogruAnaSeviye, anaDurum.SeviyeNo);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 6b (bulgu #7/#8) — Detay hesap yanlis seviyede, ama TEK bagli KasaBankaHesap kaydi
    // SOFT-DELETE edilmis bir Banka tipi hesap. Migration'in EXISTS/NOT EXISTS sorgulari
    // k.IsDeleted=0 filtresi TASIMALIDIR - aksi halde soft-delete edilmis bu Banka baglantisi
    // "aktif bir baska-tip baglanti" sanilip detay hesap YANLISLIKLA atlanirdi. Soft-delete edilmis
    // baglanti YOK SAYILMALI (aktif hicbir KasaBankaHesap yokmus GIBI degerlendirilmeli), bu da
    // "hicbir KasaBankaHesap'a bagli olmayan detay hesap" dalindan (NOT EXISTS) gecmesini ve
    // DUZELTILMESINI saglar.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task SadeceSoftDeleteEdilmisBankaBaglantisiOlanDetayHesap_AktifBaglantiSayilmazVeDuzeltilir()
    {
        await using var dbContext = CreateDbContext();
        var gercek = await OkuGercekHesaplariAsync(dbContext);
        var dogruAnaSeviye = gercek.HazirDegerlerSeviye + 1;
        var dogruDetaySeviye = dogruAnaSeviye + 1;

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await BozAnaHesabiAsync(dbContext, gercek.KrediKartlariId, dogruAnaSeviye + 1, await BulYanlisUstHesapIdAsync(dbContext, gercek));
            var yanlisSeviye = dogruDetaySeviye + 5;
            var detayId = await EkleDetayHesapAsync(dbContext, gercek.KrediKartlariId, yanlisSeviye, "soft-deleted-banka-baglantisi");
            // Bu KasaBankaHesap kaydi Banka tipinde VE soft-delete edilmis - fix'in sorgusu bunu
            // "aktif bir baska-tip baglanti" olarak GORMEMELI (k.IsDeleted=0 filtresi).
            await EkleKasaBankaHesabiAsync(dbContext, detayId, KasaBankaHesapTipleri.Banka, isDeleted: true);

            await dbContext.Database.ExecuteSqlRawAsync(FixKrediKartlariAnaHesapHiyerarsisi.FixSql);

            var detaySeviyeSonra = await dbContext.MuhasebeHesapPlanlari.AsNoTracking()
                .Where(x => x.Id == detayId).Select(x => x.SeviyeNo).SingleAsync();
            Assert.Equal(dogruDetaySeviye, detaySeviyeSonra);

            var anaDurum = await OkuAnaHesapDurumuAsync(dbContext, gercek.KrediKartlariId);
            Assert.Equal(dogruAnaSeviye, anaDurum.SeviyeNo);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 7 — Gercekten KrediKarti tipine bagli bir detay hesap: duzeltilmeli, VE
    // KasaBankaHesaplari.MuhasebeHesapPlaniId baglantisi (Id degismedigi icin) korunmali.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task KrediKartiTipineBagliDetayHesap_DuzeltilirVeKasaBankaBaglantisiKorunur()
    {
        await using var dbContext = CreateDbContext();
        var gercek = await OkuGercekHesaplariAsync(dbContext);
        var dogruAnaSeviye = gercek.HazirDegerlerSeviye + 1;
        var dogruDetaySeviye = dogruAnaSeviye + 1;

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await BozAnaHesabiAsync(dbContext, gercek.KrediKartlariId, dogruAnaSeviye + 1, await BulYanlisUstHesapIdAsync(dbContext, gercek));
            var yanlisSeviye = dogruDetaySeviye + 1;
            var detayId = await EkleDetayHesapAsync(dbContext, gercek.KrediKartlariId, yanlisSeviye, "kredi-karti-hesap");
            var kasaBankaId = await EkleKasaBankaHesabiAsync(dbContext, detayId, KasaBankaHesapTipleri.KrediKarti);

            await dbContext.Database.ExecuteSqlRawAsync(FixKrediKartlariAnaHesapHiyerarsisi.FixSql);

            var detaySeviyeSonra = await dbContext.MuhasebeHesapPlanlari.AsNoTracking()
                .Where(x => x.Id == detayId).Select(x => x.SeviyeNo).SingleAsync();
            Assert.Equal(dogruDetaySeviye, detaySeviyeSonra);

            // KasaBankaHesaplari -> MuhasebeHesapPlaniId baglantisi degismemis olmali (fix yalnizca
            // SeviyeNo/UstHesapId'yi degistirir, Id'lere dokunmaz).
            var kasaBankaMuhasebeHesapPlaniId = await dbContext.KasaBankaHesaplari.AsNoTracking()
                .Where(x => x.Id == kasaBankaId).Select(x => x.MuhasebeHesapPlaniId).SingleAsync();
            Assert.Equal(detayId, kasaBankaMuhasebeHesapPlaniId);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 8 — Migration ikinci kez calistirildiginda sonuc DEGISMEMELI (idempotency).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task IkinciKezCalistirma_SonucDegismez()
    {
        await using var dbContext = CreateDbContext();
        var gercek = await OkuGercekHesaplariAsync(dbContext);
        var dogruAnaSeviye = gercek.HazirDegerlerSeviye + 1;
        var dogruDetaySeviye = dogruAnaSeviye + 1;

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await BozAnaHesabiAsync(dbContext, gercek.KrediKartlariId, dogruAnaSeviye + 1, await BulYanlisUstHesapIdAsync(dbContext, gercek));
            var detayId = await EkleDetayHesapAsync(dbContext, gercek.KrediKartlariId, dogruDetaySeviye + 1, "idempotency");

            await dbContext.Database.ExecuteSqlRawAsync(FixKrediKartlariAnaHesapHiyerarsisi.FixSql);
            var anaDurum1 = await OkuAnaHesapDurumuAsync(dbContext, gercek.KrediKartlariId);
            var detaySeviye1 = await dbContext.MuhasebeHesapPlanlari.AsNoTracking().Where(x => x.Id == detayId).Select(x => x.SeviyeNo).SingleAsync();

            // Ikinci kez calistir.
            await dbContext.Database.ExecuteSqlRawAsync(FixKrediKartlariAnaHesapHiyerarsisi.FixSql);
            var anaDurum2 = await OkuAnaHesapDurumuAsync(dbContext, gercek.KrediKartlariId);
            var detaySeviye2 = await dbContext.MuhasebeHesapPlanlari.AsNoTracking().Where(x => x.Id == detayId).Select(x => x.SeviyeNo).SingleAsync();

            Assert.Equal(anaDurum1, anaDurum2);
            Assert.Equal(detaySeviye1, detaySeviye2);
            Assert.Equal(dogruAnaSeviye, anaDurum2.SeviyeNo);
            Assert.Equal(dogruDetaySeviye, detaySeviye2);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 9 — Migration oncesi/sonrasi iliski dogrulamalari (bulgu #7): 1.10.109'un ustu
    // 1.10 olmali, ana seviyesi = 1.10 seviyesi + 1, detay seviyesi = ana seviyesi + 1.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task MigrationSonrasiIliskiler_DogruSaglanir()
    {
        await using var dbContext = CreateDbContext();
        var gercek = await OkuGercekHesaplariAsync(dbContext);

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            await BozAnaHesabiAsync(dbContext, gercek.KrediKartlariId, gercek.HazirDegerlerSeviye + 5, await BulYanlisUstHesapIdAsync(dbContext, gercek));
            var detayId = await EkleDetayHesapAsync(dbContext, gercek.KrediKartlariId, gercek.HazirDegerlerSeviye + 8, "iliski-dogrulama");

            await dbContext.Database.ExecuteSqlRawAsync(FixKrediKartlariAnaHesapHiyerarsisi.FixSql);

            var anaDurum = await OkuAnaHesapDurumuAsync(dbContext, gercek.KrediKartlariId);
            var detaySeviye = await dbContext.MuhasebeHesapPlanlari.AsNoTracking().Where(x => x.Id == detayId).Select(x => x.SeviyeNo).SingleAsync();

            Assert.Equal(gercek.HazirDegerlerId, anaDurum.UstHesapId); // 1.10.109'un ustu 1.10 olmali
            Assert.Equal(gercek.HazirDegerlerSeviye + 1, anaDurum.SeviyeNo); // ana seviyesi = 1.10 seviyesi + 1
            Assert.Equal(anaDurum.SeviyeNo + 1, detaySeviye); // detay seviyesi = ana seviyesi + 1
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }
}
