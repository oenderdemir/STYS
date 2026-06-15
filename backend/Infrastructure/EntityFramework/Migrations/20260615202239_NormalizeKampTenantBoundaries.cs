using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeKampTenantBoundaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KampBasvurulari_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropForeignKey(
                name: "FK_KampRezervasyonlari_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropIndex(
                name: "IX_KampRezervasyonlari_KampDonemiId",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropIndex(
                name: "IX_KampRezervasyonlari_KurumId_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropIndex(
                name: "IX_KampRezervasyonlari_KurumId_RezervasyonNo",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_KampDonemiId",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_KurumId_BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_KurumId_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampRezervasyonlari",
                columns: new[] { "KampDonemiId", "TesisId", "Durum" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_RezervasyonNo",
                schema: "dbo",
                table: "KampRezervasyonlari",
                column: "RezervasyonNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "BasvuruNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampBasvurulari",
                columns: new[] { "KampDonemiId", "TesisId", "Durum" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KampRezervasyonlari_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropIndex(
                name: "IX_KampRezervasyonlari_RezervasyonNo",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampRezervasyonlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampBasvurulari",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE b
                SET b.KurumId = t.KurumId
                FROM [dbo].[KampBasvurulari] b
                INNER JOIN [dbo].[Tesisler] t ON t.Id = b.TesisId
                WHERE b.KurumId IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE r
                SET r.KurumId = t.KurumId
                FROM [dbo].[KampRezervasyonlari] r
                INNER JOIN [dbo].[Tesisler] t ON t.Id = r.TesisId
                WHERE r.KurumId IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampRezervasyonlari",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampBasvurulari",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_KampDonemiId",
                schema: "dbo",
                table: "KampRezervasyonlari",
                column: "KampDonemiId");

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_KurumId_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampRezervasyonlari",
                columns: new[] { "KurumId", "KampDonemiId", "TesisId", "Durum" });

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_KurumId_RezervasyonNo",
                schema: "dbo",
                table: "KampRezervasyonlari",
                columns: new[] { "KurumId", "RezervasyonNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_KampDonemiId",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "KampDonemiId");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_KurumId_BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari",
                columns: new[] { "KurumId", "BasvuruNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_KurumId_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampBasvurulari",
                columns: new[] { "KurumId", "KampDonemiId", "TesisId", "Durum" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_KampBasvurulari_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KampRezervasyonlari_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampRezervasyonlari",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
