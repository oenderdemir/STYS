using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260328191500_AddKonaklamaTipiIcerikKalemleri")]
    public partial class AddKonaklamaTipiIcerikKalemleri : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KonaklamaTipiIcerikKalemleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KonaklamaTipiId = table.Column<int>(type: "int", nullable: false),
                    HizmetKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Miktar = table.Column<int>(type: "int", nullable: false),
                    Periyot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_KonaklamaTipiIcerikKalemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KonaklamaTipiIcerikKalemleri_KonaklamaTipleri_KonaklamaTipiId",
                        column: x => x.KonaklamaTipiId,
                        principalSchema: "dbo",
                        principalTable: "KonaklamaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KonaklamaTipiIcerikKalemleri_KonaklamaTipiId_HizmetKodu",
                schema: "dbo",
                table: "KonaklamaTipiIcerikKalemleri",
                columns: new[] { "KonaklamaTipiId", "HizmetKodu" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = GETUTCDATE();

                INSERT INTO dbo.KonaklamaTipiIcerikKalemleri
                (
                    KonaklamaTipiId,
                    HizmetKodu,
                    Miktar,
                    Periyot,
                    Aciklama,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy,
                    IsDeleted
                )
                SELECT k.Id, v.HizmetKodu, v.Miktar, v.Periyot, v.Aciklama, @Now, @Now, N'migration', N'migration', 0
                FROM dbo.KonaklamaTipleri k
                CROSS APPLY
                (
                    SELECT
                        CASE
                            WHEN UPPER(REPLACE(REPLACE(k.Kod, ' ', ''), '_', '')) IN ('ODAKAHVALTI', 'BEDANDBREAKFAST')
                              OR UPPER(REPLACE(REPLACE(k.Ad, ' ', ''), '_', '')) = 'ODAKAHVALTI'
                                THEN 1
                            WHEN UPPER(REPLACE(REPLACE(k.Kod, ' ', ''), '_', '')) = 'YARIMPANSIYON'
                              OR UPPER(REPLACE(REPLACE(k.Ad, ' ', ''), '_', '')) = 'YARIMPANSIYON'
                                THEN 2
                            WHEN UPPER(REPLACE(REPLACE(k.Kod, ' ', ''), '_', '')) = 'TAMPANSIYON'
                              OR UPPER(REPLACE(REPLACE(k.Ad, ' ', ''), '_', '')) = 'TAMPANSIYON'
                                THEN 3
                            WHEN UPPER(REPLACE(REPLACE(k.Kod, ' ', ''), '_', '')) IN ('HERSEYDAHIL', 'ALLINCLUSIVE')
                              OR UPPER(REPLACE(REPLACE(k.Ad, ' ', ''), '_', '')) = 'HERSEYDAHIL'
                                THEN 4
                            ELSE 0
                        END AS PaketTipi
                ) p
                CROSS APPLY
                (
                    SELECT N'Kahvalti' AS HizmetKodu, 1 AS Miktar, N'Gunluk' AS Periyot, N'Pakete dahil kahvalti hizmeti.' AS Aciklama
                    WHERE p.PaketTipi IN (1, 2, 3, 4)

                    UNION ALL

                    SELECT N'AksamYemegi', 1, N'Gunluk', N'Pakete dahil aksam yemegi.'
                    WHERE p.PaketTipi IN (2, 3, 4)

                    UNION ALL

                    SELECT N'OgleYemegi', 1, N'Gunluk', N'Pakete dahil ogle yemegi.'
                    WHERE p.PaketTipi IN (3, 4)

                    UNION ALL

                    SELECT N'Wifi', 1, N'KonaklamaBoyunca', N'Konaklama boyunca internet erisimi.'
                    WHERE p.PaketTipi = 4
                ) v
                WHERE k.IsDeleted = 0
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.KonaklamaTipiIcerikKalemleri mevcut
                      WHERE mevcut.KonaklamaTipiId = k.Id
                        AND mevcut.HizmetKodu = v.HizmetKodu
                        AND mevcut.IsDeleted = 0
                  );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KonaklamaTipiIcerikKalemleri",
                schema: "dbo");
        }
    }
}
