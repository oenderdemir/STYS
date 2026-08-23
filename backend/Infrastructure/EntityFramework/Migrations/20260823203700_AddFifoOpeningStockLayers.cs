using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddFifoOpeningStockLayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "KaynakStokHareketId",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "KatmanKaynakTipi",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "StokHareketi");

            migrationBuilder.Sql("""
                UPDATE [muhasebe].[StokMaliyetKatmanlari]
                SET [KatmanKaynakTipi] = N'StokHareketi'
                WHERE [KatmanKaynakTipi] IS NULL
                   OR [KatmanKaynakTipi] = N'';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KatmanKaynakTipi",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari");

            migrationBuilder.AlterColumn<int>(
                name: "KaynakStokHareketId",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
