using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddTahsilatOdemeBelgesiKapatilacakCariHareketId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KapatilacakCariHareketId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TahsilatOdemeBelgeleri_KapatilacakCariHareketId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                column: "KapatilacakCariHareketId",
                filter: "[IsDeleted] = 0 AND [KapatilacakCariHareketId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TahsilatOdemeBelgeleri_CariHareketler_KapatilacakCariHareketId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                column: "KapatilacakCariHareketId",
                principalSchema: "muhasebe",
                principalTable: "CariHareketler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TahsilatOdemeBelgeleri_CariHareketler_KapatilacakCariHareketId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri");

            migrationBuilder.DropIndex(
                name: "IX_TahsilatOdemeBelgeleri_KapatilacakCariHareketId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri");

            migrationBuilder.DropColumn(
                name: "KapatilacakCariHareketId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri");
        }
    }
}
