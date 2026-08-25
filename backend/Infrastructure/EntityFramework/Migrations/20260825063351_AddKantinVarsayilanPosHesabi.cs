using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKantinVarsayilanPosHesabi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VarsayilanPosHesapId",
                schema: "kantin",
                table: "Kantinler",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kantinler_VarsayilanPosHesapId",
                schema: "kantin",
                table: "Kantinler",
                column: "VarsayilanPosHesapId",
                filter: "[IsDeleted] = 0 AND [VarsayilanPosHesapId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Kantinler_KasaBankaHesaplari_VarsayilanPosHesapId",
                schema: "kantin",
                table: "Kantinler",
                column: "VarsayilanPosHesapId",
                principalSchema: "muhasebe",
                principalTable: "KasaBankaHesaplari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Kantinler_KasaBankaHesaplari_VarsayilanPosHesapId",
                schema: "kantin",
                table: "Kantinler");

            migrationBuilder.DropIndex(
                name: "IX_Kantinler_VarsayilanPosHesapId",
                schema: "kantin",
                table: "Kantinler");

            migrationBuilder.DropColumn(
                name: "VarsayilanPosHesapId",
                schema: "kantin",
                table: "Kantinler");
        }
    }
}
