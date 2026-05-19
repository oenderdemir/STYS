using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeFisUniqueKaynakIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eski non-unique index'i kaldır
            migrationBuilder.DropIndex(
                name: "IX_MuhasebeFisler_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "MuhasebeFisler");

            // Yeni filtered unique index: (TesisId, KaynakModul, KaynakId)
            // Aynı kaynaktan (Tesis + Modül + Kaynak ID) yalnızca bir
            // aktif (İptal edilmemiş) fiş olmasını garantiler
            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_TesisId_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                columns: new[] { "TesisId", "KaynakModul", "KaynakId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [KaynakModul] IS NOT NULL AND [KaynakId] IS NOT NULL AND [Durum] <> 'Iptal'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Yeni unique index'i kaldır
            migrationBuilder.DropIndex(
                name: "IX_MuhasebeFisler_TesisId_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "MuhasebeFisler");

            // Eski non-unique index'i geri oluştur
            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                columns: new[] { "KaynakModul", "KaynakId" },
                unique: false,
                filter: "[KaynakId] IS NOT NULL AND [IsDeleted] = 0");
        }
    }
}
