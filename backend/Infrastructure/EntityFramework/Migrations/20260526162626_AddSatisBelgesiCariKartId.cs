using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddSatisBelgesiCariKartId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CariKartId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_CariKartId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                column: "CariKartId",
                filter: "[IsDeleted] = 0 AND [CariKartId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_SatisBelgeleri_CariKartlar_CariKartId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                column: "CariKartId",
                principalSchema: "muhasebe",
                principalTable: "CariKartlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SatisBelgeleri_CariKartlar_CariKartId",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropIndex(
                name: "IX_SatisBelgeleri_CariKartId",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "CariKartId",
                schema: "muhasebe",
                table: "SatisBelgeleri");
        }
    }
}
