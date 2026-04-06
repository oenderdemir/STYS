using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260406094500_RemoveKampBasvuruUserShortcut")]
    public partial class RemoveKampBasvuruUserShortcut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE sahip
SET sahip.UserId = kaynak.UserId
FROM dbo.KampBasvuruSahipleri sahip
OUTER APPLY
(
    SELECT TOP 1 basvuru.BasvuruSahibiUserId AS UserId
    FROM dbo.KampBasvurulari basvuru
    WHERE basvuru.KampBasvuruSahibiId = sahip.Id
      AND basvuru.BasvuruSahibiUserId IS NOT NULL
    ORDER BY ISNULL(basvuru.CreatedAt, basvuru.UpdatedAt) DESC, basvuru.Id DESC
) kaynak
WHERE sahip.UserId IS NULL
  AND kaynak.UserId IS NOT NULL;
");

            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_BasvuruSahibiUserId_KampDonemiId",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropColumn(
                name: "BasvuruSahibiUserId",
                schema: "dbo",
                table: "KampBasvurulari");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BasvuruSahibiUserId",
                schema: "dbo",
                table: "KampBasvurulari",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE basvuru
SET basvuru.BasvuruSahibiUserId = sahip.UserId
FROM dbo.KampBasvurulari basvuru
INNER JOIN dbo.KampBasvuruSahipleri sahip ON sahip.Id = basvuru.KampBasvuruSahibiId
WHERE sahip.UserId IS NOT NULL;
");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_BasvuruSahibiUserId_KampDonemiId",
                schema: "dbo",
                table: "KampBasvurulari",
                columns: new[] { "BasvuruSahibiUserId", "KampDonemiId" },
                filter: "[IsDeleted] = 0 AND [BasvuruSahibiUserId] IS NOT NULL");
        }
    }
}
