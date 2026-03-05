using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260305230000_AddReservationPriceSnapshot")]
    public partial class AddReservationPriceSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParaBirimi",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TRY");

            migrationBuilder.AddColumn<decimal>(
                name: "ToplamUcret",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                DECLARE @SeedTag nvarchar(128) = N'migration_seed_rezervasyon_test';

                UPDATE rsa
                SET rsa.AyrilanKisiSayisi =
                    CASE
                        WHEN ISNULL(rsa.KapasiteSnapshot, 0) < 1 THEN 1
                        WHEN rsa.AyrilanKisiSayisi > rsa.KapasiteSnapshot THEN rsa.KapasiteSnapshot
                        ELSE rsa.AyrilanKisiSayisi
                    END
                FROM dbo.RezervasyonSegmentOdaAtamalari rsa
                INNER JOIN dbo.RezervasyonSegmentleri rs ON rs.Id = rsa.RezervasyonSegmentId
                INNER JOIN dbo.Rezervasyonlar r ON r.Id = rs.RezervasyonId
                WHERE r.CreatedBy = @SeedTag
                  AND r.IsDeleted = 0
                  AND rsa.IsDeleted = 0;

                ;WITH SegmentKisi AS (
                    SELECT
                        rs.RezervasyonId,
                        SUM(rsa.AyrilanKisiSayisi) AS KisiSayisi
                    FROM dbo.RezervasyonSegmentleri rs
                    INNER JOIN dbo.RezervasyonSegmentOdaAtamalari rsa ON rsa.RezervasyonSegmentId = rs.Id
                    INNER JOIN dbo.Rezervasyonlar r ON r.Id = rs.RezervasyonId
                    WHERE r.CreatedBy = @SeedTag
                      AND r.IsDeleted = 0
                      AND rs.IsDeleted = 0
                      AND rsa.IsDeleted = 0
                    GROUP BY rs.RezervasyonId, rs.Id
                ),
                RezervasyonKisi AS (
                    SELECT
                        RezervasyonId,
                        MAX(KisiSayisi) AS KisiSayisi
                    FROM SegmentKisi
                    GROUP BY RezervasyonId
                )
                UPDATE r
                SET r.KisiSayisi = rk.KisiSayisi
                FROM dbo.Rezervasyonlar r
                INNER JOIN RezervasyonKisi rk ON rk.RezervasyonId = r.Id
                WHERE r.CreatedBy = @SeedTag
                  AND r.IsDeleted = 0;

                UPDATE r
                SET
                    r.ToplamUcret = CONVERT(decimal(18,2), r.KisiSayisi * (CASE WHEN DATEDIFF(day, r.GirisTarihi, r.CikisTarihi) > 0 THEN DATEDIFF(day, r.GirisTarihi, r.CikisTarihi) ELSE 1 END) * 1000.00),
                    r.ParaBirimi = N'TRY'
                FROM dbo.Rezervasyonlar r
                WHERE r.CreatedBy = @SeedTag
                  AND r.IsDeleted = 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParaBirimi",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "ToplamUcret",
                schema: "dbo",
                table: "Rezervasyonlar");
        }
    }
}

