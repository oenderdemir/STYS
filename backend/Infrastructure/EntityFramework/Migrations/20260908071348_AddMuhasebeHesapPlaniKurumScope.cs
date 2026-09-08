using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeHesapPlaniKurumScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_TesisId_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_TesisId_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE hp
                SET hp.KurumId = t.KurumId
                FROM [muhasebe].[MuhasebeHesapPlanlari] hp
                INNER JOIN [dbo].[Tesisler] t ON t.Id = hp.TesisId
                WHERE hp.TesisId IS NOT NULL
                  AND hp.KurumId IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [KurumId] IS NULL AND [TesisId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_KurumId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "KurumId",
                filter: "[IsDeleted] = 0 AND [KurumId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_KurumId_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                columns: new[] { "KurumId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [KurumId] IS NOT NULL AND [TesisId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_KurumId_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                columns: new[] { "KurumId", "TamKod" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [KurumId] IS NOT NULL AND [TesisId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_KurumId_TesisId_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                columns: new[] { "KurumId", "TesisId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [KurumId] IS NOT NULL AND [TesisId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_KurumId_TesisId_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                columns: new[] { "KurumId", "TesisId", "TamKod" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [KurumId] IS NOT NULL AND [TesisId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "TamKod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [KurumId] IS NULL AND [TesisId] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MuhasebeHesapPlanlari_ScopeKurumTesis",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                sql: "([TesisId] IS NULL OR [KurumId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_MuhasebeHesapPlanlari_Kurumlar_KurumId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MuhasebeHesapPlanlari_Kurumlar_KurumId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_KurumId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_KurumId_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_KurumId_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_KurumId_TesisId_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_KurumId_TesisId_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MuhasebeHesapPlanlari_ScopeKurumTesis",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [TesisId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "TamKod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [TesisId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_TesisId_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                columns: new[] { "TesisId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [TesisId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_TesisId_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                columns: new[] { "TesisId", "TamKod" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [TesisId] IS NOT NULL");
        }
    }
}
