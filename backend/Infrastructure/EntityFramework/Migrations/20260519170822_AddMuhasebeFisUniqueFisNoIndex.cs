using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeFisUniqueFisNoIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_TesisId_FisNo",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                columns: new[] { "TesisId", "FisNo" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [FisNo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MuhasebeFisler_TesisId_FisNo",
                schema: "muhasebe",
                table: "MuhasebeFisler");
        }
    }
}
