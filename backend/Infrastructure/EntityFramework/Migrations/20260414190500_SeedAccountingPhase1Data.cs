using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260414190500_SeedAccountingPhase1Data")]
public partial class SeedAccountingPhase1Data : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @SeedTag nvarchar(128) = N'migration_seed_accounting_phase1_v1';

            IF EXISTS (
                SELECT 1
                FROM [muhasebe].[CariKartlar]
                WHERE [CreatedBy] = @SeedTag
                  AND [IsDeleted] = 0
            )
            BEGIN
                RETURN;
            END;

            INSERT INTO [muhasebe].[CariKartlar]
            (
                [CariTipi], [CariKodu], [UnvanAdSoyad], [VergiNoTckn], [VergiDairesi], [Telefon], [Eposta], [Adres], [Il], [Ilce],
                [AktifMi], [EFaturaMukellefiMi], [EArsivKapsamindaMi], [Aciklama], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            VALUES
            (N'Musteri', N'CR-MUS-0001', N'Ahmet Yilmaz', N'11111111111', NULL, N'05001112233', N'ahmet.yilmaz@ornek.local', N'Merkez Mah. 10', N'Ankara', N'Cankaya', 1, 0, 0, N'Seed musteri 1', 0, @Now, @Now, @SeedTag, @SeedTag),
            (N'Musteri', N'CR-MUS-0002', N'Ayse Kara', N'22222222222', NULL, N'05004445566', N'ayse.kara@ornek.local', N'Yildiz Sok. 12', N'Izmir', N'Konak', 1, 0, 0, N'Seed musteri 2', 0, @Now, @Now, @SeedTag, @SeedTag),
            (N'KurumsalMusteri', N'CR-KRM-0001', N'Anadolu Turizm A.S.', N'1234567890', N'Ulus', N'03124445566', N'finans@anadoluturizm.local', N'Ataturk Cad. 45', N'Ankara', N'Yenimahalle', 1, 1, 1, N'Seed kurumsal cari', 0, @Now, @Now, @SeedTag, @SeedTag),
            (N'Tedarikci', N'CR-TED-0001', N'Mavi Gida Ltd.', N'9988776655', N'Kizilay', N'03123334455', N'muhasebe@mavigida.local', N'Sanayi Bolgesi 3', N'Ankara', N'Sincan', 1, 1, 0, N'Seed tedarikci', 0, @Now, @Now, @SeedTag, @SeedTag),
            (N'Personel', N'CR-PRS-0001', N'Kasiyer Demo', N'33333333333', NULL, N'05556667788', N'kasiyer.demo@ornek.local', N'Lale Sok. 5', N'Ankara', N'Etimesgut', 1, 0, 0, N'Seed personel cari', 0, @Now, @Now, @SeedTag, @SeedTag),
            (N'Diger', N'CR-DGR-0001', N'Diger Cari Ornek', NULL, NULL, NULL, NULL, NULL, N'Ankara', N'Cankaya', 1, 0, 0, N'Seed diger cari', 0, @Now, @Now, @SeedTag, @SeedTag);

            DECLARE @CariMusteri1Id int = (SELECT TOP (1) [Id] FROM [muhasebe].[CariKartlar] WHERE [CariKodu] = N'CR-MUS-0001' AND [IsDeleted] = 0 ORDER BY [Id]);
            DECLARE @CariMusteri2Id int = (SELECT TOP (1) [Id] FROM [muhasebe].[CariKartlar] WHERE [CariKodu] = N'CR-MUS-0002' AND [IsDeleted] = 0 ORDER BY [Id]);
            DECLARE @CariKurumsalId int = (SELECT TOP (1) [Id] FROM [muhasebe].[CariKartlar] WHERE [CariKodu] = N'CR-KRM-0001' AND [IsDeleted] = 0 ORDER BY [Id]);
            DECLARE @CariTedarikciId int = (SELECT TOP (1) [Id] FROM [muhasebe].[CariKartlar] WHERE [CariKodu] = N'CR-TED-0001' AND [IsDeleted] = 0 ORDER BY [Id]);

            INSERT INTO [muhasebe].[CariHareketler]
            (
                [CariKartId], [HareketTarihi], [BelgeTuru], [BelgeNo], [Aciklama], [BorcTutari], [AlacakTutari], [ParaBirimi], [VadeTarihi], [Durum], [KaynakModul], [KaynakId],
                [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            VALUES
            (@CariMusteri1Id, DATEADD(day, -7, @Now), N'Fatura', N'FTR-2026-0001', N'Konaklama faturasi', 1500.00, 0.00, N'TRY', DATEADD(day, 23, @Now), N'Aktif', N'Rezervasyon', 101, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@CariMusteri1Id, DATEADD(day, -5, @Now), N'Tahsilat', N'THS-2026-0001', N'Nakit tahsilat', 0.00, 500.00, N'TRY', NULL, N'Aktif', N'Muhasebe', NULL, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@CariMusteri2Id, DATEADD(day, -3, @Now), N'Fatura', N'FTR-2026-0002', N'Restoran harcamasi', 320.00, 0.00, N'TRY', DATEADD(day, 27, @Now), N'Aktif', N'Restoran', 5501, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@CariKurumsalId, DATEADD(day, -2, @Now), N'Fatura', N'FTR-2026-0003', N'Kurumsal konaklama', 4800.00, 0.00, N'TRY', DATEADD(day, 28, @Now), N'Aktif', N'Rezervasyon', 204, 0, @Now, @Now, @SeedTag, @SeedTag),
            (@CariTedarikciId, DATEADD(day, -1, @Now), N'Alis Faturasi', N'ALS-2026-0001', N'Gida alimi', 0.00, 1250.00, N'TRY', NULL, N'Aktif', N'SatinAlma', 87, 0, @Now, @Now, @SeedTag, @SeedTag);

            INSERT INTO [muhasebe].[KasaHareketleri]
            (
                [KasaKodu], [HareketTarihi], [HareketTipi], [Tutar], [ParaBirimi], [Aciklama], [BelgeNo], [CariKartId], [KaynakModul], [KaynakId], [Durum],
                [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            VALUES
            (N'MERKEZ', DATEADD(day, -6, @Now), N'Tahsilat', 500.00, N'TRY', N'Musteri tahsilati', N'KAS-THS-0001', @CariMusteri1Id, N'Muhasebe', NULL, N'Aktif', 0, @Now, @Now, @SeedTag, @SeedTag),
            (N'MERKEZ', DATEADD(day, -1, @Now), N'Odeme', 350.00, N'TRY', N'Kucuk gider odemesi', N'KAS-ODM-0001', @CariTedarikciId, N'Muhasebe', NULL, N'Aktif', 0, @Now, @Now, @SeedTag, @SeedTag),
            (N'RESTORAN', DATEADD(hour, -8, @Now), N'Tahsilat', 220.00, N'TRY', N'Restoran pesin odeme', N'KAS-THS-0002', @CariMusteri2Id, N'Restoran', 5501, N'Aktif', 0, @Now, @Now, @SeedTag, @SeedTag);

            INSERT INTO [muhasebe].[BankaHareketleri]
            (
                [BankaAdi], [HesapKoduIban], [HareketTarihi], [HareketTipi], [Tutar], [ParaBirimi], [Aciklama], [BelgeNo], [CariKartId], [KaynakModul], [KaynakId], [Durum],
                [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            VALUES
            (N'Ziraat Bankasi', N'TR000000000000000000000001', DATEADD(day, -4, @Now), N'Tahsilat', 4800.00, N'TRY', N'Kurumsal havale', N'BNK-THS-0001', @CariKurumsalId, N'Muhasebe', NULL, N'Aktif', 0, @Now, @Now, @SeedTag, @SeedTag),
            (N'Vakıfbank', N'TR000000000000000000000002', DATEADD(day, -2, @Now), N'Odeme', 1250.00, N'TRY', N'Tedarikci EFT', N'BNK-ODM-0001', @CariTedarikciId, N'Muhasebe', NULL, N'Aktif', 0, @Now, @Now, @SeedTag, @SeedTag);

            INSERT INTO [muhasebe].[TahsilatOdemeBelgeleri]
            (
                [BelgeNo], [BelgeTarihi], [BelgeTipi], [CariKartId], [Tutar], [ParaBirimi], [OdemeYontemi], [Aciklama], [KaynakModul], [KaynakId], [Durum],
                [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
            )
            VALUES
            (N'TOB-2026-0001', DATEADD(day, -6, @Now), N'Tahsilat', @CariMusteri1Id, 500.00, N'TRY', N'Nakit', N'Cari tahsilat belgesi', N'Muhasebe', NULL, N'Aktif', 0, @Now, @Now, @SeedTag, @SeedTag),
            (N'TOB-2026-0002', DATEADD(day, -4, @Now), N'Tahsilat', @CariKurumsalId, 4800.00, N'TRY', N'HavaleEft', N'Kurumsal tahsilat belgesi', N'Muhasebe', NULL, N'Aktif', 0, @Now, @Now, @SeedTag, @SeedTag),
            (N'TOB-2026-0003', DATEADD(day, -2, @Now), N'Odeme', @CariTedarikciId, 1250.00, N'TRY', N'HavaleEft', N'Tedarikci odeme belgesi', N'Muhasebe', NULL, N'Aktif', 0, @Now, @Now, @SeedTag, @SeedTag),
            (N'TOB-2026-0004', DATEADD(hour, -8, @Now), N'Tahsilat', @CariMusteri2Id, 220.00, N'TRY', N'KrediKarti', N'Restoran tahsilat belgesi', N'Restoran', 5501, N'Aktif', 0, @Now, @Now, @SeedTag, @SeedTag);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @SeedTag nvarchar(128) = N'migration_seed_accounting_phase1_v1';

            DELETE FROM [muhasebe].[TahsilatOdemeBelgeleri]
            WHERE [CreatedBy] = @SeedTag;

            DELETE FROM [muhasebe].[BankaHareketleri]
            WHERE [CreatedBy] = @SeedTag;

            DELETE FROM [muhasebe].[KasaHareketleri]
            WHERE [CreatedBy] = @SeedTag;

            DELETE FROM [muhasebe].[CariHareketler]
            WHERE [CreatedBy] = @SeedTag;

            DELETE FROM [muhasebe].[CariKartlar]
            WHERE [CreatedBy] = @SeedTag;
            """);
    }
}
