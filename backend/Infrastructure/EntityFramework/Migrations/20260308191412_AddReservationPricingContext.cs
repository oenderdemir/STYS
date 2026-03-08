using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationPricingContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KonaklamaTipiId",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MisafirTipiId",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KonaklamaTipiId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "MisafirTipiId",
                schema: "dbo",
                table: "Rezervasyonlar");
        }
    }
}
