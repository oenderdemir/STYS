using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddOdaFiyatKullanimSekliAndTekKisilikFiyat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OdaFiyatlari_TesisOdaTipiId_KonaklamaTipiId_MisafirTipiId_KisiSayisi_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "OdaFiyatlari");

            migrationBuilder.AddColumn<bool>(
                name: "TekKisilikFiyatUygulandiMi",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "KullanimSekli",
                schema: "dbo",
                table: "OdaFiyatlari",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "KisiBasi");

            migrationBuilder.CreateIndex(
                name: "IX_OdaFiyatlari_TesisOdaTipiId_KonaklamaTipiId_MisafirTipiId_KullanimSekli_KisiSayisi_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "OdaFiyatlari",
                columns: new[] { "TesisOdaTipiId", "KonaklamaTipiId", "MisafirTipiId", "KullanimSekli", "KisiSayisi", "BaslangicTarihi", "BitisTarihi" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OdaFiyatlari_TesisOdaTipiId_KonaklamaTipiId_MisafirTipiId_KullanimSekli_KisiSayisi_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "OdaFiyatlari");

            migrationBuilder.DropColumn(
                name: "TekKisilikFiyatUygulandiMi",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "KullanimSekli",
                schema: "dbo",
                table: "OdaFiyatlari");

            migrationBuilder.CreateIndex(
                name: "IX_OdaFiyatlari_TesisOdaTipiId_KonaklamaTipiId_MisafirTipiId_KisiSayisi_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "OdaFiyatlari",
                columns: new[] { "TesisOdaTipiId", "KonaklamaTipiId", "MisafirTipiId", "KisiSayisi", "BaslangicTarihi", "BitisTarihi" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
