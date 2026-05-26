using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddCariHareketKapamaAlanlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IliskiliCariHareketId",
                schema: "muhasebe",
                table: "CariHareketler",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KalanTutar",
                schema: "muhasebe",
                table: "CariHareketler",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "KapananTutar",
                schema: "muhasebe",
                table: "CariHareketler",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "KapandiMi",
                schema: "muhasebe",
                table: "CariHareketler",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE muhasebe.CariHareketler
                SET
                    KapananTutar = 0,
                    KalanTutar = CASE
                        WHEN ISNULL(BorcTutari, 0) > 0 THEN ISNULL(BorcTutari, 0)
                        ELSE ISNULL(AlacakTutari, 0)
                    END,
                    KapandiMi = 0
                WHERE IsDeleted = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CariHareketler_CariKartId_KapandiMi",
                schema: "muhasebe",
                table: "CariHareketler",
                columns: new[] { "CariKartId", "KapandiMi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CariHareketler_IliskiliCariHareketId",
                schema: "muhasebe",
                table: "CariHareketler",
                column: "IliskiliCariHareketId",
                filter: "[IsDeleted] = 0 AND [IliskiliCariHareketId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CariHareketler_CariHareketler_IliskiliCariHareketId",
                schema: "muhasebe",
                table: "CariHareketler",
                column: "IliskiliCariHareketId",
                principalSchema: "muhasebe",
                principalTable: "CariHareketler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CariHareketler_CariHareketler_IliskiliCariHareketId",
                schema: "muhasebe",
                table: "CariHareketler");

            migrationBuilder.DropIndex(
                name: "IX_CariHareketler_CariKartId_KapandiMi",
                schema: "muhasebe",
                table: "CariHareketler");

            migrationBuilder.DropIndex(
                name: "IX_CariHareketler_IliskiliCariHareketId",
                schema: "muhasebe",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "IliskiliCariHareketId",
                schema: "muhasebe",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "KalanTutar",
                schema: "muhasebe",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "KapananTutar",
                schema: "muhasebe",
                table: "CariHareketler");

            migrationBuilder.DropColumn(
                name: "KapandiMi",
                schema: "muhasebe",
                table: "CariHareketler");
        }
    }
}
