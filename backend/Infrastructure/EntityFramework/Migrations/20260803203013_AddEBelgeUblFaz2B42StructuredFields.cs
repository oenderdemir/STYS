using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddEBelgeUblFaz2B42StructuredFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MusteriAd",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusteriIl",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusteriIlce",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusteriSoyad",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Il",
                schema: "dbo",
                table: "Kurumlar",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ilce",
                schema: "dbo",
                table: "Kurumlar",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ad",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Soyad",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MusteriAd",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "MusteriIl",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "MusteriIlce",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "MusteriSoyad",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "Il",
                schema: "dbo",
                table: "Kurumlar");

            migrationBuilder.DropColumn(
                name: "Ilce",
                schema: "dbo",
                table: "Kurumlar");

            migrationBuilder.DropColumn(
                name: "Ad",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "Soyad",
                schema: "muhasebe",
                table: "CariKartlar");
        }
    }
}
