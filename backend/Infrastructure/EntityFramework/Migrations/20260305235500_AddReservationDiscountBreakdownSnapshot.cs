using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260305235500_AddReservationDiscountBreakdownSnapshot")]
    public partial class AddReservationDiscountBreakdownSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ToplamBazUcret",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "UygulananIndirimlerJson",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE r
                SET r.ToplamBazUcret = CASE WHEN r.ToplamUcret < 0 THEN 0 ELSE r.ToplamUcret END
                FROM dbo.Rezervasyonlar r
                WHERE r.IsDeleted = 0
                  AND r.ToplamBazUcret = 0;

                UPDATE r
                SET r.UygulananIndirimlerJson = N'[]'
                FROM dbo.Rezervasyonlar r
                WHERE r.IsDeleted = 0
                  AND r.UygulananIndirimlerJson IS NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ToplamBazUcret",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "UygulananIndirimlerJson",
                schema: "dbo",
                table: "Rezervasyonlar");
        }
    }
}
