using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260328203000_AddRezervasyonKonaklamaHaklari")]
    public partial class AddRezervasyonKonaklamaHaklari : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RezervasyonKonaklamaHaklari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonId = table.Column<int>(type: "int", nullable: false),
                    HizmetKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HizmetAdiSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Miktar = table.Column<int>(type: "int", nullable: false),
                    Periyot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PeriyotAdiSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HakTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AciklamaSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Durum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RezervasyonKonaklamaHaklari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklamaHaklari_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklamaHaklari_RezervasyonId_HizmetKodu_HakTarihi_Periyot",
                schema: "dbo",
                table: "RezervasyonKonaklamaHaklari",
                columns: new[] { "RezervasyonId", "HizmetKodu", "HakTarihi", "Periyot" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                ;WITH GunlukKaynak AS
                (
                    SELECT
                        r.Id AS RezervasyonId,
                        CAST(r.GirisTarihi AS date) AS GirisGun,
                        CAST(r.CikisTarihi AS date) AS CikisGun,
                        ik.HizmetKodu,
                        ik.Miktar,
                        ik.Periyot,
                        ik.Aciklama
                    FROM dbo.Rezervasyonlar r
                    INNER JOIN dbo.KonaklamaTipiIcerikKalemleri ik
                        ON ik.KonaklamaTipiId = r.KonaklamaTipiId
                       AND ik.IsDeleted = 0
                       AND ik.Periyot = N'Gunluk'
                    WHERE r.IsDeleted = 0
                      AND CAST(r.GirisTarihi AS date) < CAST(r.CikisTarihi AS date)
                ),
                Gunler AS
                (
                    SELECT RezervasyonId, GirisGun AS HakTarihi, CikisGun, HizmetKodu, Miktar, Periyot, Aciklama
                    FROM GunlukKaynak

                    UNION ALL

                    SELECT RezervasyonId, DATEADD(DAY, 1, HakTarihi), CikisGun, HizmetKodu, Miktar, Periyot, Aciklama
                    FROM Gunler
                    WHERE DATEADD(DAY, 1, HakTarihi) < CikisGun
                )
                INSERT INTO dbo.RezervasyonKonaklamaHaklari
                (
                    RezervasyonId, HizmetKodu, HizmetAdiSnapshot, Miktar, Periyot, PeriyotAdiSnapshot,
                    HakTarihi, AciklamaSnapshot, Durum, AktifMi, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted
                )
                SELECT
                    g.RezervasyonId,
                    g.HizmetKodu,
                    CASE g.HizmetKodu
                        WHEN N'Kahvalti' THEN N'Kahvalti'
                        WHEN N'OgleYemegi' THEN N'Ogle Yemegi'
                        WHEN N'AksamYemegi' THEN N'Aksam Yemegi'
                        WHEN N'Wifi' THEN N'Wi-Fi'
                        WHEN N'Otopark' THEN N'Otopark'
                        WHEN N'HavaalaniTransferi' THEN N'Havaalani Transferi'
                        WHEN N'GunlukTemizlik' THEN N'Gunluk Temizlik'
                        ELSE g.HizmetKodu
                    END,
                    g.Miktar,
                    g.Periyot,
                    N'Gunluk',
                    g.HakTarihi,
                    g.Aciklama,
                    N'Bekliyor',
                    1,
                    GETUTCDATE(),
                    GETUTCDATE(),
                    N'migration',
                    N'migration',
                    0
                FROM Gunler g
                OPTION (MAXRECURSION 366);

                INSERT INTO dbo.RezervasyonKonaklamaHaklari
                (
                    RezervasyonId, HizmetKodu, HizmetAdiSnapshot, Miktar, Periyot, PeriyotAdiSnapshot,
                    HakTarihi, AciklamaSnapshot, Durum, AktifMi, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted
                )
                SELECT
                    r.Id,
                    ik.HizmetKodu,
                    CASE ik.HizmetKodu
                        WHEN N'Kahvalti' THEN N'Kahvalti'
                        WHEN N'OgleYemegi' THEN N'Ogle Yemegi'
                        WHEN N'AksamYemegi' THEN N'Aksam Yemegi'
                        WHEN N'Wifi' THEN N'Wi-Fi'
                        WHEN N'Otopark' THEN N'Otopark'
                        WHEN N'HavaalaniTransferi' THEN N'Havaalani Transferi'
                        WHEN N'GunlukTemizlik' THEN N'Gunluk Temizlik'
                        ELSE ik.HizmetKodu
                    END,
                    ik.Miktar,
                    ik.Periyot,
                    N'Konaklama Boyunca',
                    CAST(r.GirisTarihi AS date),
                    ik.Aciklama,
                    N'Bekliyor',
                    1,
                    GETUTCDATE(),
                    GETUTCDATE(),
                    N'migration',
                    N'migration',
                    0
                FROM dbo.Rezervasyonlar r
                INNER JOIN dbo.KonaklamaTipiIcerikKalemleri ik
                    ON ik.KonaklamaTipiId = r.KonaklamaTipiId
                   AND ik.IsDeleted = 0
                   AND ik.Periyot = N'KonaklamaBoyunca'
                WHERE r.IsDeleted = 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RezervasyonKonaklamaHaklari",
                schema: "dbo");
        }
    }
}
