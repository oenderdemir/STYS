using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class RenameKullaniciTesisSahipliklariToSahiplikleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KullaniciTesisSahipliklari_Tesisler_TesisId",
                schema: "dbo",
                table: "KullaniciTesisSahipliklari");

            migrationBuilder.DropPrimaryKey(
                name: "PK_KullaniciTesisSahipliklari",
                schema: "dbo",
                table: "KullaniciTesisSahipliklari");

            migrationBuilder.RenameTable(
                name: "KullaniciTesisSahipliklari",
                schema: "dbo",
                newName: "KullaniciTesisSahiplikleri",
                newSchema: "dbo");

            migrationBuilder.RenameIndex(
                name: "IX_KullaniciTesisSahipliklari_UserId",
                schema: "dbo",
                table: "KullaniciTesisSahiplikleri",
                newName: "IX_KullaniciTesisSahiplikleri_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_KullaniciTesisSahipliklari_TesisId",
                schema: "dbo",
                table: "KullaniciTesisSahiplikleri",
                newName: "IX_KullaniciTesisSahiplikleri_TesisId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_KullaniciTesisSahiplikleri",
                schema: "dbo",
                table: "KullaniciTesisSahiplikleri",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_KullaniciTesisSahiplikleri_Tesisler_TesisId",
                schema: "dbo",
                table: "KullaniciTesisSahiplikleri",
                column: "TesisId",
                principalSchema: "dbo",
                principalTable: "Tesisler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KullaniciTesisSahiplikleri_Tesisler_TesisId",
                schema: "dbo",
                table: "KullaniciTesisSahiplikleri");

            migrationBuilder.DropPrimaryKey(
                name: "PK_KullaniciTesisSahiplikleri",
                schema: "dbo",
                table: "KullaniciTesisSahiplikleri");

            migrationBuilder.RenameTable(
                name: "KullaniciTesisSahiplikleri",
                schema: "dbo",
                newName: "KullaniciTesisSahipliklari",
                newSchema: "dbo");

            migrationBuilder.RenameIndex(
                name: "IX_KullaniciTesisSahiplikleri_UserId",
                schema: "dbo",
                table: "KullaniciTesisSahipliklari",
                newName: "IX_KullaniciTesisSahipliklari_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_KullaniciTesisSahiplikleri_TesisId",
                schema: "dbo",
                table: "KullaniciTesisSahipliklari",
                newName: "IX_KullaniciTesisSahipliklari_TesisId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_KullaniciTesisSahipliklari",
                schema: "dbo",
                table: "KullaniciTesisSahipliklari",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_KullaniciTesisSahipliklari_Tesisler_TesisId",
                schema: "dbo",
                table: "KullaniciTesisSahipliklari",
                column: "TesisId",
                principalSchema: "dbo",
                principalTable: "Tesisler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
