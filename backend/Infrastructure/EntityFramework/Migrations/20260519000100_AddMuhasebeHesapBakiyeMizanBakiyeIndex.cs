using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeHesapBakiyeMizanBakiyeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapBakiyeleri_MizanBakiye",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                columns: new[] { "TesisId", "MaliYil", "Donem", "KonsolideMi", "HesapKodu" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapBakiyeleri_MizanBakiye",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri");
        }
    }
}
