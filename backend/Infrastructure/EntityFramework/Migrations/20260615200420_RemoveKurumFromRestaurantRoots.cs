using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class RemoveKurumFromRestaurantRoots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Restoranlar_Kurumlar_KurumId",
                schema: "restoran",
                table: "Restoranlar");

            migrationBuilder.DropForeignKey(
                name: "FK_RestoranSiparisleri_Kurumlar_KurumId",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_KurumId_RestoranId_SiparisTarihi",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_KurumId_RestoranMasaId_SiparisDurumu",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_KurumId_SiparisNo",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_RestoranId",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_RestoranMasaId",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_Restoranlar_KurumId_TesisId_Ad",
                schema: "restoran",
                table: "Restoranlar");

            migrationBuilder.DropIndex(
                name: "IX_Restoranlar_TesisId",
                schema: "restoran",
                table: "Restoranlar");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "restoran",
                table: "Restoranlar");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_RestoranId_SiparisTarihi",
                schema: "restoran",
                table: "RestoranSiparisleri",
                columns: new[] { "RestoranId", "SiparisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_RestoranMasaId_SiparisDurumu",
                schema: "restoran",
                table: "RestoranSiparisleri",
                columns: new[] { "RestoranMasaId", "SiparisDurumu" },
                filter: "[IsDeleted] = 0 AND [RestoranMasaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_SiparisNo",
                schema: "restoran",
                table: "RestoranSiparisleri",
                column: "SiparisNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Restoranlar_TesisId_Ad",
                schema: "restoran",
                table: "Restoranlar",
                columns: new[] { "TesisId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_RestoranId_SiparisTarihi",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_RestoranMasaId_SiparisDurumu",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_SiparisNo",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_Restoranlar_TesisId_Ad",
                schema: "restoran",
                table: "Restoranlar");

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "restoran",
                table: "RestoranSiparisleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "restoran",
                table: "Restoranlar",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE r
                SET r.KurumId = t.KurumId
                FROM [restoran].[Restoranlar] r
                INNER JOIN [dbo].[Tesisler] t ON t.Id = r.TesisId
                WHERE r.KurumId IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE s
                SET s.KurumId = r.KurumId
                FROM [restoran].[RestoranSiparisleri] s
                INNER JOIN [restoran].[Restoranlar] r ON r.Id = s.RestoranId
                WHERE s.KurumId IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "restoran",
                table: "RestoranSiparisleri",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "restoran",
                table: "Restoranlar",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_KurumId_RestoranId_SiparisTarihi",
                schema: "restoran",
                table: "RestoranSiparisleri",
                columns: new[] { "KurumId", "RestoranId", "SiparisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_KurumId_RestoranMasaId_SiparisDurumu",
                schema: "restoran",
                table: "RestoranSiparisleri",
                columns: new[] { "KurumId", "RestoranMasaId", "SiparisDurumu" },
                filter: "[IsDeleted] = 0 AND [RestoranMasaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_KurumId_SiparisNo",
                schema: "restoran",
                table: "RestoranSiparisleri",
                columns: new[] { "KurumId", "SiparisNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_RestoranId",
                schema: "restoran",
                table: "RestoranSiparisleri",
                column: "RestoranId");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_RestoranMasaId",
                schema: "restoran",
                table: "RestoranSiparisleri",
                column: "RestoranMasaId");

            migrationBuilder.CreateIndex(
                name: "IX_Restoranlar_KurumId_TesisId_Ad",
                schema: "restoran",
                table: "Restoranlar",
                columns: new[] { "KurumId", "TesisId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Restoranlar_TesisId",
                schema: "restoran",
                table: "Restoranlar",
                column: "TesisId");

            migrationBuilder.AddForeignKey(
                name: "FK_Restoranlar_Kurumlar_KurumId",
                schema: "restoran",
                table: "Restoranlar",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RestoranSiparisleri_Kurumlar_KurumId",
                schema: "restoran",
                table: "RestoranSiparisleri",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
