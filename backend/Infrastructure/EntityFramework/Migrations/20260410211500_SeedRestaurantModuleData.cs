using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260410211500_SeedRestaurantModuleData")]
public partial class SeedRestaurantModuleData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @SeedTag nvarchar(128) = N'migration_seed_restoran_module_v1';

            IF EXISTS (
                SELECT 1
                FROM [restoran].[Restoranlar]
                WHERE [CreatedBy] = @SeedTag
                  AND [IsDeleted] = 0
            )
            BEGIN
                RETURN;
            END;

            DECLARE @TesisId int = (
                SELECT TOP (1) t.[Id]
                FROM [dbo].[Tesisler] t
                WHERE t.[AktifMi] = 1
                  AND t.[IsDeleted] = 0
                ORDER BY t.[Id]
            );

            IF @TesisId IS NULL
            BEGIN
                RETURN;
            END;

            INSERT INTO [restoran].[Restoranlar]
            (
                [TesisId], [Ad], [Aciklama], [AktifMi], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            VALUES
            (
                @TesisId, N'Ana Restoran', N'Ornek restoran seed verisi', 1, 0,
                @Now, @Now, @SeedTag, @SeedTag
            );

            DECLARE @RestoranId int = CAST(SCOPE_IDENTITY() AS int);

            INSERT INTO [restoran].[RestoranMasalari]
            (
                [RestoranId], [MasaNo], [Kapasite], [Durum], [AktifMi], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            VALUES
            (@RestoranId, N'M-01', 4, N'Musait', 1, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@RestoranId, N'M-02', 4, N'Musait', 1, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@RestoranId, N'M-03', 6, N'Musait', 1, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@RestoranId, N'M-04', 2, N'Rezerve', 1, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@RestoranId, N'M-05', 4, N'Kapali', 1, 0, @Now, @Now, @SeedTag, @SeedTag);

            DECLARE @Kategoriler TABLE ([Id] int, [Ad] nvarchar(128));

            INSERT INTO [restoran].[RestoranMenuKategorileri]
            (
                [RestoranId], [Ad], [SiraNo], [AktifMi], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            OUTPUT inserted.[Id], inserted.[Ad] INTO @Kategoriler([Id], [Ad])
            VALUES
            (@RestoranId, N'Corbalar', 1, 1, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@RestoranId, N'Ana Yemekler', 2, 1, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@RestoranId, N'Icecekler', 3, 1, 0, @Now, @Now, @SeedTag, @SeedTag);

            DECLARE @CorbalarId int = (SELECT TOP (1) [Id] FROM @Kategoriler WHERE [Ad] = N'Corbalar');
            DECLARE @AnaYemeklerId int = (SELECT TOP (1) [Id] FROM @Kategoriler WHERE [Ad] = N'Ana Yemekler');
            DECLARE @IceceklerId int = (SELECT TOP (1) [Id] FROM @Kategoriler WHERE [Ad] = N'Icecekler');

            DECLARE @Urunler TABLE ([Id] int, [Ad] nvarchar(128), [Fiyat] decimal(18,2));

            INSERT INTO [restoran].[RestoranMenuUrunleri]
            (
                [RestoranMenuKategoriId], [Ad], [Aciklama], [Fiyat], [ParaBirimi], [HazirlamaSuresiDakika], [AktifMi], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            OUTPUT inserted.[Id], inserted.[Ad], inserted.[Fiyat] INTO @Urunler([Id], [Ad], [Fiyat])
            VALUES
            (@CorbalarId, N'Mercimek Corbasi', N'Gunun corbasi', 75.00, N'TRY', 10, 1, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@CorbalarId, N'Ezogelin Corbasi', N'Acili ezogelin', 85.00, N'TRY', 10, 1, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@AnaYemeklerId, N'Izgara Tavuk', N'Pilav ile servis', 220.00, N'TRY', 25, 1, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@AnaYemeklerId, N'Adana Kebap', N'Lavas ve salata ile', 260.00, N'TRY', 30, 1, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@IceceklerId, N'Ayran', N'300 ml', 35.00, N'TRY', 2, 1, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@IceceklerId, N'Kola', N'330 ml', 45.00, N'TRY', 2, 1, 0, @Now, @Now, @SeedTag, @SeedTag);

            DECLARE @MercimekId int = (SELECT TOP (1) [Id] FROM @Urunler WHERE [Ad] = N'Mercimek Corbasi');
            DECLARE @MercimekFiyat decimal(18,2) = (SELECT TOP (1) [Fiyat] FROM @Urunler WHERE [Ad] = N'Mercimek Corbasi');
            DECLARE @IzgaraTavukId int = (SELECT TOP (1) [Id] FROM @Urunler WHERE [Ad] = N'Izgara Tavuk');
            DECLARE @IzgaraTavukFiyat decimal(18,2) = (SELECT TOP (1) [Fiyat] FROM @Urunler WHERE [Ad] = N'Izgara Tavuk');
            DECLARE @AdanaId int = (SELECT TOP (1) [Id] FROM @Urunler WHERE [Ad] = N'Adana Kebap');
            DECLARE @AdanaFiyat decimal(18,2) = (SELECT TOP (1) [Fiyat] FROM @Urunler WHERE [Ad] = N'Adana Kebap');
            DECLARE @AyranId int = (SELECT TOP (1) [Id] FROM @Urunler WHERE [Ad] = N'Ayran');
            DECLARE @AyranFiyat decimal(18,2) = (SELECT TOP (1) [Fiyat] FROM @Urunler WHERE [Ad] = N'Ayran');

            DECLARE @Masa1Id int = (
                SELECT TOP (1) [Id]
                FROM [restoran].[RestoranMasalari]
                WHERE [RestoranId] = @RestoranId
                  AND [MasaNo] = N'M-01'
                  AND [IsDeleted] = 0
                ORDER BY [Id]
            );

            DECLARE @Masa2Id int = (
                SELECT TOP (1) [Id]
                FROM [restoran].[RestoranMasalari]
                WHERE [RestoranId] = @RestoranId
                  AND [MasaNo] = N'M-02'
                  AND [IsDeleted] = 0
                ORDER BY [Id]
            );

            DECLARE @RandomSuffix nvarchar(12) = RIGHT(REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''), 12);
            DECLARE @SiparisNo1 nvarchar(64) = CONCAT(N'SEED-RST-', @RestoranId, N'-A-', @RandomSuffix);
            DECLARE @SiparisNo2 nvarchar(64) = CONCAT(N'SEED-RST-', @RestoranId, N'-B-', @RandomSuffix);

            INSERT INTO [restoran].[RestoranSiparisleri]
            (
                [RestoranId], [RestoranMasaId], [SiparisNo], [SiparisDurumu], [ToplamTutar], [OdenenTutar], [KalanTutar],
                [ParaBirimi], [OdemeDurumu], [Notlar], [SiparisTarihi], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            VALUES
            (
                @RestoranId, @Masa1Id, @SiparisNo1, N'Hazirlaniyor', 0, 0, 0,
                N'TRY', N'Odenmedi', N'Seed acik siparis', DATEADD(minute, -30, @Now), 0,
                @Now, @Now, @SeedTag, @SeedTag
            );

            DECLARE @Siparis1Id int = CAST(SCOPE_IDENTITY() AS int);

            INSERT INTO [restoran].[RestoranSiparisKalemleri]
            (
                [RestoranSiparisId], [RestoranMenuUrunId], [UrunAdiSnapshot], [BirimFiyat], [Miktar], [SatirToplam], [Notlar], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            VALUES
            (@Siparis1Id, @MercimekId, N'Mercimek Corbasi', @MercimekFiyat, 2.00, 2.00 * @MercimekFiyat, NULL, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@Siparis1Id, @IzgaraTavukId, N'Izgara Tavuk', @IzgaraTavukFiyat, 1.00, 1.00 * @IzgaraTavukFiyat, N'Soslu', 0, @Now, @Now, @SeedTag, @SeedTag);

            UPDATE s
            SET
                [ToplamTutar] = totals.[Toplam],
                [OdenenTutar] = 0,
                [KalanTutar] = totals.[Toplam],
                [OdemeDurumu] = N'Odenmedi'
            FROM [restoran].[RestoranSiparisleri] s
            CROSS APPLY
            (
                SELECT ISNULL(SUM(k.[SatirToplam]), 0) AS [Toplam]
                FROM [restoran].[RestoranSiparisKalemleri] k
                WHERE k.[RestoranSiparisId] = s.[Id]
                  AND k.[IsDeleted] = 0
            ) totals
            WHERE s.[Id] = @Siparis1Id;

            INSERT INTO [restoran].[RestoranSiparisleri]
            (
                [RestoranId], [RestoranMasaId], [SiparisNo], [SiparisDurumu], [ToplamTutar], [OdenenTutar], [KalanTutar],
                [ParaBirimi], [OdemeDurumu], [Notlar], [SiparisTarihi], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            VALUES
            (
                @RestoranId, @Masa2Id, @SiparisNo2, N'Tamamlandi', 0, 0, 0,
                N'TRY', N'Odenmedi', N'Seed tamamlanmis siparis', DATEADD(hour, -4, @Now), 0,
                @Now, @Now, @SeedTag, @SeedTag
            );

            DECLARE @Siparis2Id int = CAST(SCOPE_IDENTITY() AS int);

            INSERT INTO [restoran].[RestoranSiparisKalemleri]
            (
                [RestoranSiparisId], [RestoranMenuUrunId], [UrunAdiSnapshot], [BirimFiyat], [Miktar], [SatirToplam], [Notlar], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            VALUES
            (@Siparis2Id, @AdanaId, N'Adana Kebap', @AdanaFiyat, 1.00, 1.00 * @AdanaFiyat, NULL, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@Siparis2Id, @AyranId, N'Ayran', @AyranFiyat, 2.00, 2.00 * @AyranFiyat, NULL, 0, @Now, @Now, @SeedTag, @SeedTag);

            DECLARE @Siparis2Toplam decimal(18,2) = (
                SELECT ISNULL(SUM(k.[SatirToplam]), 0)
                FROM [restoran].[RestoranSiparisKalemleri] k
                WHERE k.[RestoranSiparisId] = @Siparis2Id
                  AND k.[IsDeleted] = 0
            );

            INSERT INTO [restoran].[RestoranOdemeleri]
            (
                [RestoranSiparisId], [OdemeTipi], [Tutar], [ParaBirimi], [OdemeTarihi], [Aciklama],
                [RezervasyonId], [RezervasyonOdemeId], [Durum], [IslemReferansNo], [IsDeleted],
                [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            VALUES
            (
                @Siparis2Id, N'Nakit', @Siparis2Toplam, N'TRY', DATEADD(hour, -3, @Now), N'Seed nakit odeme',
                NULL, NULL, N'Tamamlandi', CONCAT(N'SEED-NAKIT-', @RandomSuffix), 0,
                @Now, @Now, @SeedTag, @SeedTag
            );

            UPDATE s
            SET
                [ToplamTutar] = @Siparis2Toplam,
                [OdenenTutar] = @Siparis2Toplam,
                [KalanTutar] = 0,
                [OdemeDurumu] = N'Odendi'
            FROM [restoran].[RestoranSiparisleri] s
            WHERE s.[Id] = @Siparis2Id;

            UPDATE [restoran].[RestoranMasalari]
            SET [Durum] = N'Dolu', [UpdatedAt] = @Now, [UpdatedBy] = @SeedTag
            WHERE [Id] = @Masa1Id;

            UPDATE [restoran].[RestoranMasalari]
            SET [Durum] = N'Musait', [UpdatedAt] = @Now, [UpdatedBy] = @SeedTag
            WHERE [Id] = @Masa2Id;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @SeedTag nvarchar(128) = N'migration_seed_restoran_module_v1';

            DELETE FROM [restoran].[RestoranOdemeleri]
            WHERE [CreatedBy] = @SeedTag;

            DELETE FROM [restoran].[RestoranSiparisKalemleri]
            WHERE [CreatedBy] = @SeedTag;

            DELETE FROM [restoran].[RestoranSiparisleri]
            WHERE [CreatedBy] = @SeedTag;

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
