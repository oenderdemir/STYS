using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeFisTersKayitBaglantilari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IptalEdilenFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TersKayitFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_IptalEdilenFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                column: "IptalEdilenFisId");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_TersKayitFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                column: "TersKayitFisId");

            migrationBuilder.AddForeignKey(
                name: "FK_MuhasebeFisler_MuhasebeFisler_IptalEdilenFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                column: "IptalEdilenFisId",
                principalSchema: "muhasebe",
                principalTable: "MuhasebeFisler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MuhasebeFisler_MuhasebeFisler_TersKayitFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                column: "TersKayitFisId",
                principalSchema: "muhasebe",
                principalTable: "MuhasebeFisler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MuhasebeFisler_MuhasebeFisler_IptalEdilenFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler");

            migrationBuilder.DropForeignKey(
                name: "FK_MuhasebeFisler_MuhasebeFisler_TersKayitFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeFisler_IptalEdilenFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeFisler_TersKayitFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler");

            migrationBuilder.DropColumn(
                name: "IptalEdilenFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler");

            migrationBuilder.DropColumn(
                name: "TersKayitFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler");
        }
    }
}
