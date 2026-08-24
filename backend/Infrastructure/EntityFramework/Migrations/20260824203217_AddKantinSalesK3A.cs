using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKantinSalesK3A : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TahsilatOdemeBelgesiId",
                schema: "kantin",
                table: "KantinSatisOdemeleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerakendeCariKartId",
                schema: "kantin",
                table: "Kantinler",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisOdemeleri_TahsilatOdemeBelgesiId",
                schema: "kantin",
                table: "KantinSatisOdemeleri",
                column: "TahsilatOdemeBelgesiId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [TahsilatOdemeBelgesiId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Kantinler_PerakendeCariKartId",
                schema: "kantin",
                table: "Kantinler",
                column: "PerakendeCariKartId",
                filter: "[IsDeleted] = 0 AND [PerakendeCariKartId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Kantinler_CariKartlar_PerakendeCariKartId",
                schema: "kantin",
                table: "Kantinler",
                column: "PerakendeCariKartId",
                principalSchema: "muhasebe",
                principalTable: "CariKartlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KantinSatisOdemeleri_TahsilatOdemeBelgeleri_TahsilatOdemeBelgesiId",
                schema: "kantin",
                table: "KantinSatisOdemeleri",
                column: "TahsilatOdemeBelgesiId",
                principalSchema: "muhasebe",
                principalTable: "TahsilatOdemeBelgeleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Kantinler_CariKartlar_PerakendeCariKartId",
                schema: "kantin",
                table: "Kantinler");

            migrationBuilder.DropForeignKey(
                name: "FK_KantinSatisOdemeleri_TahsilatOdemeBelgeleri_TahsilatOdemeBelgesiId",
                schema: "kantin",
                table: "KantinSatisOdemeleri");

            migrationBuilder.DropIndex(
                name: "IX_KantinSatisOdemeleri_TahsilatOdemeBelgesiId",
                schema: "kantin",
                table: "KantinSatisOdemeleri");

            migrationBuilder.DropIndex(
                name: "IX_Kantinler_PerakendeCariKartId",
                schema: "kantin",
                table: "Kantinler");

            migrationBuilder.DropColumn(
                name: "TahsilatOdemeBelgesiId",
                schema: "kantin",
                table: "KantinSatisOdemeleri");

            migrationBuilder.DropColumn(
                name: "PerakendeCariKartId",
                schema: "kantin",
                table: "Kantinler");
        }
    }
}
