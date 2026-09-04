using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddCariKartMusteriIdentityUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VergiNoTcknNormalized",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            // Backfill legacy VergiNoTckn values into VergiNoTcknNormalized using the same logic as
            // CariKartIdentityNormalizer.NormalizeVergiNoTckn (trim, strip whitespace / '-' / '.',
            // uppercase, truncate to 32). NULLIF turns an all-punctuation value into NULL.
            migrationBuilder.Sql(@"
                UPDATE c
                SET VergiNoTcknNormalized = NULLIF(
                    LEFT(
                        UPPER(
                            REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                                LTRIM(RTRIM(c.VergiNoTckn)),
                                '-', ''), '.', ''), ' ', ''), CHAR(9), ''), CHAR(13), ''), CHAR(10), '')
                        ),
                        32),
                    '')
                FROM muhasebe.CariKartlar c
                WHERE c.IsDeleted = 0
                  AND c.CariTipi IN ('Musteri','KurumsalMusteri')
                  AND c.VergiNoTckn IS NOT NULL
                  AND LTRIM(RTRIM(c.VergiNoTckn)) <> '';
            ");

            // Duplicate repair: pick a deterministic canonical card per (TesisId, normalized) group,
            // soft-delete unused non-canonical cards (moving their operational links to canonical),
            // and NULL the normalized key on financially-used non-canonical cards so the unique index
            // can be created without a violation. Financially-used legacy cards are never hard-deleted
            // or auto-merged; they are left active for manual review.
            migrationBuilder.Sql(@"
                IF OBJECT_ID('tempdb..#DupGroups') IS NOT NULL DROP TABLE #DupGroups;
                SELECT TesisId, VergiNoTcknNormalized
                INTO #DupGroups
                FROM muhasebe.CariKartlar
                WHERE IsDeleted = 0 AND AktifMi = 1 AND TesisId IS NOT NULL
                  AND VergiNoTcknNormalized IS NOT NULL
                  AND CariTipi IN ('Musteri','KurumsalMusteri')
                GROUP BY TesisId, VergiNoTcknNormalized
                HAVING COUNT(*) > 1;

                IF OBJECT_ID('tempdb..#DupCards') IS NOT NULL DROP TABLE #DupCards;
                SELECT
                    c.Id,
                    c.TesisId,
                    c.VergiNoTcknNormalized,
                    c.MuhasebeHesapPlaniId,
                    CASE WHEN EXISTS (SELECT 1 FROM muhasebe.CariHareketler x WHERE x.CariKartId = c.Id)
                          OR EXISTS (SELECT 1 FROM muhasebe.TahsilatOdemeBelgeleri x WHERE x.CariKartId = c.Id)
                          OR EXISTS (SELECT 1 FROM muhasebe.SatisBelgeleri x WHERE x.CariKartId = c.Id)
                          OR EXISTS (SELECT 1 FROM muhasebe.BankaHareketleri x WHERE x.CariKartId = c.Id)
                          OR EXISTS (SELECT 1 FROM muhasebe.KasaHareketleri x WHERE x.CariKartId = c.Id)
                          OR EXISTS (SELECT 1 FROM muhasebe.StokHareketleri x WHERE x.CariKartId = c.Id)
                          OR EXISTS (SELECT 1 FROM muhasebe.MuhasebeFisSatirlari x WHERE x.CariKartId = c.Id)
                          OR EXISTS (SELECT 1 FROM entegrasyon.PosOdemeIslemleri x WHERE x.CariKartId = c.Id)
                        THEN 1 ELSE 0 END AS FinansalKullanildi,
                    CASE WHEN EXISTS (SELECT 1 FROM dbo.Rezervasyonlar x WHERE x.CariKartId = c.Id)
                          OR EXISTS (SELECT 1 FROM dbo.Tesisler x WHERE x.RezervasyonMisafirVarsayilanCariKartId = c.Id)
                        THEN 1 ELSE 0 END AS IsKaydiVar
                INTO #DupCards
                FROM muhasebe.CariKartlar c
                INNER JOIN #DupGroups g ON g.TesisId = c.TesisId AND g.VergiNoTcknNormalized = c.VergiNoTcknNormalized
                WHERE c.IsDeleted = 0 AND c.AktifMi = 1 AND c.CariTipi IN ('Musteri','KurumsalMusteri');

                IF OBJECT_ID('tempdb..#Canonical') IS NOT NULL DROP TABLE #Canonical;
                SELECT
                    g.TesisId,
                    g.VergiNoTcknNormalized,
                    COALESCE(
                        (SELECT MIN(d2.Id) FROM #DupCards d2
                          WHERE d2.TesisId = g.TesisId AND d2.VergiNoTcknNormalized = g.VergiNoTcknNormalized
                            AND d2.FinansalKullanildi = 1),
                        (SELECT MIN(d2.Id) FROM #DupCards d2
                          WHERE d2.TesisId = g.TesisId AND d2.VergiNoTcknNormalized = g.VergiNoTcknNormalized
                            AND d2.IsKaydiVar = 1),
                        (SELECT MIN(d2.Id) FROM #DupCards d2
                          WHERE d2.TesisId = g.TesisId AND d2.VergiNoTcknNormalized = g.VergiNoTcknNormalized)
                    ) AS CanonicalId
                INTO #Canonical
                FROM #DupGroups g;

                -- Financially-used non-canonical cards: keep active, drop normalized key (manual review).
                UPDATE c
                SET VergiNoTcknNormalized = NULL
                FROM muhasebe.CariKartlar c
                INNER JOIN #DupCards d ON d.Id = c.Id
                INNER JOIN #Canonical k ON k.TesisId = d.TesisId AND k.VergiNoTcknNormalized = d.VergiNoTcknNormalized
                WHERE d.Id <> k.CanonicalId AND d.FinansalKullanildi = 1;

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

                UPDATE c
                SET IsDeleted = 1, AktifMi = 0, DeletedAt = GETUTCDATE(), DeletedBy = 'migration:dedup'
                FROM muhasebe.CariKartlar c
                INNER JOIN #DupCards d ON d.Id = c.Id
                INNER JOIN #Canonical k ON k.TesisId = d.TesisId AND k.VergiNoTcknNormalized = d.VergiNoTcknNormalized
                WHERE d.Id <> k.CanonicalId AND d.FinansalKullanildi = 0;

                DROP TABLE #Canonical;
                DROP TABLE #DupCards;
                DROP TABLE #DupGroups;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_TesisId_VergiNoTcknNormalized_Musteri",
                schema: "muhasebe",
                table: "CariKartlar",
                columns: new[] { "TesisId", "VergiNoTcknNormalized" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1 AND [TesisId] IS NOT NULL AND [VergiNoTcknNormalized] IS NOT NULL AND [CariTipi] IN ('Musteri', 'KurumsalMusteri')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CariKartlar_TesisId_VergiNoTcknNormalized_Musteri",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "VergiNoTcknNormalized",
                schema: "muhasebe",
                table: "CariKartlar");
        }
    }
}
