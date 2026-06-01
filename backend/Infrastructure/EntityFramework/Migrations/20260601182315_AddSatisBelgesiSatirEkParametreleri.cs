using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddSatisBelgesiSatirEkParametreleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "IndirimOrani",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "KonaklamaVergisiOrani",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "KonaklamaVergisiTutari",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OivOrani",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OivTutari",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OtvOrani",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OtvTutari",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndirimOrani",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "KonaklamaVergisiOrani",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "KonaklamaVergisiTutari",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "OivOrani",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "OivTutari",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "OtvOrani",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "OtvTutari",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");
        }
    }
}
