using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddYilToKampProgramiRemoveKampYiliFromKuralSeti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KampProgramlari_Ad",
                schema: "dbo",
                table: "KampProgramlari");

            migrationBuilder.DropIndex(
                name: "IX_KampProgramlari_Kod",
                schema: "dbo",
                table: "KampProgramlari");

            migrationBuilder.DropIndex(
                name: "IX_KampKuralSetleri_KampProgramiId_KampYili",
                schema: "dbo",
                table: "KampKuralSetleri");

            migrationBuilder.AddColumn<int>(
                name: "Yil",
                schema: "dbo",
                table: "KampProgramlari",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE [dbo].[KampProgramlari] SET [Yil] = 2025 WHERE [Yil] = 0");

            migrationBuilder.DropColumn(
                name: "KampYili",
                schema: "dbo",
                table: "KampKuralSetleri");

            // Soft-delete duplicate KampKuralSetleri records (keep only the latest one per program)
            migrationBuilder.Sql(@"
                WITH DupeCTE AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY KampProgramiId ORDER BY Id DESC) as RN
                    FROM [dbo].[KampKuralSetleri]
                    WHERE [IsDeleted] = 0
                )
                UPDATE [dbo].[KampKuralSetleri]
                SET [IsDeleted] = 1
                FROM DupeCTE
                WHERE [dbo].[KampKuralSetleri].Id = DupeCTE.Id
                  AND DupeCTE.RN > 1
            ");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramlari_Yil_Ad",
                schema: "dbo",
                table: "KampProgramlari",
                columns: new[] { "Yil", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramlari_Yil_Kod",
                schema: "dbo",
                table: "KampProgramlari",
                columns: new[] { "Yil", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampKuralSetleri_KampProgramiId",
                schema: "dbo",
                table: "KampKuralSetleri",
                column: "KampProgramiId",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KampProgramlari_Yil_Ad",
                schema: "dbo",
                table: "KampProgramlari");

            migrationBuilder.DropIndex(
                name: "IX_KampProgramlari_Yil_Kod",
                schema: "dbo",
                table: "KampProgramlari");

            migrationBuilder.DropIndex(
                name: "IX_KampKuralSetleri_KampProgramiId",
                schema: "dbo",
                table: "KampKuralSetleri");

            migrationBuilder.DropColumn(
                name: "Yil",
                schema: "dbo",
                table: "KampProgramlari");

            migrationBuilder.AddColumn<int>(
                name: "KampYili",
                schema: "dbo",
                table: "KampKuralSetleri",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramlari_Ad",
                schema: "dbo",
                table: "KampProgramlari",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramlari_Kod",
                schema: "dbo",
                table: "KampProgramlari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampKuralSetleri_KampProgramiId_KampYili",
                schema: "dbo",
                table: "KampKuralSetleri",
                columns: new[] { "KampProgramiId", "KampYili" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
