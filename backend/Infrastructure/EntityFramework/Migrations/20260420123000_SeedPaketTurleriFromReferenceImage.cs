using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260420123000_SeedPaketTurleriFromReferenceImage")]
public partial class SeedPaketTurleriFromReferenceImage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @Rows TABLE
            (
                Ad nvarchar(128) NOT NULL,
                KisaAd nvarchar(16) NOT NULL
            );

            INSERT INTO @Rows (Ad, KisaAd) VALUES
            (N'Adet', N'Ad.'),
            (N'Kilogram', N'Kg.'),
            (N'Cuval', N'Cuv.'),
            (N'Kasa', N'Kas.'),
            (N'Koli', N'Kol.'),
            (N'Teneke', N'Ten.'),
            (N'Kova', N'Kov.'),
            (N'Paket', N'Pk.'),
            (N'Lire', N'L.'),
            (N'Demet', N'Dm.');

            -- Onceki seedde "Litre" kaldiysa gorseldeki "Lire" ile hizala.
            UPDATE p
            SET p.Ad = N'Lire',
                p.KisaAd = N'L.',
                p.AktifMi = 1,
                p.IsDeleted = 0,
                p.DeletedAt = NULL,
                p.DeletedBy = NULL,
                p.UpdatedAt = @Now,
                p.UpdatedBy = N'system'
            FROM [muhasebe].[PaketTurleri] p
            WHERE p.Ad = N'Litre';

            MERGE [muhasebe].[PaketTurleri] AS target
            USING @Rows AS source
                ON target.Ad = source.Ad
            WHEN MATCHED THEN
                UPDATE SET
                    target.KisaAd = source.KisaAd,
                    target.AktifMi = 1,
                    target.IsDeleted = 0,
                    target.DeletedAt = NULL,
                    target.DeletedBy = NULL,
                    target.UpdatedAt = @Now,
                    target.UpdatedBy = N'system'
            WHEN NOT MATCHED THEN
                INSERT (Ad, KisaAd, AktifMi, IsDeleted, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                VALUES (source.Ad, source.KisaAd, 1, 0, @Now, @Now, N'system', N'system');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM [muhasebe].[PaketTurleri]
            WHERE [Ad] IN (N'Adet', N'Kilogram', N'Cuval', N'Kasa', N'Koli', N'Teneke', N'Kova', N'Paket', N'Lire', N'Demet')
              AND [CreatedBy] = N'system';
            """);
    }
}
