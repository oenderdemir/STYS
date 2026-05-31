using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class FixCariKartBankaHesabiDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CariKartBankaHesaplari_CariKartlar_CariKartId",
                schema: "muhasebe",
                table: "CariKartBankaHesaplari");

            migrationBuilder.AddForeignKey(
                name: "FK_CariKartBankaHesaplari_CariKartlar_CariKartId",
                schema: "muhasebe",
                table: "CariKartBankaHesaplari",
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
                name: "FK_CariKartBankaHesaplari_CariKartlar_CariKartId",
                schema: "muhasebe",
                table: "CariKartBankaHesaplari");

            migrationBuilder.AddForeignKey(
                name: "FK_CariKartBankaHesaplari_CariKartlar_CariKartId",
                schema: "muhasebe",
                table: "CariKartBankaHesaplari",
                column: "CariKartId",
                principalSchema: "muhasebe",
                principalTable: "CariKartlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
