using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260325004000_AddReservationPrimaryGuestGender")]
    public partial class AddReservationPrimaryGuestGender : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MisafirCinsiyeti",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MisafirCinsiyeti",
                schema: "dbo",
                table: "Rezervasyonlar");
        }
    }
}
