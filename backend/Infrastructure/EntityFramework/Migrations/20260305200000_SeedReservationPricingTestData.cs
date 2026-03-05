using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260305200000_SeedReservationPricingTestData")]
    public class SeedReservationPricingTestData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @SeedTag nvarchar(128) = N'migration_seed_rezervasyon_test';
                DECLARE @PriceStart datetime2 = '2026-01-01T00:00:00';
                DECLARE @PriceEnd datetime2 = '2028-12-31T23:59:59';

                DECLARE @KonaklamaSadeceOdaId int = (SELECT TOP (1) Id FROM [dbo].[KonaklamaTipleri] WHERE [Kod] = N'SADECE_ODA');
                DECLARE @KonaklamaOdaKahvaltiId int = (SELECT TOP (1) Id FROM [dbo].[KonaklamaTipleri] WHERE [Kod] = N'ODA_KAHVALTI');
                DECLARE @KonaklamaYarimPansiyonId int = (SELECT TOP (1) Id FROM [dbo].[KonaklamaTipleri] WHERE [Kod] = N'YARIM_PANSIYON');

                DECLARE @MisafirMisafirId int = (SELECT TOP (1) Id FROM [dbo].[MisafirTipleri] WHERE [Kod] = N'MISAFIR');
                DECLARE @MisafirKamuId int = (SELECT TOP (1) Id FROM [dbo].[MisafirTipleri] WHERE [Kod] = N'KAMU_PERSONELI');
                DECLARE @MisafirKurumId int = (SELECT TOP (1) Id FROM [dbo].[MisafirTipleri] WHERE [Kod] = N'KURUM_PERSONELI');

                IF @KonaklamaSadeceOdaId IS NOT NULL AND @KonaklamaOdaKahvaltiId IS NOT NULL AND @KonaklamaYarimPansiyonId IS NOT NULL
                   AND @MisafirMisafirId IS NOT NULL AND @MisafirKamuId IS NOT NULL AND @MisafirKurumId IS NOT NULL
                BEGIN
                    ;WITH ActiveRoomTypes AS (
                        SELECT
                            ot.Id AS TesisOdaTipiId,
                            ot.Kapasite
                        FROM [dbo].[TesisOdaTipleri] ot
                        WHERE ot.[AktifMi] = 1
                          AND ot.[IsDeleted] = 0
                    ),
                    KonaklamaTipleri AS (
                        SELECT @KonaklamaSadeceOdaId AS KonaklamaTipiId, CAST(1.00 AS decimal(18,4)) AS KonaklamaKatsayisi
                        UNION ALL SELECT @KonaklamaOdaKahvaltiId, CAST(1.20 AS decimal(18,4))
                        UNION ALL SELECT @KonaklamaYarimPansiyonId, CAST(1.35 AS decimal(18,4))
                    ),
                    MisafirTipleri AS (
                        SELECT @MisafirMisafirId AS MisafirTipiId, CAST(1.00 AS decimal(18,4)) AS MisafirKatsayisi
                        UNION ALL SELECT @MisafirKamuId, CAST(0.90 AS decimal(18,4))
                        UNION ALL SELECT @MisafirKurumId, CAST(0.85 AS decimal(18,4))
                    )
                    INSERT INTO [dbo].[OdaFiyatlari]
                    (
                        [TesisOdaTipiId],
                        [KonaklamaTipiId],
                        [MisafirTipiId],
                        [KisiSayisi],
                        [Fiyat],
                        [ParaBirimi],
                        [BaslangicTarihi],
                        [BitisTarihi],
                        [AktifMi],
                        [IsDeleted],
                        [CreatedAt],
                        [UpdatedAt],
                        [CreatedBy],
                        [UpdatedBy]
                    )
                    SELECT
                        rt.TesisOdaTipiId,
                        kt.KonaklamaTipiId,
                        mt.MisafirTipiId,
                        1,
                        CAST(ROUND(
                            (
                                CASE
                                    WHEN rt.Kapasite = 1 THEN 950
                                    WHEN rt.Kapasite = 2 THEN 1450
                                    WHEN rt.Kapasite = 3 THEN 1950
                                    WHEN rt.Kapasite = 4 THEN 2400
                                    ELSE 800 + (rt.Kapasite * 420)
                                END
                            ) * kt.KonaklamaKatsayisi * mt.MisafirKatsayisi
                        , 2) AS decimal(18,2)) AS Fiyat,
                        N'TRY',
                        @PriceStart,
                        @PriceEnd,
                        1,
                        0,
                        @Now,
                        @Now,
                        @SeedTag,
                        @SeedTag
                    FROM ActiveRoomTypes rt
                    CROSS JOIN KonaklamaTipleri kt
                    CROSS JOIN MisafirTipleri mt
                    WHERE NOT EXISTS
                    (
                        SELECT 1
                        FROM [dbo].[OdaFiyatlari] f
                        WHERE f.[TesisOdaTipiId] = rt.TesisOdaTipiId
                          AND f.[KonaklamaTipiId] = kt.KonaklamaTipiId
                          AND f.[MisafirTipiId] = mt.MisafirTipiId
                          AND f.[KisiSayisi] = 1
                          AND f.[BaslangicTarihi] = @PriceStart
                          AND f.[BitisTarihi] = @PriceEnd
                          AND f.[IsDeleted] = 0
                    );
                END;

                IF NOT EXISTS (SELECT 1 FROM [dbo].[IndirimKurallari] WHERE [Kod] = N'ANADOLU_PERSONEL_15' AND [IsDeleted] = 0)
                BEGIN
                    INSERT INTO [dbo].[IndirimKurallari]
                    (
                        [Kod], [Ad], [IndirimTipi], [Deger], [KapsamTipi], [TesisId], [BaslangicTarihi], [BitisTarihi], [Oncelik], [BirlesebilirMi], [AktifMi],
                        [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                    )
                    VALUES
                    (
                        N'ANADOLU_PERSONEL_15',
                        N'Anadolu Universitesi Personeline %15',
                        N'Yuzde',
                        15,
                        N'Sistem',
                        NULL,
                        '2026-01-01T00:00:00',
                        '2028-12-31T23:59:59',
                        200,
                        1,
                        1,
                        0,
                        @Now,
                        @Now,
                        @SeedTag,
                        @SeedTag
                    );
                END;

                DECLARE @SystemRuleId int = (
                    SELECT TOP (1) [Id]
                    FROM [dbo].[IndirimKurallari]
                    WHERE [Kod] = N'ANADOLU_PERSONEL_15'
                      AND [IsDeleted] = 0
                );
                DECLARE @KurumMisafirId int = (SELECT TOP (1) [Id] FROM [dbo].[MisafirTipleri] WHERE [Kod] = N'KURUM_PERSONELI' AND [IsDeleted] = 0);

                IF @SystemRuleId IS NOT NULL AND @KurumMisafirId IS NOT NULL
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM [dbo].[IndirimKuraliMisafirTipleri]
                        WHERE [IndirimKuraliId] = @SystemRuleId
                          AND [MisafirTipiId] = @KurumMisafirId
                          AND [IsDeleted] = 0
                    )
                    BEGIN
                        INSERT INTO [dbo].[IndirimKuraliMisafirTipleri]
                        (
                            [IndirimKuraliId], [MisafirTipiId],
                            [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                        )
                        VALUES
                        (
                            @SystemRuleId, @KurumMisafirId,
                            0, @Now, @Now, @SeedTag, @SeedTag
                        );
                    END;
                END;

                INSERT INTO [dbo].[IndirimKurallari]
                (
                    [Kod], [Ad], [IndirimTipi], [Deger], [KapsamTipi], [TesisId], [BaslangicTarihi], [BitisTarihi], [Oncelik], [BirlesebilirMi], [AktifMi],
                    [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                )
                SELECT
                    CONCAT(N'TESIS_HOSGELDIN_', t.[Id]),
                    CONCAT(t.[Ad], N' Hosgeldin Indirimi (100 TRY)'),
                    N'Tutar',
                    100,
                    N'Tesis',
                    t.[Id],
                    '2026-01-01T00:00:00',
                    '2028-12-31T23:59:59',
                    100,
                    1,
                    1,
                    0,
                    @Now,
                    @Now,
                    @SeedTag,
                    @SeedTag
                FROM [dbo].[Tesisler] t
                WHERE t.[AktifMi] = 1
                  AND t.[IsDeleted] = 0
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [dbo].[IndirimKurallari] ir
                      WHERE ir.[Kod] = CONCAT(N'TESIS_HOSGELDIN_', t.[Id])
                        AND ir.[IsDeleted] = 0
                  );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @SeedTag nvarchar(128) = N'migration_seed_rezervasyon_test';

                DELETE FROM [dbo].[IndirimKuraliKonaklamaTipleri]
                WHERE [CreatedBy] = @SeedTag;

                DELETE FROM [dbo].[IndirimKuraliMisafirTipleri]
                WHERE [CreatedBy] = @SeedTag;

                DELETE FROM [dbo].[IndirimKurallari]
                WHERE [CreatedBy] = @SeedTag;

                DELETE FROM [dbo].[OdaFiyatlari]
                WHERE [CreatedBy] = @SeedTag;
                """);
        }
    }
}
