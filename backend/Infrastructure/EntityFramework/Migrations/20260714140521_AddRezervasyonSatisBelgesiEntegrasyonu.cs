using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddRezervasyonSatisBelgesiEntegrasyonu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SatisBelgesiId",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_SatisBelgesiId",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "SatisBelgesiId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [SatisBelgesiId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Rezervasyonlar_SatisBelgeleri_SatisBelgesiId",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "SatisBelgesiId",
                principalSchema: "muhasebe",
                principalTable: "SatisBelgeleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rezervasyonlar_SatisBelgeleri_SatisBelgesiId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_SatisBelgesiId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "SatisBelgesiId",
                schema: "dbo",
                table: "Rezervasyonlar");
        }
    }
}
