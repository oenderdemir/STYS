using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260405193000_DynamicKampBasvuruKurallari")]
    public partial class DynamicKampBasvuruKurallari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KampBasvuruSahibiId",
                schema: "dbo",
                table: "KampBasvurulari",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KampAkrabalikTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    YakindanDogrulanabilirMi = table.Column<bool>(type: "bit", nullable: false),
                    BasvuruSahibiAkrabaligiMi = table.Column<bool>(type: "bit", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampAkrabalikTipleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampBasvuruSahibiTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OncelikSirasi = table.Column<int>(type: "int", nullable: false),
                    TabanPuan = table.Column<int>(type: "int", nullable: false),
                    HizmetYiliPuaniAktifMi = table.Column<bool>(type: "bit", nullable: false),
                    EmekliBonusPuani = table.Column<int>(type: "int", nullable: false),
                    VarsayilanKatilimciTipiKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampBasvuruSahibiTipleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampBasvuruSahipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TcKimlikNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    AdSoyad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampBasvuruSahipleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampKatilimciTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KamuTarifesiUygulanirMi = table.Column<bool>(type: "bit", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampKatilimciTipleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampKuralSetleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampYili = table.Column<int>(type: "int", nullable: false),
                    OncekiYilSayisi = table.Column<int>(type: "int", nullable: false),
                    KatilimCezaPuani = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampKuralSetleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampBasvuruGecmisKatilimlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampBasvuruSahibiId = table.Column<int>(type: "int", nullable: false),
                    KatilimYili = table.Column<int>(type: "int", nullable: false),
                    KaynakBasvuruId = table.Column<int>(type: "int", nullable: true),
                    BeyanMi = table.Column<bool>(type: "bit", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampBasvuruGecmisKatilimlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampBasvuruGecmisKatilimlari_KampBasvuruSahipleri_KampBasvuruSahibiId",
                        column: x => x.KampBasvuruSahibiId,
                        principalSchema: "dbo",
                        principalTable: "KampBasvuruSahipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KampBasvuruGecmisKatilimlari_KampBasvurulari_KaynakBasvuruId",
                        column: x => x.KaynakBasvuruId,
                        principalSchema: "dbo",
                        principalTable: "KampBasvurulari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KampAkrabalikTipleri_Kod",
                schema: "dbo",
                table: "KampAkrabalikTipleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruGecmisKatilimlari_KampBasvuruSahibiId_KatilimYili",
                schema: "dbo",
                table: "KampBasvuruGecmisKatilimlari",
                columns: new[] { "KampBasvuruSahibiId", "KatilimYili" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruGecmisKatilimlari_KaynakBasvuruId",
                schema: "dbo",
                table: "KampBasvuruGecmisKatilimlari",
                column: "KaynakBasvuruId");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruSahibiTipleri_Kod",
                schema: "dbo",
                table: "KampBasvuruSahibiTipleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruSahipleri_TcKimlikNo",
                schema: "dbo",
                table: "KampBasvuruSahipleri",
                column: "TcKimlikNo",
                unique: true,
                filter: "[IsDeleted] = 0 AND [TcKimlikNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruSahipleri_UserId",
                schema: "dbo",
                table: "KampBasvuruSahipleri",
                column: "UserId",
                filter: "[IsDeleted] = 0 AND [UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KampKatilimciTipleri_Kod",
                schema: "dbo",
                table: "KampKatilimciTipleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_KampKuralSetleri_KampYili",
                schema: "dbo",
                table: "KampKuralSetleri",
                column: "KampYili",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(@"
INSERT INTO dbo.KampKatilimciTipleri (Kod, Ad, KamuTarifesiUygulanirMi, AktifMi, IsDeleted, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES
    (N'Kamu', N'Kamu', 1, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'SehitGaziMalul', N'Sehit/Gazi/Malul', 1, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'Diger', N'Diger', 0, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration');

INSERT INTO dbo.KampBasvuruSahibiTipleri (Kod, Ad, OncelikSirasi, TabanPuan, HizmetYiliPuaniAktifMi, EmekliBonusPuani, VarsayilanKatilimciTipiKodu, AktifMi, IsDeleted, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES
    (N'KurumPersoneli', N'Kurum Personeli', 1, 40, 1, 0, N'Kamu', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'KurumEmeklisi', N'Kurum Emeklisi', 1, 20, 0, 30, N'Kamu', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'BagliKurulusPersoneli', N'Bagli / Ilgili Kurulus Personeli', 2, 15, 0, 0, N'Kamu', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'BagliKurulusEmeklisi', N'Bagli / Ilgili Kurulus Emeklisi', 2, 15, 0, 0, N'Kamu', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'DigerKamuPersoneli', N'Diger Kamu Personeli', 3, 10, 0, 0, N'Kamu', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'DigerKamuEmeklisi', N'Diger Kamu Emeklisi', 3, 10, 0, 0, N'Kamu', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'Diger', N'Diger', 4, 5, 0, 0, N'Diger', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration');

INSERT INTO dbo.KampAkrabalikTipleri (Kod, Ad, YakindanDogrulanabilirMi, BasvuruSahibiAkrabaligiMi, AktifMi, IsDeleted, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES
    (N'BasvuruSahibi', N'Basvuru Sahibi', 1, 1, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'Es', N'Es', 1, 0, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'Cocuk', N'Cocuk', 1, 0, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'Anne', N'Anne', 1, 0, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'Baba', N'Baba', 1, 0, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'Kardes', N'Kardes', 0, 0, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (N'Diger', N'Diger', 0, 0, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration');

INSERT INTO dbo.KampKuralSetleri (KampYili, OncekiYilSayisi, KatilimCezaPuani, AktifMi, IsDeleted, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
VALUES
    (2025, 2, 20, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'),
    (2026, 2, 20, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration');
");

            migrationBuilder.Sql(@"
CREATE TABLE #KampBasvuruSahibiMap
(
    BasvuruId INT NOT NULL,
    KampBasvuruSahibiId INT NOT NULL
);

INSERT INTO dbo.KampBasvuruSahipleri (TcKimlikNo, AdSoyad, UserId, AktifMi, IsDeleted, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
SELECT kaynak.TcKimlikNo, MAX(kaynak.AdSoyad), MAX(kaynak.UserId), 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'
FROM
(
    SELECT NULLIF(LTRIM(RTRIM(k.TcKimlikNo)), N'') AS TcKimlikNo, b.BasvuruSahibiAdiSoyadi AS AdSoyad, b.BasvuruSahibiUserId AS UserId
    FROM dbo.KampBasvurulari b
    LEFT JOIN dbo.KampBasvuruKatilimcilari k ON k.KampBasvuruId = b.Id AND k.BasvuruSahibiMi = 1 AND k.IsDeleted = 0
) kaynak
WHERE kaynak.TcKimlikNo IS NOT NULL
GROUP BY kaynak.TcKimlikNo;

INSERT INTO #KampBasvuruSahibiMap (BasvuruId, KampBasvuruSahibiId)
SELECT b.Id, s.Id
FROM dbo.KampBasvurulari b
LEFT JOIN dbo.KampBasvuruKatilimcilari k ON k.KampBasvuruId = b.Id AND k.BasvuruSahibiMi = 1 AND k.IsDeleted = 0
INNER JOIN dbo.KampBasvuruSahipleri s ON s.TcKimlikNo = NULLIF(LTRIM(RTRIM(k.TcKimlikNo)), N'');

MERGE dbo.KampBasvuruSahipleri AS target
USING
(
    SELECT b.Id AS BasvuruId, b.BasvuruSahibiAdiSoyadi AS AdSoyad, b.BasvuruSahibiUserId AS UserId
    FROM dbo.KampBasvurulari b
    LEFT JOIN dbo.KampBasvuruKatilimcilari k ON k.KampBasvuruId = b.Id AND k.BasvuruSahibiMi = 1 AND k.IsDeleted = 0
    WHERE NULLIF(LTRIM(RTRIM(k.TcKimlikNo)), N'') IS NULL
) AS source
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (TcKimlikNo, AdSoyad, UserId, AktifMi, IsDeleted, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
    VALUES (NULL, source.AdSoyad, source.UserId, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration')
OUTPUT source.BasvuruId, inserted.Id INTO #KampBasvuruSahibiMap (BasvuruId, KampBasvuruSahibiId);

UPDATE b
SET b.KampBasvuruSahibiId = m.KampBasvuruSahibiId
FROM dbo.KampBasvurulari b
INNER JOIN #KampBasvuruSahibiMap m ON m.BasvuruId = b.Id;

INSERT INTO dbo.KampBasvuruGecmisKatilimlari (KampBasvuruSahibiId, KatilimYili, KaynakBasvuruId, BeyanMi, AktifMi, IsDeleted, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
SELECT kaynak.KampBasvuruSahibiId, kaynak.KatilimYili, MIN(kaynak.KaynakBasvuruId), 1, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'migration', N'migration'
FROM
(
    SELECT KampBasvuruSahibiId, 2024 AS KatilimYili, Id AS KaynakBasvuruId
    FROM dbo.KampBasvurulari
    WHERE KampBasvuruSahibiId IS NOT NULL AND Kamp2024tenFaydalandiMi = 1

    UNION ALL

    SELECT KampBasvuruSahibiId, 2025 AS KatilimYili, Id AS KaynakBasvuruId
    FROM dbo.KampBasvurulari
    WHERE KampBasvuruSahibiId IS NOT NULL AND Kamp2025tenFaydalandiMi = 1
) kaynak
GROUP BY kaynak.KampBasvuruSahibiId, kaynak.KatilimYili;

DROP TABLE #KampBasvuruSahibiMap;
");

            migrationBuilder.AlterColumn<int>(
                name: "KampBasvuruSahibiId",
                schema: "dbo",
                table: "KampBasvurulari",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_KampBasvuruSahibiId",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "KampBasvuruSahibiId");

            migrationBuilder.AddForeignKey(
                name: "FK_KampBasvurulari_KampBasvuruSahipleri_KampBasvuruSahibiId",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "KampBasvuruSahibiId",
                principalSchema: "dbo",
                principalTable: "KampBasvuruSahipleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "Kamp2024tenFaydalandiMi",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropColumn(
                name: "Kamp2025tenFaydalandiMi",
                schema: "dbo",
                table: "KampBasvurulari");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Kamp2024tenFaydalandiMi",
                schema: "dbo",
                table: "KampBasvurulari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Kamp2025tenFaydalandiMi",
                schema: "dbo",
                table: "KampBasvurulari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
UPDATE b
SET Kamp2024tenFaydalandiMi = CASE WHEN EXISTS (
        SELECT 1 FROM dbo.KampBasvuruGecmisKatilimlari g
        WHERE g.KampBasvuruSahibiId = b.KampBasvuruSahibiId AND g.KatilimYili = 2024 AND g.IsDeleted = 0
    ) THEN 1 ELSE 0 END,
    Kamp2025tenFaydalandiMi = CASE WHEN EXISTS (
        SELECT 1 FROM dbo.KampBasvuruGecmisKatilimlari g
        WHERE g.KampBasvuruSahibiId = b.KampBasvuruSahibiId AND g.KatilimYili = 2025 AND g.IsDeleted = 0
    ) THEN 1 ELSE 0 END
FROM dbo.KampBasvurulari b;
");

            migrationBuilder.DropForeignKey(
                name: "FK_KampBasvurulari_KampBasvuruSahipleri_KampBasvuruSahibiId",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_KampBasvuruSahibiId",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropTable(
                name: "KampAkrabalikTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampBasvuruGecmisKatilimlari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampBasvuruSahibiTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampKatilimciTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampKuralSetleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampBasvuruSahipleri",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "KampBasvuruSahibiId",
                schema: "dbo",
                table: "KampBasvurulari");
        }
    }
}
