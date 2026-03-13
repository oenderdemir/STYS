using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKonaklayanYatakNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "YatakNo",
                schema: "dbo",
                table: "RezervasyonKonaklayanSegmentAtamalari",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklayanSegmentAtamalari_RezervasyonSegmentId_OdaId_YatakNo",
                schema: "dbo",
                table: "RezervasyonKonaklayanSegmentAtamalari",
                columns: new[] { "RezervasyonSegmentId", "OdaId", "YatakNo" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [YatakNo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RezervasyonKonaklayanSegmentAtamalari_RezervasyonSegmentId_OdaId_YatakNo",
                schema: "dbo",
                table: "RezervasyonKonaklayanSegmentAtamalari");

            migrationBuilder.DropColumn(
                name: "YatakNo",
                schema: "dbo",
                table: "RezervasyonKonaklayanSegmentAtamalari");
        }
    }
}
