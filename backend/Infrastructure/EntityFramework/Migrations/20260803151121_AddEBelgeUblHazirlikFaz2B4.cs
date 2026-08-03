using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddEBelgeUblHazirlikFaz2B4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Kur",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "ParaBirimi",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TRY");

            migrationBuilder.AddColumn<string>(
                name: "Adres",
                schema: "dbo",
                table: "Kurumlar",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VergiDairesi",
                schema: "dbo",
                table: "Kurumlar",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kur",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "ParaBirimi",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "Adres",
                schema: "dbo",
                table: "Kurumlar");

            migrationBuilder.DropColumn(
                name: "VergiDairesi",
                schema: "dbo",
                table: "Kurumlar");
        }
    }
}
