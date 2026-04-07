using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260405180000_AddKampParametreAndUpdateYears")]
public partial class AddKampParametreAndUpdateYears : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. KampParametreleri tablosu
        migrationBuilder.CreateTable(
            name: "KampParametreleri",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Kod = table.Column<string>(maxLength: 128, nullable: false),
                Deger = table.Column<string>(maxLength: 512, nullable: false),
                Aciklama = table.Column<string>(maxLength: 256, nullable: true),
                IsDeleted = table.Column<bool>(nullable: false, defaultValue: false),
                DeletedAt = table.Column<DateTime>(nullable: true),
                DeletedBy = table.Column<string>(maxLength: 256, nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: true),
                CreatedBy = table.Column<string>(maxLength: 256, nullable: true),
                UpdatedAt = table.Column<DateTime>(nullable: true),
                UpdatedBy = table.Column<string>(maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KampParametreleri", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_KampParametreleri_Kod",
            schema: "dbo",
            table: "KampParametreleri",
            column: "Kod",
            unique: true,
            filter: "[IsDeleted] = 0");

        // 2. Seed parametreler
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            INSERT INTO [dbo].[KampParametreleri] ([Kod], [Deger], [Aciklama], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]) VALUES
            (N'KamuAvansKisiBasi', N'1700', N'Kamu katilimci basi avans tutari (TL)', 0, @Now, @Now, N'system', N'system'),
            (N'DigerAvansKisiBasi', N'2550', N'Diger katilimci basi avans tutari (TL)', 0, @Now, @Now, N'system', N'system'),
            (N'YemekOrani', N'0.50', N'Yemek ucreti orani (tam ucrete gore)', 0, @Now, @Now, N'system', N'system'),
            (N'UcretsizCocukSiniri', N'2023-01-01', N'Bu tarihten sonra dogan cocuklar ucretsiz (2026 kampi icin)', 0, @Now, @Now, N'system', N'system'),
            (N'YarimUcretliCocukSiniri', N'2020-01-01', N'Bu tarih arasi dogan cocuklar yarim ucretli (2026 kampi icin)', 0, @Now, @Now, N'system', N'system'),
            (N'EmekliBonusPuan', N'30', N'Emekli basvuru sahibi bonus puani', 0, @Now, @Now, N'system', N'system'),
            (N'KatilimciBasinaPuan', N'10', N'Her katilimci icin eklenen puan', 0, @Now, @Now, N'system', N'system'),
            (N'OncekiYilKatilimPenalti', N'20', N'Onceki yil kampindan faydalanma penaltisi', 0, @Now, @Now, N'system', N'system'),
            (N'TabanPuan.KurumPersoneli', N'40', N'Kurum personeli taban puani', 0, @Now, @Now, N'system', N'system'),
            (N'TabanPuan.KurumEmeklisi', N'20', N'Kurum emeklisi taban puani', 0, @Now, @Now, N'system', N'system'),
            (N'TabanPuan.BagliKurulus', N'15', N'Bagli kurulus personel/emekli taban puani', 0, @Now, @Now, N'system', N'system'),
            (N'TabanPuan.DigerKamu', N'10', N'Diger kamu personel/emekli taban puani', 0, @Now, @Now, N'system', N'system'),
            (N'TabanPuan.Diger', N'5', N'Diger basvuru sahipleri taban puani', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Standart34.Ad', N'Standart 3-4 Kisilik', N'Konaklama birim tipi etiketi', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Standart34.KamuGunluk', N'1700', N'Standart 3-4 kisilik kamu gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Standart34.DigerGunluk', N'2550', N'Standart 3-4 kisilik diger gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Standart34.BuzdolabiGunluk', N'45', N'Standart 3-4 kisilik buzdolabi gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Standart34.TelevizyonGunluk', N'45', N'Standart 3-4 kisilik televizyon gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Standart34.KlimaGunluk', N'60', N'Standart 3-4 kisilik klima gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Standart34.MinKisi', N'3', N'Standart 3-4 kisilik minimum kisi sayisi', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Standart34.MaksKisi', N'4', N'Standart 3-4 kisilik maksimum kisi sayisi', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Prefabrik45.Ad', N'Prefabrik 4-5 Kisilik', N'Konaklama birim tipi etiketi', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Prefabrik45.KamuGunluk', N'1550', N'Prefabrik 4-5 kisilik kamu gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Prefabrik45.DigerGunluk', N'2325', N'Prefabrik 4-5 kisilik diger gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Prefabrik45.BuzdolabiGunluk', N'40', N'Prefabrik 4-5 kisilik buzdolabi gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Prefabrik45.TelevizyonGunluk', N'40', N'Prefabrik 4-5 kisilik televizyon gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Prefabrik45.KlimaGunluk', N'50', N'Prefabrik 4-5 kisilik klima gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Prefabrik45.MinKisi', N'4', N'Prefabrik 4-5 kisilik minimum kisi sayisi', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Prefabrik45.MaksKisi', N'5', N'Prefabrik 4-5 kisilik maksimum kisi sayisi', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Otel45.Ad', N'Otel 4-5 Kisilik', N'Konaklama birim tipi etiketi', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Otel45.KamuGunluk', N'1550', N'Otel 4-5 kisilik kamu gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Otel45.DigerGunluk', N'2325', N'Otel 4-5 kisilik diger gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Otel45.BuzdolabiGunluk', N'40', N'Otel 4-5 kisilik buzdolabi gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Otel45.TelevizyonGunluk', N'40', N'Otel 4-5 kisilik televizyon gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Otel45.KlimaGunluk', N'50', N'Otel 4-5 kisilik klima gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Otel45.MinKisi', N'4', N'Otel 4-5 kisilik minimum kisi sayisi', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Otel45.MaksKisi', N'5', N'Otel 4-5 kisilik maksimum kisi sayisi', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Betonarme45.Ad', N'Betonarme 4-5 Kisilik', N'Konaklama birim tipi etiketi', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Betonarme45.KamuGunluk', N'1700', N'Betonarme 4-5 kisilik kamu gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Betonarme45.DigerGunluk', N'2550', N'Betonarme 4-5 kisilik diger gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Betonarme45.BuzdolabiGunluk', N'40', N'Betonarme 4-5 kisilik buzdolabi gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Betonarme45.TelevizyonGunluk', N'40', N'Betonarme 4-5 kisilik televizyon gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Betonarme45.KlimaGunluk', N'50', N'Betonarme 4-5 kisilik klima gunluk ucret', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Betonarme45.MinKisi', N'4', N'Betonarme 4-5 kisilik minimum kisi sayisi', 0, @Now, @Now, N'system', N'system'),
            (N'Konaklama.Betonarme45.MaksKisi', N'5', N'Betonarme 4-5 kisilik maksimum kisi sayisi', 0, @Now, @Now, N'system', N'system');
            """);

        // 3. KampBasvurulari: Kamp2023tenFaydalandiMi → drop, Kamp2025tenFaydalandiMi → add
        migrationBuilder.AddColumn<bool>(
            name: "Kamp2025tenFaydalandiMi",
            schema: "dbo",
            table: "KampBasvurulari",
            nullable: false,
            defaultValue: false);

        migrationBuilder.DropColumn(
            name: "Kamp2023tenFaydalandiMi",
            schema: "dbo",
            table: "KampBasvurulari");

        // 4. 2026 Yaz Kampi seed
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @ProgramId int;

            SELECT @ProgramId = [Id] FROM [dbo].[KampProgramlari] WHERE [Kod] = N'YAZ_KAMPI';

            IF @ProgramId IS NOT NULL
            BEGIN
                UPDATE [dbo].[KampProgramlari]
                SET [Ad] = N'Yaz Kampi', [Aciklama] = N'Yaz kampi programi.', [UpdatedAt] = @Now
                WHERE [Id] = @ProgramId;
            END
            ELSE
            BEGIN
                INSERT INTO [dbo].[KampProgramlari] ([Kod], [Ad], [Aciklama], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (N'YAZ_KAMPI', N'Yaz Kampi', N'Yaz kampi programi.', 1, 0, @Now, @Now, N'system', N'system');

                SET @ProgramId = SCOPE_IDENTITY();
            END

            DECLARE @Donemler TABLE ([Kod] nvarchar(64), [Ad] nvarchar(160), [Baslangic] date, [Bitis] date);
            INSERT INTO @Donemler ([Kod], [Ad], [Baslangic], [Bitis]) VALUES
            (N'2026-YAZ-01', N'2026 Yaz Kampi 1. Donem', '2026-06-01', '2026-06-05'),
            (N'2026-YAZ-02', N'2026 Yaz Kampi 2. Donem', '2026-06-08', '2026-06-12'),
            (N'2026-YAZ-03', N'2026 Yaz Kampi 3. Donem', '2026-06-15', '2026-06-19'),
            (N'2026-YAZ-04', N'2026 Yaz Kampi 4. Donem', '2026-06-22', '2026-06-26'),
            (N'2026-YAZ-05', N'2026 Yaz Kampi 5. Donem', '2026-06-29', '2026-07-03'),
            (N'2026-YAZ-06', N'2026 Yaz Kampi 6. Donem', '2026-07-06', '2026-07-10'),
            (N'2026-YAZ-07', N'2026 Yaz Kampi 7. Donem', '2026-07-13', '2026-07-17'),
            (N'2026-YAZ-08', N'2026 Yaz Kampi 8. Donem', '2026-07-20', '2026-07-24'),
            (N'2026-YAZ-09', N'2026 Yaz Kampi 9. Donem', '2026-07-27', '2026-07-31'),
            (N'2026-YAZ-10', N'2026 Yaz Kampi 10. Donem', '2026-08-03', '2026-08-07'),
            (N'2026-YAZ-11', N'2026 Yaz Kampi 11. Donem', '2026-08-10', '2026-08-14'),
            (N'2026-YAZ-12', N'2026 Yaz Kampi 12. Donem', '2026-08-17', '2026-08-21'),
            (N'2026-YAZ-13', N'2026 Yaz Kampi 13. Donem', '2026-08-24', '2026-08-28'),
            (N'2026-YAZ-14', N'2026 Yaz Kampi 14. Donem', '2026-08-31', '2026-09-04'),
            (N'2026-YAZ-15', N'2026 Yaz Kampi 15. Donem', '2026-09-07', '2026-09-11'),
            (N'2026-YAZ-16', N'2026 Yaz Kampi 16. Donem', '2026-09-14', '2026-09-18'),
            (N'2026-YAZ-17', N'2026 Yaz Kampi 17. Donem', '2026-09-21', '2026-09-25');

            INSERT INTO [dbo].[KampDonemleri]
            ([KampProgramiId], [Kod], [Ad], [Yil], [BasvuruBaslangicTarihi], [BasvuruBitisTarihi], [KonaklamaBaslangicTarihi], [KonaklamaBitisTarihi], [MinimumGece], [MaksimumGece], [OnayGerektirirMi], [CekilisGerekliMi], [AyniAileIcinTekBasvuruMu], [IptalSonGun], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT
                @ProgramId,
                d.[Kod],
                d.[Ad],
                2026,
                '2026-03-01',
                '2026-05-02',
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

            ;WITH AktifTesisler AS
            (
                SELECT
                    t.[Id],
                    CASE
                        WHEN oda.[OdaSayisi] > 0 THEN oda.[OdaSayisi]
                        ELSE 50
                    END AS [ToplamKontenjan]
                FROM [dbo].[Tesisler] t
                OUTER APPLY
                (
                    SELECT COUNT(1) AS [OdaSayisi]
                    FROM [dbo].[Binalar] b
                    INNER JOIN [dbo].[Odalar] o ON o.[BinaId] = b.[Id] AND o.[IsDeleted] = 0 AND o.[AktifMi] = 1
                    WHERE b.[TesisId] = t.[Id] AND b.[IsDeleted] = 0 AND b.[AktifMi] = 1
                ) oda
                WHERE t.[IsDeleted] = 0
                  AND t.[AktifMi] = 1
            )
            INSERT INTO [dbo].[KampDonemiTesisleri] ([KampDonemiId], [TesisId], [AktifMi], [BasvuruyaAcikMi], [ToplamKontenjan], [Aciklama], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT d.[Id], t.[Id], 1, 1, t.[ToplamKontenjan], N'Genel kamp atamasi (otomatik).', 0, @Now, @Now, N'system', N'system'
            FROM [dbo].[KampDonemleri] d
            CROSS JOIN AktifTesisler t
            WHERE d.[KampProgramiId] = @ProgramId AND d.[Yil] = 2026
              AND NOT EXISTS (SELECT 1 FROM [dbo].[KampDonemiTesisleri] x WHERE x.[KampDonemiId] = d.[Id] AND x.[TesisId] = t.[Id]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @ProgramId int = (SELECT TOP 1 [Id] FROM [dbo].[KampProgramlari] WHERE [Kod] = N'YAZ_KAMPI');
            IF @ProgramId IS NOT NULL
            BEGIN
                DELETE FROM [dbo].[KampDonemiTesisleri] WHERE [KampDonemiId] IN (SELECT [Id] FROM [dbo].[KampDonemleri] WHERE [KampProgramiId] = @ProgramId AND [Yil] = 2026);
                DELETE FROM [dbo].[KampDonemleri] WHERE [KampProgramiId] = @ProgramId AND [Kod] LIKE N'2026-YAZ-%';
            END
            """);

        migrationBuilder.AddColumn<bool>(
            name: "Kamp2023tenFaydalandiMi",
            schema: "dbo",
            table: "KampBasvurulari",
            nullable: false,
            defaultValue: false);

        migrationBuilder.DropColumn(
            name: "Kamp2025tenFaydalandiMi",
            schema: "dbo",
            table: "KampBasvurulari");

        migrationBuilder.DropTable(name: "KampParametreleri", schema: "dbo");
    }
}
