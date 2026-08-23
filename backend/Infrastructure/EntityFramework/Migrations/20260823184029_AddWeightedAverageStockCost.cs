using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddWeightedAverageStockCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaliyetBirimFiyat",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaliyetTutari",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaliyetBirimFiyat",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "MaliyetTutari",
                schema: "muhasebe",
                table: "StokHareketleri");
        }
    }
}
