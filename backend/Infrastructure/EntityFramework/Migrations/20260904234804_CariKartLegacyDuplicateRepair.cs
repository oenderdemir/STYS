using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <summary>
    /// Idempotent legacy data repair for the müşteri cari-kart duplicate fix. This runs AFTER
    /// AddCariKartMusteriIdentityUniqueIndex (which only added the column + unique index). It:
    ///   - backfills VergiNoTcknNormalized from legacy VergiNoTckn,
    ///   - repairs duplicate groups (deterministic canonical + safe soft-delete),
    ///   - leaves financially-used legacy cards active for manual review,
    ///   - ensures the unique index exists in the correct state.
    /// The unique index is dropped before the data repair and re-created afterwards so backfilled
    /// values never violate it mid-repair.
    /// </summary>
    public partial class CariKartLegacyDuplicateRepair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CariKartlar_TesisId_VergiNoTcknNormalized_Musteri' AND object_id = OBJECT_ID('muhasebe.CariKartlar'))
                    DROP INDEX [IX_CariKartlar_TesisId_VergiNoTcknNormalized_Musteri] ON muhasebe.CariKartlar;
            ");

            migrationBuilder.Sql(@"
                -- Backfill legacy VergiNoTckn into VergiNoTcknNormalized (same rules as
                -- CariKartIdentityNormalizer: trim, strip whitespace/'-'/'.', uppercase, truncate 32).
                UPDATE c
                SET VergiNoTcknNormalized = NULLIF(
                    LEFT(UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(c.VergiNoTckn)), '-', ''), '.', ''), ' ', ''), CHAR(9), ''), CHAR(13), ''), CHAR(10), '')), 32),
                    '')
                FROM muhasebe.CariKartlar c
                WHERE c.IsDeleted = 0
                  AND c.CariTipi IN ('Musteri','KurumsalMusteri')
                  AND c.VergiNoTckn IS NOT NULL
                  AND LTRIM(RTRIM(c.VergiNoTckn)) <> '';
            ");

            migrationBuilder.Sql(@"
                IF OBJECT_ID('tempdb..#DupGroups') IS NOT NULL DROP TABLE #DupGroups;
                SELECT TesisId, VergiNoTcknNormalized INTO #DupGroups
                FROM muhasebe.CariKartlar
                WHERE IsDeleted = 0 AND AktifMi = 1 AND TesisId IS NOT NULL AND VergiNoTcknNormalized IS NOT NULL
                  AND CariTipi IN ('Musteri','KurumsalMusteri')
                GROUP BY TesisId, VergiNoTcknNormalized HAVING COUNT(*) > 1;

                IF OBJECT_ID('tempdb..#DupCards') IS NOT NULL DROP TABLE #DupCards;
                SELECT
                    c.Id, c.TesisId, c.VergiNoTcknNormalized, c.MuhasebeHesapPlaniId,
                    c.Ad, c.Soyad, c.VergiDairesi, c.Telefon, c.Eposta, c.Adres, c.Il, c.Ilce,
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

                -- Financially-used non-canonical cards: keep active, drop normalized key (manual review).
                UPDATE c SET VergiNoTcknNormalized = NULL
                FROM muhasebe.CariKartlar c
                INNER JOIN #DupCards d ON d.Id = c.Id
                INNER JOIN #Canonical k ON k.TesisId = d.TesisId AND k.VergiNoTcknNormalized = d.VergiNoTcknNormalized
                WHERE d.Id <> k.CanonicalId AND d.FinansalKullanildi = 1;

                -- Profile merge: fill canonical's EMPTY fields from non-canonical unused cards only when a
                -- single non-empty value is agreed; ambiguous values leave the canonical value untouched.
                IF OBJECT_ID('tempdb..#ProfileFill') IS NOT NULL DROP TABLE #ProfileFill;
                SELECT k.CanonicalId,
                    CASE WHEN MIN(NULLIF(LTRIM(RTRIM(d.Ad)), '')) = MAX(NULLIF(LTRIM(RTRIM(d.Ad)), '')) THEN MIN(NULLIF(LTRIM(RTRIM(d.Ad)), '')) END AS Ad,
                    CASE WHEN MIN(NULLIF(LTRIM(RTRIM(d.Soyad)), '')) = MAX(NULLIF(LTRIM(RTRIM(d.Soyad)), '')) THEN MIN(NULLIF(LTRIM(RTRIM(d.Soyad)), '')) END AS Soyad,
                    CASE WHEN MIN(NULLIF(LTRIM(RTRIM(d.VergiDairesi)), '')) = MAX(NULLIF(LTRIM(RTRIM(d.VergiDairesi)), '')) THEN MIN(NULLIF(LTRIM(RTRIM(d.VergiDairesi)), '')) END AS VergiDairesi,
                    CASE WHEN MIN(NULLIF(LTRIM(RTRIM(d.Telefon)), '')) = MAX(NULLIF(LTRIM(RTRIM(d.Telefon)), '')) THEN MIN(NULLIF(LTRIM(RTRIM(d.Telefon)), '')) END AS Telefon,
                    CASE WHEN MIN(NULLIF(LTRIM(RTRIM(d.Eposta)), '')) = MAX(NULLIF(LTRIM(RTRIM(d.Eposta)), '')) THEN MIN(NULLIF(LTRIM(RTRIM(d.Eposta)), '')) END AS Eposta,
                    CASE WHEN MIN(NULLIF(LTRIM(RTRIM(d.Adres)), '')) = MAX(NULLIF(LTRIM(RTRIM(d.Adres)), '')) THEN MIN(NULLIF(LTRIM(RTRIM(d.Adres)), '')) END AS Adres,
                    CASE WHEN MIN(NULLIF(LTRIM(RTRIM(d.Il)), '')) = MAX(NULLIF(LTRIM(RTRIM(d.Il)), '')) THEN MIN(NULLIF(LTRIM(RTRIM(d.Il)), '')) END AS Il,
                    CASE WHEN MIN(NULLIF(LTRIM(RTRIM(d.Ilce)), '')) = MAX(NULLIF(LTRIM(RTRIM(d.Ilce)), '')) THEN MIN(NULLIF(LTRIM(RTRIM(d.Ilce)), '')) END AS Ilce
                INTO #ProfileFill
                FROM #DupCards d
                INNER JOIN #Canonical k ON k.TesisId = d.TesisId AND k.VergiNoTcknNormalized = d.VergiNoTcknNormalized
                WHERE d.Id <> k.CanonicalId AND d.FinansalKullanildi = 0
                GROUP BY k.CanonicalId;

                UPDATE c
                SET
                    Ad = CASE WHEN c.Ad IS NULL OR LTRIM(RTRIM(c.Ad)) = '' THEN f.Ad ELSE c.Ad END,
                    Soyad = CASE WHEN c.Soyad IS NULL OR LTRIM(RTRIM(c.Soyad)) = '' THEN f.Soyad ELSE c.Soyad END,
                    VergiDairesi = CASE WHEN c.VergiDairesi IS NULL OR LTRIM(RTRIM(c.VergiDairesi)) = '' THEN f.VergiDairesi ELSE c.VergiDairesi END,
                    Telefon = CASE WHEN c.Telefon IS NULL OR LTRIM(RTRIM(c.Telefon)) = '' THEN f.Telefon ELSE c.Telefon END,
                    Eposta = CASE WHEN c.Eposta IS NULL OR LTRIM(RTRIM(c.Eposta)) = '' THEN f.Eposta ELSE c.Eposta END,
                    Adres = CASE WHEN c.Adres IS NULL OR LTRIM(RTRIM(c.Adres)) = '' THEN f.Adres ELSE c.Adres END,
                    Il = CASE WHEN c.Il IS NULL OR LTRIM(RTRIM(c.Il)) = '' THEN f.Il ELSE c.Il END,
                    Ilce = CASE WHEN c.Ilce IS NULL OR LTRIM(RTRIM(c.Ilce)) = '' THEN f.Ilce ELSE c.Ilce END
                FROM muhasebe.CariKartlar c
                INNER JOIN #ProfileFill f ON f.CanonicalId = c.Id;

                DROP TABLE #ProfileFill;

                -- Unused non-canonical cards: move operational links to canonical, then soft-delete.
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
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CariKartlar_TesisId_VergiNoTcknNormalized_Musteri' AND object_id = OBJECT_ID('muhasebe.CariKartlar'))
                    CREATE UNIQUE INDEX [IX_CariKartlar_TesisId_VergiNoTcknNormalized_Musteri]
                    ON [muhasebe].[CariKartlar] ([TesisId], [VergiNoTcknNormalized])
                    WHERE [IsDeleted] = 0 AND [AktifMi] = 1 AND [TesisId] IS NOT NULL AND [VergiNoTcknNormalized] IS NOT NULL AND [CariTipi] IN ('Musteri', 'KurumsalMusteri');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
