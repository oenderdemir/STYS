using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260329124500_SeedSampleConsumptionPoints")]
public class SeedSampleConsumptionPoints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ;WITH HedefBinalar AS
            (
                SELECT
                    B.[Id] AS [BinaId],
                    B.[TesisId],
                    ROW_NUMBER() OVER (PARTITION BY B.[TesisId] ORDER BY B.[Id]) AS [SiraNo]
                FROM [dbo].[Binalar] B
                INNER JOIN [dbo].[Tesisler] T ON T.[Id] = B.[TesisId]
                WHERE B.[AktifMi] = 1
                  AND B.[IsDeleted] = 0
                  AND T.[AktifMi] = 1
                  AND T.[IsDeleted] = 0
            )
            INSERT INTO [dbo].[IsletmeAlanlari]
                ([BinaId], [IsletmeAlaniSinifiId], [OzelAd], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy])
            SELECT
                HB.[BinaId],
                IAS.[Id],
                V.[OzelAd],
                1,
                0,
                SYSUTCDATETIME(),
                N'system',
                SYSUTCDATETIME(),
                N'system'
            FROM HedefBinalar HB
            INNER JOIN (VALUES
                (N'RESTORAN', N'Ana Restoran'),
                (N'BAR', N'Lobi Bar'),
                (N'ODA_SERVISI', N'Oda Servisi')
            ) V([Kod], [OzelAd]) ON 1 = 1
            INNER JOIN [dbo].[IsletmeAlaniSiniflari] IAS
                ON IAS.[Kod] = V.[Kod]
               AND IAS.[IsDeleted] = 0
            WHERE HB.[SiraNo] = 1
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [dbo].[IsletmeAlanlari] IA
                  INNER JOIN [dbo].[Binalar] B2 ON B2.[Id] = IA.[BinaId]
                  WHERE IA.[AktifMi] = 1
                    AND IA.[IsDeleted] = 0
                    AND B2.[TesisId] = HB.[TesisId]
                    AND IA.[IsletmeAlaniSinifiId] = IAS.[Id]
                    AND ISNULL(LTRIM(RTRIM(IA.[OzelAd])), N'') = V.[OzelAd]
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE IA
            FROM [dbo].[IsletmeAlanlari] IA
            INNER JOIN [dbo].[IsletmeAlaniSiniflari] IAS ON IAS.[Id] = IA.[IsletmeAlaniSinifiId]
            WHERE IA.[CreatedBy] = N'system'
              AND IA.[OzelAd] IN (N'Ana Restoran', N'Lobi Bar', N'Oda Servisi')
              AND IAS.[Kod] IN (N'RESTORAN', N'BAR', N'ODA_SERVISI')
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [dbo].[RezervasyonKonaklamaHakkiTuketimKayitlari] TK
                  WHERE TK.[IsletmeAlaniId] = IA.[Id]
                    AND TK.[IsDeleted] = 0
              );
            """);
    }
}
