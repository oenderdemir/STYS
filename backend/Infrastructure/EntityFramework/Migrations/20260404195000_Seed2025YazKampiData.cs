using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260404195000_Seed2025YazKampiData")]
public partial class Seed2025YazKampiData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @ProgramId int;

            SELECT @ProgramId = [Id] FROM [dbo].[KampProgramlari] WHERE [Kod] = N'YAZ_KAMPI';

            IF @ProgramId IS NULL
            BEGIN
                INSERT INTO [dbo].[KampProgramlari] ([Kod], [Ad], [Aciklama], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (N'YAZ_KAMPI', N'2025 Yaz Kampi', N'2025 kamp talimatina gore seed edilen yaz kampi programi.', 1, 0, @Now, @Now, N'system', N'system');

                SET @ProgramId = SCOPE_IDENTITY();
            END

            DECLARE @Donemler TABLE ([Kod] nvarchar(64), [Ad] nvarchar(160), [Baslangic] date, [Bitis] date);
            INSERT INTO @Donemler ([Kod], [Ad], [Baslangic], [Bitis]) VALUES
            (N'2025-YAZ-01', N'2025 Yaz Kampi 1. Donem', '2025-06-02', '2025-06-06'),
            (N'2025-YAZ-02', N'2025 Yaz Kampi 2. Donem', '2025-06-09', '2025-06-13'),
            (N'2025-YAZ-03', N'2025 Yaz Kampi 3. Donem', '2025-06-16', '2025-06-20'),
            (N'2025-YAZ-04', N'2025 Yaz Kampi 4. Donem', '2025-06-23', '2025-06-27'),
            (N'2025-YAZ-05', N'2025 Yaz Kampi 5. Donem', '2025-06-30', '2025-07-04'),
            (N'2025-YAZ-06', N'2025 Yaz Kampi 6. Donem', '2025-07-07', '2025-07-11'),
            (N'2025-YAZ-07', N'2025 Yaz Kampi 7. Donem', '2025-07-14', '2025-07-18'),
            (N'2025-YAZ-08', N'2025 Yaz Kampi 8. Donem', '2025-07-21', '2025-07-25'),
            (N'2025-YAZ-09', N'2025 Yaz Kampi 9. Donem', '2025-07-28', '2025-08-01'),
            (N'2025-YAZ-10', N'2025 Yaz Kampi 10. Donem', '2025-08-04', '2025-08-08'),
            (N'2025-YAZ-11', N'2025 Yaz Kampi 11. Donem', '2025-08-11', '2025-08-15'),
            (N'2025-YAZ-12', N'2025 Yaz Kampi 12. Donem', '2025-08-18', '2025-08-22'),
            (N'2025-YAZ-13', N'2025 Yaz Kampi 13. Donem', '2025-08-25', '2025-08-29'),
            (N'2025-YAZ-14', N'2025 Yaz Kampi 14. Donem', '2025-09-01', '2025-09-05'),
            (N'2025-YAZ-15', N'2025 Yaz Kampi 15. Donem', '2025-09-08', '2025-09-12'),
            (N'2025-YAZ-16', N'2025 Yaz Kampi 16. Donem', '2025-09-15', '2025-09-19'),
            (N'2025-YAZ-17', N'2025 Yaz Kampi 17. Donem', '2025-09-22', '2025-09-26');

            INSERT INTO [dbo].[KampDonemleri]
            ([KampProgramiId], [Kod], [Ad], [Yil], [BasvuruBaslangicTarihi], [BasvuruBitisTarihi], [KonaklamaBaslangicTarihi], [KonaklamaBitisTarihi], [MinimumGece], [MaksimumGece], [OnayGerektirirMi], [CekilisGerekliMi], [AyniAileIcinTekBasvuruMu], [IptalSonGun], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT
                @ProgramId,
                d.[Kod],
                d.[Ad],
                2025,
                DATEADD(day, -30, d.[Baslangic]),
                DATEADD(day, -7, d.[Baslangic]),
                d.[Baslangic],
                d.[Bitis],
                5,
                5,
                1,
                1,
                1,
                DATEADD(day, -7, d.[Baslangic]),
                1,
                0,
                @Now,
                @Now,
                N'system',
                N'system'
            FROM @Donemler d
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[KampDonemleri] x WHERE x.[Kod] = d.[Kod]);

            DECLARE @AlataTesisId int = (SELECT TOP 1 [Id] FROM [dbo].[Tesisler] WHERE [Ad] LIKE N'%Alata%' AND [IsDeleted] = 0 ORDER BY [Id]);
            DECLARE @FocaTesisId int = (SELECT TOP 1 [Id] FROM [dbo].[Tesisler] WHERE ([Ad] LIKE N'%Foca%' OR [Ad] LIKE N'%Foça%') AND [IsDeleted] = 0 ORDER BY [Id]);

            IF @AlataTesisId IS NOT NULL
            BEGIN
                INSERT INTO [dbo].[KampDonemiTesisleri] ([KampDonemiId], [TesisId], [AktifMi], [BasvuruyaAcikMi], [ToplamKontenjan], [Aciklama], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT d.[Id], @AlataTesisId, 1, 1, 52, N'Alata 52 oda, 3-4 kisilik.', 0, @Now, @Now, N'system', N'system'
                FROM [dbo].[KampDonemleri] d
                WHERE d.[KampProgramiId] = @ProgramId
                  AND NOT EXISTS (SELECT 1 FROM [dbo].[KampDonemiTesisleri] x WHERE x.[KampDonemiId] = d.[Id] AND x.[TesisId] = @AlataTesisId);
            END

            IF @FocaTesisId IS NOT NULL
            BEGIN
                INSERT INTO [dbo].[KampDonemiTesisleri] ([KampDonemiId], [TesisId], [AktifMi], [BasvuruyaAcikMi], [ToplamKontenjan], [Aciklama], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT d.[Id], @FocaTesisId, 1, 1, 61, N'Foça 61 oda, 4-5 kisilik.', 0, @Now, @Now, N'system', N'system'
                FROM [dbo].[KampDonemleri] d
                WHERE d.[KampProgramiId] = @ProgramId
                  AND NOT EXISTS (SELECT 1 FROM [dbo].[KampDonemiTesisleri] x WHERE x.[KampDonemiId] = d.[Id] AND x.[TesisId] = @FocaTesisId);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @ProgramId int = (SELECT TOP 1 [Id] FROM [dbo].[KampProgramlari] WHERE [Kod] = N'YAZ_KAMPI');
            IF @ProgramId IS NOT NULL
            BEGIN
                DELETE FROM [dbo].[KampDonemiTesisleri] WHERE [KampDonemiId] IN (SELECT [Id] FROM [dbo].[KampDonemleri] WHERE [KampProgramiId] = @ProgramId);
                DELETE FROM [dbo].[KampDonemleri] WHERE [KampProgramiId] = @ProgramId AND [Kod] LIKE N'2025-YAZ-%';
                DELETE FROM [dbo].[KampProgramlari] WHERE [Id] = @ProgramId AND NOT EXISTS (SELECT 1 FROM [dbo].[KampDonemleri] WHERE [KampProgramiId] = @ProgramId);
            END
            """);
    }
}
