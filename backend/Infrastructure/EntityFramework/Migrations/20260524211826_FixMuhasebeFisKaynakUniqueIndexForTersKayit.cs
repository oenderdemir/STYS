using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class FixMuhasebeFisKaynakUniqueIndexForTersKayit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MuhasebeFisler_TesisId_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "MuhasebeFisler");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_TesisId_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                columns: new[] { "TesisId", "KaynakModul", "KaynakId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [KaynakModul] IS NOT NULL AND [KaynakId] IS NOT NULL AND [Durum] NOT IN ('Iptal', 'TersKayit')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MuhasebeFisler_TesisId_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "MuhasebeFisler");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_TesisId_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                columns: new[] { "TesisId", "KaynakModul", "KaynakId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [KaynakModul] IS NOT NULL AND [KaynakId] IS NOT NULL AND [Durum] <> 'Iptal'");
        }
    }
}
