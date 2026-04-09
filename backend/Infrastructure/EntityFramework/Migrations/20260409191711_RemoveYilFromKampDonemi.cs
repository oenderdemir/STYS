using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class RemoveYilFromKampDonemi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KampDonemleri_KampProgramiId_Yil_Ad",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropColumn(
                name: "Yil",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KampProgramiId_Ad",
                schema: "dbo",
                table: "KampDonemleri",
                columns: new[] { "KampProgramiId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KampDonemleri_KampProgramiId_Ad",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.AddColumn<int>(
                name: "Yil",
                schema: "dbo",
                table: "KampDonemleri",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KampProgramiId_Yil_Ad",
                schema: "dbo",
                table: "KampDonemleri",
                columns: new[] { "KampProgramiId", "Yil", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
