using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Tests.TestSupport;

namespace STYS.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class CariKartDuplicateMigrationIntegrationTests
{
    private static async Task CleanupCariDataAsync(StysAppDbContext db, int tesisId)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM muhasebe.CariHareketler WHERE CariKartId IN (SELECT Id FROM muhasebe.CariKartlar WHERE TesisId = {tesisId});
            DELETE FROM muhasebe.CariKartYetkiliKisileri WHERE CariKartId IN (SELECT Id FROM muhasebe.CariKartlar WHERE TesisId = {tesisId});
            DELETE FROM muhasebe.CariKartBankaHesaplari WHERE CariKartId IN (SELECT Id FROM muhasebe.CariKartlar WHERE TesisId = {tesisId});
            DELETE FROM muhasebe.CariKartlar WHERE TesisId = {tesisId};
            DELETE FROM muhasebe.MuhasebeHesapBakiyeleri WHERE MuhasebeHesapPlaniId IN (SELECT Id FROM muhasebe.MuhasebeHesapPlanlari WHERE TesisId = {tesisId});
            DELETE FROM muhasebe.MuhasebeHesapPlanlari WHERE TesisId = {tesisId};
            """);
    }

    private const string IndexName = "IX_CariKartlar_TesisId_VergiNoTcknNormalized_Musteri";

    private const string DropIndexSql =
        "DROP INDEX [IX_CariKartlar_TesisId_VergiNoTcknNormalized_Musteri] ON [muhasebe].[CariKartlar]";

    private const string RecreateIndexSql =
        "CREATE UNIQUE INDEX [IX_CariKartlar_TesisId_VergiNoTcknNormalized_Musteri] " +
        "ON [muhasebe].[CariKartlar] ([TesisId], [VergiNoTcknNormalized]) " +
        "WHERE [IsDeleted] = 0 AND [AktifMi] = 1 AND [TesisId] IS NOT NULL AND [VergiNoTcknNormalized] IS NOT NULL AND [CariTipi] IN ('Musteri', 'KurumsalMusteri')";

    private const string BackfillSql = """
        UPDATE c
        SET VergiNoTcknNormalized = NULLIF(
            LEFT(UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(c.VergiNoTckn)), '-', ''), '.', ''), ' ', ''), CHAR(9), ''), CHAR(13), ''), CHAR(10), '')), 32),
            '')
        FROM muhasebe.CariKartlar c
        WHERE c.IsDeleted = 0
          AND c.CariTipi IN ('Musteri','KurumsalMusteri')
          AND c.VergiNoTckn IS NOT NULL
          AND LTRIM(RTRIM(c.VergiNoTckn)) <> '';
        """;

    private const string RepairSql = """
        IF OBJECT_ID('tempdb..#DupGroups') IS NOT NULL DROP TABLE #DupGroups;
        SELECT TesisId, VergiNoTcknNormalized INTO #DupGroups
        FROM muhasebe.CariKartlar
        WHERE IsDeleted = 0 AND AktifMi = 1 AND TesisId IS NOT NULL AND VergiNoTcknNormalized IS NOT NULL
          AND CariTipi IN ('Musteri','KurumsalMusteri')
        GROUP BY TesisId, VergiNoTcknNormalized HAVING COUNT(*) > 1;

        IF OBJECT_ID('tempdb..#DupCards') IS NOT NULL DROP TABLE #DupCards;
        SELECT c.Id, c.TesisId, c.VergiNoTcknNormalized, c.MuhasebeHesapPlaniId,
            CASE WHEN EXISTS (SELECT 1 FROM muhasebe.CariHareketler x WHERE x.CariKartId = c.Id)
                  OR EXISTS (SELECT 1 FROM muhasebe.TahsilatOdemeBelgeleri x WHERE x.CariKartId = c.Id)
                  OR EXISTS (SELECT 1 FROM muhasebe.SatisBelgeleri x WHERE x.CariKartId = c.Id)
                  OR EXISTS (SELECT 1 FROM muhasebe.BankaHareketleri x WHERE x.CariKartId = c.Id)
                  OR EXISTS (SELECT 1 FROM muhasebe.KasaHareketleri x WHERE x.CariKartId = c.Id)
                  OR EXISTS (SELECT 1 FROM muhasebe.StokHareketleri x WHERE x.CariKartId = c.Id)
                  OR EXISTS (SELECT 1 FROM muhasebe.MuhasebeFisSatirlari x WHERE x.CariKartId = c.Id)
                  OR EXISTS (SELECT 1 FROM entegrasyon.PosOdemeIslemleri x WHERE x.CariKartId = c.Id)
                  OR (c.MuhasebeHesapPlaniId IS NOT NULL AND EXISTS (SELECT 1 FROM muhasebe.MuhasebeFisSatirlari x WHERE x.MuhasebeHesapPlaniId = c.MuhasebeHesapPlaniId))
                  OR (c.MuhasebeHesapPlaniId IS NOT NULL AND EXISTS (SELECT 1 FROM muhasebe.MuhasebeHesapBakiyeleri x WHERE x.MuhasebeHesapPlaniId = c.MuhasebeHesapPlaniId))
                THEN 1 ELSE 0 END AS FinansalKullanildi,
            CASE WHEN EXISTS (SELECT 1 FROM dbo.Rezervasyonlar x WHERE x.CariKartId = c.Id)
                  OR EXISTS (SELECT 1 FROM dbo.Tesisler x WHERE x.RezervasyonMisafirVarsayilanCariKartId = c.Id)
                THEN 1 ELSE 0 END AS IsKaydiVar
        INTO #DupCards
        FROM muhasebe.CariKartlar c
        INNER JOIN #DupGroups g ON g.TesisId = c.TesisId AND g.VergiNoTcknNormalized = c.VergiNoTcknNormalized
        WHERE c.IsDeleted = 0 AND c.AktifMi = 1 AND c.CariTipi IN ('Musteri','KurumsalMusteri');

        IF OBJECT_ID('tempdb..#Canonical') IS NOT NULL DROP TABLE #Canonical;
        SELECT g.TesisId, g.VergiNoTcknNormalized,
            COALESCE(
                (SELECT MIN(d2.Id) FROM #DupCards d2 WHERE d2.TesisId = g.TesisId AND d2.VergiNoTcknNormalized = g.VergiNoTcknNormalized AND d2.FinansalKullanildi = 1),
                (SELECT MIN(d2.Id) FROM #DupCards d2 WHERE d2.TesisId = g.TesisId AND d2.VergiNoTcknNormalized = g.VergiNoTcknNormalized AND d2.IsKaydiVar = 1),
                (SELECT MIN(d2.Id) FROM #DupCards d2 WHERE d2.TesisId = g.TesisId AND d2.VergiNoTcknNormalized = g.VergiNoTcknNormalized)
            ) AS CanonicalId
        INTO #Canonical FROM #DupGroups g;

        UPDATE c SET VergiNoTcknNormalized = NULL
        FROM muhasebe.CariKartlar c
        INNER JOIN #DupCards d ON d.Id = c.Id
        INNER JOIN #Canonical k ON k.TesisId = d.TesisId AND k.VergiNoTcknNormalized = d.VergiNoTcknNormalized
        WHERE d.Id <> k.CanonicalId AND d.FinansalKullanildi = 1;

        UPDATE y SET y.CariKartId = k.CanonicalId
        FROM muhasebe.CariKartYetkiliKisileri y
        INNER JOIN #DupCards d ON d.Id = y.CariKartId
        INNER JOIN #Canonical k ON k.TesisId = d.TesisId AND k.VergiNoTcknNormalized = d.VergiNoTcknNormalized
        WHERE d.Id <> k.CanonicalId AND d.FinansalKullanildi = 0;

        UPDATE b SET b.CariKartId = k.CanonicalId
        FROM muhasebe.CariKartBankaHesaplari b
        INNER JOIN #DupCards d ON d.Id = b.CariKartId
        INNER JOIN #Canonical k ON k.TesisId = d.TesisId AND k.VergiNoTcknNormalized = d.VergiNoTcknNormalized
        WHERE d.Id <> k.CanonicalId AND d.FinansalKullanildi = 0;

        UPDATE r SET r.CariKartId = k.CanonicalId
        FROM dbo.Rezervasyonlar r
        INNER JOIN #DupCards d ON d.Id = r.CariKartId
        INNER JOIN #Canonical k ON k.TesisId = d.TesisId AND k.VergiNoTcknNormalized = d.VergiNoTcknNormalized
        WHERE d.Id <> k.CanonicalId AND d.FinansalKullanildi = 0;

        UPDATE t SET t.RezervasyonMisafirVarsayilanCariKartId = k.CanonicalId
        FROM dbo.Tesisler t
        INNER JOIN #DupCards d ON d.Id = t.RezervasyonMisafirVarsayilanCariKartId
        INNER JOIN #Canonical k ON k.TesisId = d.TesisId AND k.VergiNoTcknNormalized = d.VergiNoTcknNormalized
        WHERE d.Id <> k.CanonicalId AND d.FinansalKullanildi = 0;

        UPDATE h SET h.AktifMi = 0
        FROM muhasebe.MuhasebeHesapPlanlari h
        INNER JOIN #DupCards d ON d.MuhasebeHesapPlaniId = h.Id
        INNER JOIN #Canonical k ON k.TesisId = d.TesisId AND k.VergiNoTcknNormalized = d.VergiNoTcknNormalized
        WHERE d.Id <> k.CanonicalId AND d.FinansalKullanildi = 0 AND d.MuhasebeHesapPlaniId IS NOT NULL;

        UPDATE c SET IsDeleted = 1, AktifMi = 0, DeletedAt = GETUTCDATE(), DeletedBy = 'migration:dedup'
        FROM muhasebe.CariKartlar c
        INNER JOIN #DupCards d ON d.Id = c.Id
        INNER JOIN #Canonical k ON k.TesisId = d.TesisId AND k.VergiNoTcknNormalized = d.VergiNoTcknNormalized
        WHERE d.Id <> k.CanonicalId AND d.FinansalKullanildi = 0;

        DROP TABLE #Canonical; DROP TABLE #DupCards; DROP TABLE #DupGroups;
        """;

    [IntegrationFact]
    public async Task Backfill_LegacyVergiNoTckn_NormalizeEdilir()
    {
        var cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(cs)) return;
        var suffix = Guid.NewGuid().ToString("N")[..10];

        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var (_, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, suffix);

        var card = new CariKart
        {
            TesisId = tesis.Id,
            CariTipi = CariKartTipleri.Musteri,
            CariKodu = $"KOD-{suffix}",
            UnvanAdSoyad = "Backfill Test",
            VergiNoTckn = "123-456-7890",
            VergiNoTcknNormalized = null,
            AktifMi = true
        };
        db.CariKartlar.Add(card);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(BackfillSql);

        var normalized = await db.CariKartlar.Where(x => x.Id == card.Id).Select(x => x.VergiNoTcknNormalized).SingleAsync();
        Assert.Equal("1234567890", normalized);
        await CleanupCariDataAsync(db, tesis.Id);
    }

    [IntegrationFact]
    public async Task Repair_KullanilmamisDuplicate_TekCanonicalKartKalir()
    {
        var cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(cs)) return;
        var suffix = Guid.NewGuid().ToString("N")[..10];

        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var (_, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, suffix);

        await db.Database.ExecuteSqlRawAsync(DropIndexSql);
        try
        {
            var cards = new List<CariKart>();
            for (var i = 0; i < 3; i++)
            {
                cards.Add(new CariKart
                {
                    TesisId = tesis.Id,
                    CariTipi = CariKartTipleri.Musteri,
                    CariKodu = $"DUP-{suffix}-{i}",
                    UnvanAdSoyad = "Dup Test",
                    VergiNoTckn = "11111111111",
                    VergiNoTcknNormalized = "11111111111",
                    AktifMi = true
                });
            }

            db.CariKartlar.AddRange(cards);
            await db.SaveChangesAsync();

            await db.Database.ExecuteSqlRawAsync(RepairSql);

            var active = await db.CariKartlar.IgnoreQueryFilters()
                .Where(x => cards.Select(c => c.Id).Contains(x.Id) && !x.IsDeleted)
                .ToListAsync();

            Assert.Single(active);
            Assert.Equal(cards.Min(c => c.Id), active[0].Id);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(RecreateIndexSql);
            await CleanupCariDataAsync(db, tesis.Id);
        }
    }

    [IntegrationFact]
    public async Task Repair_FinansalKullanilanKart_CanonicalOlur()
    {
        var cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(cs)) return;
        var suffix = Guid.NewGuid().ToString("N")[..10];

        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var (_, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, suffix);

        await db.Database.ExecuteSqlRawAsync(DropIndexSql);
        try
        {
            var unused = new CariKart
            {
                TesisId = tesis.Id,
                CariTipi = CariKartTipleri.Musteri,
                CariKodu = $"FIN-{suffix}-0",
                UnvanAdSoyad = "Fin Test",
                VergiNoTckn = "22222222222",
                VergiNoTcknNormalized = "22222222222",
                AktifMi = true
            };
            var used = new CariKart
            {
                TesisId = tesis.Id,
                CariTipi = CariKartTipleri.Musteri,
                CariKodu = $"FIN-{suffix}-1",
                UnvanAdSoyad = "Fin Test",
                VergiNoTckn = "22222222222",
                VergiNoTcknNormalized = "22222222222",
                AktifMi = true
            };
            db.CariKartlar.AddRange(unused, used);
            await db.SaveChangesAsync();

            db.CariHareketler.Add(new CariHareket
            {
                CariKartId = used.Id,
                HareketTarihi = DateTime.UtcNow.Date,
                BelgeTuru = "TEST",
                BelgeNo = $"TEST-{suffix}",
                Aciklama = "test",
                BorcTutari = 0m,
                AlacakTutari = 0m,
                KapananTutar = 0m,
                KalanTutar = 0m,
                ParaBirimi = "TRY",
                Durum = CariHareketDurumlari.Aktif,
                KaynakModul = "TEST",
                KaynakId = 0,
                KapandiMi = false
            });
            await db.SaveChangesAsync();

            await db.Database.ExecuteSqlRawAsync(RepairSql);

            var usedSurvives = await db.CariKartlar.IgnoreQueryFilters().AnyAsync(x => x.Id == used.Id && !x.IsDeleted);
            var unusedDeleted = await db.CariKartlar.IgnoreQueryFilters().AnyAsync(x => x.Id == unused.Id && x.IsDeleted);

            Assert.True(usedSurvives);
            Assert.True(unusedDeleted);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(RecreateIndexSql);
            await CleanupCariDataAsync(db, tesis.Id);
        }
    }

    [IntegrationFact]
    public async Task FarkliTesis_AyniTckn_IzinVerilir()
    {
        var cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(cs)) return;
        var suffix = Guid.NewGuid().ToString("N")[..10];

        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var (_, _, tesis1) = await AgentTestSupport.SeedKurumIlTesisAsync(db, suffix + "a");
        var (_, _, tesis2) = await AgentTestSupport.SeedKurumIlTesisAsync(db, suffix + "b");

        db.CariKartlar.AddRange(
            new CariKart
            {
                TesisId = tesis1.Id,
                CariTipi = CariKartTipleri.Musteri,
                CariKodu = $"TES-A-{suffix}",
                UnvanAdSoyad = "Tesis A",
                VergiNoTckn = "33333333333",
                VergiNoTcknNormalized = "33333333333",
                AktifMi = true
            },
            new CariKart
            {
                TesisId = tesis2.Id,
                CariTipi = CariKartTipleri.Musteri,
                CariKodu = $"TES-B-{suffix}",
                UnvanAdSoyad = "Tesis B",
                VergiNoTckn = "33333333333",
                VergiNoTcknNormalized = "33333333333",
                AktifMi = true
            });

        await db.SaveChangesAsync(); // should not throw

        var count = await db.CariKartlar.CountAsync(x => x.VergiNoTcknNormalized == "33333333333" && !x.IsDeleted);
        Assert.Equal(2, count);
        await CleanupCariDataAsync(db, tesis1.Id);
        await CleanupCariDataAsync(db, tesis2.Id);
    }

    [IntegrationFact]
    public async Task Repair_AccountLevelFinancialUsage_Silinmez()
    {
        var cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(cs)) return;
        var suffix = Guid.NewGuid().ToString("N")[..10];

        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var (_, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, suffix);

        var hesap = new MuhasebeHesapPlani
        {
            Kod = $"120.{suffix}",
            TamKod = $"120.{suffix}",
            Ad = "Detay Hesap",
            TesisId = tesis.Id,
            SeviyeNo = 3,
            HesapTipi = HesapTipi.DetayHesap
        };
        db.MuhasebeHesapPlanlari.Add(hesap);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(DropIndexSql);
        try
        {
            var unused = new CariKart
            {
                TesisId = tesis.Id,
                CariTipi = CariKartTipleri.Musteri,
                CariKodu = $"ACC-{suffix}-0",
                UnvanAdSoyad = "Acc Test",
                VergiNoTckn = "44444444444",
                VergiNoTcknNormalized = "44444444444",
                AktifMi = true
            };
            var accountUsed = new CariKart
            {
                TesisId = tesis.Id,
                CariTipi = CariKartTipleri.Musteri,
                CariKodu = $"ACC-{suffix}-1",
                UnvanAdSoyad = "Acc Test",
                VergiNoTckn = "44444444444",
                VergiNoTcknNormalized = "44444444444",
                MuhasebeHesapPlaniId = hesap.Id,
                AktifMi = true
            };
            db.CariKartlar.AddRange(unused, accountUsed);
            await db.SaveChangesAsync();

            db.MuhasebeHesapBakiyeleri.Add(new MuhasebeHesapBakiye
            {
                TesisId = tesis.Id,
                MaliYil = 2026,
                Donem = 1,
                MuhasebeHesapPlaniId = hesap.Id,
                HesapKodu = hesap.TamKod,
                HesapAdi = hesap.Ad,
                KonsolideMi = false,
                BorcToplam = 100m,
                AlacakToplam = 0m,
                BorcBakiye = 100m,
                AlacakBakiye = 0m,
                NetBakiye = 100m,
                BakiyeTipi = "Borc",
                HesapSeviyesi = 3,
                SonGuncellemeTarihi = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            await db.Database.ExecuteSqlRawAsync(RepairSql);

            // accountUsed has an account-level financial usage (MuhasebeHesapBakiyeleri), so it must not be soft-deleted.
            var accountUsedSurvives = await db.CariKartlar.IgnoreQueryFilters().AnyAsync(x => x.Id == accountUsed.Id && !x.IsDeleted);
            Assert.True(accountUsedSurvives);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(RecreateIndexSql);
            await CleanupCariDataAsync(db, tesis.Id);
        }
    }

    [IntegrationFact]
    public async Task Repair_IkiFinansalKullanilmis_HerIkiKorunur()
    {
        var cs = Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(cs)) return;
        var suffix = Guid.NewGuid().ToString("N")[..10];

        await using var db = AgentTestSupport.CreateDbContext(cs);
        await db.Database.MigrateAsync();
        var (_, _, tesis) = await AgentTestSupport.SeedKurumIlTesisAsync(db, suffix);

        await db.Database.ExecuteSqlRawAsync(DropIndexSql);
        try
        {
            var a = new CariKart
            {
                TesisId = tesis.Id,
                CariTipi = CariKartTipleri.Musteri,
                CariKodu = $"MULTI-{suffix}-0",
                UnvanAdSoyad = "Multi Fin",
                VergiNoTckn = "55555555555",
                VergiNoTcknNormalized = "55555555555",
                AktifMi = true
            };
            var b = new CariKart
            {
                TesisId = tesis.Id,
                CariTipi = CariKartTipleri.Musteri,
                CariKodu = $"MULTI-{suffix}-1",
                UnvanAdSoyad = "Multi Fin",
                VergiNoTckn = "55555555555",
                VergiNoTcknNormalized = "55555555555",
                AktifMi = true
            };
            db.CariKartlar.AddRange(a, b);
            await db.SaveChangesAsync();

            foreach (var card in new[] { a, b })
            {
                db.CariHareketler.Add(new CariHareket
                {
                    CariKartId = card.Id,
                    HareketTarihi = DateTime.UtcNow.Date,
                    BelgeTuru = "TEST",
                    BelgeNo = $"TEST-{suffix}-{card.Id}",
                    Aciklama = "test",
                    BorcTutari = 0m,
                    AlacakTutari = 0m,
                    KapananTutar = 0m,
                    KalanTutar = 0m,
                    ParaBirimi = "TRY",
                    Durum = CariHareketDurumlari.Aktif,
                    KaynakModul = "TEST",
                    KaynakId = 0,
                    KapandiMi = false
                });
            }

            await db.SaveChangesAsync();

            await db.Database.ExecuteSqlRawAsync(RepairSql);

            var aSurvives = await db.CariKartlar.IgnoreQueryFilters().AnyAsync(x => x.Id == a.Id && !x.IsDeleted);
            var bSurvives = await db.CariKartlar.IgnoreQueryFilters().AnyAsync(x => x.Id == b.Id && !x.IsDeleted);
            Assert.True(aSurvives);
            Assert.True(bSurvives);

            // The non-canonical financially-used card must have its normalized key cleared (manual review).
            var canonicalId = Math.Min(a.Id, b.Id);
            var nonCanonicalId = Math.Max(a.Id, b.Id);
            var nonCanonicalNormalized = await db.CariKartlar.IgnoreQueryFilters().Where(x => x.Id == nonCanonicalId).Select(x => x.VergiNoTcknNormalized).SingleAsync();
            Assert.Null(nonCanonicalNormalized);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(RecreateIndexSql);
            await CleanupCariDataAsync(db, tesis.Id);
        }
    }
}
