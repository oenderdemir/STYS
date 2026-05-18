using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeHesapBakiyeSorguAlanlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BakiyeTipi",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "HesapSeviyesi",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "NetBakiye",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "UstHesapKodu",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapBakiyeleri_BakiyeTipi",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                column: "BakiyeTipi");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapBakiyeleri_TesisId_MaliYil_Donem_HesapSeviyesi",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                columns: new[] { "TesisId", "MaliYil", "Donem", "HesapSeviyesi" });

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapBakiyeleri_UstHesapKodu",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                column: "UstHesapKodu");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapBakiyeleri_BakiyeTipi",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapBakiyeleri_TesisId_MaliYil_Donem_HesapSeviyesi",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapBakiyeleri_UstHesapKodu",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri");

            migrationBuilder.DropColumn(
                name: "BakiyeTipi",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri");

            migrationBuilder.DropColumn(
                name: "HesapSeviyesi",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri");

            migrationBuilder.DropColumn(
                name: "NetBakiye",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri");

            migrationBuilder.DropColumn(
                name: "UstHesapKodu",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri");
        }
    }
}
