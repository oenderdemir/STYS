using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260410223000_SeedRestaurantsForAllTesisWithSharedMenuTemplates")]
public partial class SeedRestaurantsForAllTesisWithSharedMenuTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @SeedTag nvarchar(128) = N'migration_seed_restoran_shared_templates_v1';

            INSERT INTO [restoran].[Restoranlar]
            (
                [TesisId], [Ad], [Aciklama], [AktifMi], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            SELECT
                t.[Id],
                N'Ana Restoran',
                CONCAT(t.[Ad], N' icin otomatik olusturulan restoran'),
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
                  FROM [restoran].[Restoranlar] r
                  WHERE r.[TesisId] = t.[Id]
                    AND r.[Ad] = N'Ana Restoran'
                    AND r.[IsDeleted] = 0
                    AND r.[AktifMi] = 1
              );

            DECLARE @KategoriTemplate TABLE
            (
                [Ad] nvarchar(128) NOT NULL,
                [SiraNo] int NOT NULL
            );

            INSERT INTO @KategoriTemplate ([Ad], [SiraNo])
            VALUES
            (N'Corbalar', 1),
            (N'Ana Yemekler', 2),
            (N'Tatlilar', 3),
            (N'Icecekler', 4);

            DECLARE @UrunTemplate TABLE
            (
                [KategoriAd] nvarchar(128) NOT NULL,
                [UrunAd] nvarchar(128) NOT NULL,
                [Aciklama] nvarchar(512) NULL,
                [Fiyat] decimal(18,2) NOT NULL,
                [HazirlamaSuresiDakika] int NOT NULL
            );

            INSERT INTO @UrunTemplate ([KategoriAd], [UrunAd], [Aciklama], [Fiyat], [HazirlamaSuresiDakika])
            VALUES
            (N'Corbalar', N'Mercimek Corbasi', N'Gunun corbasi', 75.00, 10),
            (N'Corbalar', N'Ezogelin Corbasi', N'Acili ezogelin', 85.00, 10),
            (N'Ana Yemekler', N'Izgara Tavuk', N'Pilav ile servis', 220.00, 25),
            (N'Ana Yemekler', N'Kofte Izgara', N'Patates ve salata ile', 245.00, 25),
            (N'Tatlilar', N'Sutlac', N'Firin sutlac', 95.00, 8),
            (N'Tatlilar', N'Baklava', N'4 dilim', 130.00, 5),
            (N'Icecekler', N'Ayran', N'300 ml', 35.00, 2),
            (N'Icecekler', N'Kola', N'330 ml', 45.00, 2);

            INSERT INTO [restoran].[RestoranMasalari]
            (
                [RestoranId], [MasaNo], [Kapasite], [Durum], [AktifMi], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            SELECT
                r.[Id],
                m.[MasaNo],
                m.[Kapasite],
                m.[Durum],
                1,
                0,
                @Now,
                @Now,
                @SeedTag,
                @SeedTag
            FROM [restoran].[Restoranlar] r
            CROSS APPLY
            (
                VALUES
                (N'M-01', 4, N'Musait'),
                (N'M-02', 4, N'Musait'),
                (N'M-03', 6, N'Musait'),
                (N'M-04', 2, N'Rezerve'),
                (N'M-05', 4, N'Musait')
            ) m([MasaNo], [Kapasite], [Durum])
            WHERE r.[AktifMi] = 1
              AND r.[IsDeleted] = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [restoran].[RestoranMasalari] rm
                  WHERE rm.[RestoranId] = r.[Id]
                    AND rm.[MasaNo] = m.[MasaNo]
                    AND rm.[IsDeleted] = 0
              );

            INSERT INTO [restoran].[RestoranMenuKategorileri]
            (
                [RestoranId], [Ad], [SiraNo], [AktifMi], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            SELECT
                r.[Id],
                k.[Ad],
                k.[SiraNo],
                1,
                0,
                @Now,
                @Now,
                @SeedTag,
                @SeedTag
            FROM [restoran].[Restoranlar] r
            CROSS JOIN @KategoriTemplate k
            WHERE r.[AktifMi] = 1
              AND r.[IsDeleted] = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [restoran].[RestoranMenuKategorileri] c
                  WHERE c.[RestoranId] = r.[Id]
                    AND c.[Ad] = k.[Ad]
                    AND c.[IsDeleted] = 0
              );

            INSERT INTO [restoran].[RestoranMenuUrunleri]
            (
                [RestoranMenuKategoriId], [Ad], [Aciklama], [Fiyat], [ParaBirimi], [HazirlamaSuresiDakika], [AktifMi], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            SELECT
                c.[Id],
                u.[UrunAd],
                u.[Aciklama],
                u.[Fiyat],
                N'TRY',
                u.[HazirlamaSuresiDakika],
                1,
                0,
                @Now,
                @Now,
                @SeedTag,
                @SeedTag
            FROM [restoran].[RestoranMenuKategorileri] c
            INNER JOIN @UrunTemplate u ON u.[KategoriAd] = c.[Ad]
            INNER JOIN [restoran].[Restoranlar] r ON r.[Id] = c.[RestoranId]
            WHERE r.[AktifMi] = 1
              AND r.[IsDeleted] = 0
              AND c.[AktifMi] = 1
              AND c.[IsDeleted] = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [restoran].[RestoranMenuUrunleri] p
                  WHERE p.[RestoranMenuKategoriId] = c.[Id]
                    AND p.[Ad] = u.[UrunAd]
                    AND p.[IsDeleted] = 0
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @SeedTag nvarchar(128) = N'migration_seed_restoran_shared_templates_v1';

            DELETE FROM [restoran].[RestoranMenuUrunleri]
            WHERE [CreatedBy] = @SeedTag;

            DELETE FROM [restoran].[RestoranMenuKategorileri]
            WHERE [CreatedBy] = @SeedTag;

            DELETE FROM [restoran].[RestoranMasalari]
            WHERE [CreatedBy] = @SeedTag;

            DELETE FROM [restoran].[Restoranlar]
            WHERE [CreatedBy] = @SeedTag;
            """);
    }
}
