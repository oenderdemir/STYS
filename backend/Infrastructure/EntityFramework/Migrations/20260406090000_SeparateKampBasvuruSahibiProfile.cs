using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260406090000_SeparateKampBasvuruSahibiProfile")]
    public partial class SeparateKampBasvuruSahibiProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BasvuruSahibiTipi",
                schema: "dbo",
                table: "KampBasvuruSahipleri",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<int>(
                name: "HizmetYili",
                schema: "dbo",
                table: "KampBasvuruSahipleri",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
UPDATE sahip
SET sahip.AdSoyad = COALESCE(kaynak.BasvuruSahibiAdiSoyadi, sahip.AdSoyad),
    sahip.BasvuruSahibiTipi = COALESCE(kaynak.BasvuruSahibiTipi, sahip.BasvuruSahibiTipi),
    sahip.HizmetYili = COALESCE(kaynak.HizmetYili, sahip.HizmetYili)
FROM dbo.KampBasvuruSahipleri sahip
OUTER APPLY
(
    SELECT TOP 1
        basvuru.BasvuruSahibiAdiSoyadi,
        basvuru.BasvuruSahibiTipi,
        basvuru.HizmetYili
    FROM dbo.KampBasvurulari basvuru
    WHERE basvuru.KampBasvuruSahibiId = sahip.Id
    ORDER BY ISNULL(basvuru.CreatedAt, basvuru.UpdatedAt) DESC, basvuru.Id DESC
) kaynak;
");

            migrationBuilder.RenameColumn(
                name: "BasvuruSahibiAdiSoyadi",
                schema: "dbo",
                table: "KampBasvurulari",
                newName: "BasvuruSahibiAdiSoyadiSnapshot");

            migrationBuilder.RenameColumn(
                name: "BasvuruSahibiTipi",
                schema: "dbo",
                table: "KampBasvurulari",
                newName: "BasvuruSahibiTipiSnapshot");

            migrationBuilder.RenameColumn(
                name: "HizmetYili",
                schema: "dbo",
                table: "KampBasvurulari",
                newName: "HizmetYiliSnapshot");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BasvuruSahibiAdiSoyadiSnapshot",
                schema: "dbo",
                table: "KampBasvurulari",
                newName: "BasvuruSahibiAdiSoyadi");

            migrationBuilder.RenameColumn(
                name: "BasvuruSahibiTipiSnapshot",
                schema: "dbo",
                table: "KampBasvurulari",
                newName: "BasvuruSahibiTipi");

            migrationBuilder.RenameColumn(
                name: "HizmetYiliSnapshot",
                schema: "dbo",
                table: "KampBasvurulari",
                newName: "HizmetYili");

            migrationBuilder.DropColumn(
                name: "BasvuruSahibiTipi",
                schema: "dbo",
                table: "KampBasvuruSahipleri");

            migrationBuilder.DropColumn(
                name: "HizmetYili",
                schema: "dbo",
                table: "KampBasvuruSahipleri");
        }
    }
}
