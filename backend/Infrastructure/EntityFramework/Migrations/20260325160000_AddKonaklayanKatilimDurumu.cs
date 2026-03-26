using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260325160000_AddKonaklayanKatilimDurumu")]
    public partial class AddKonaklayanKatilimDurumu : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KatilimDurumu",
                schema: "dbo",
                table: "RezervasyonKonaklayanlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Bekleniyor");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KatilimDurumu",
                schema: "dbo",
                table: "RezervasyonKonaklayanlar");
        }
    }
}
