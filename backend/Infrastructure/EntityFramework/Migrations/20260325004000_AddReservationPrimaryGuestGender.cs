using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
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
