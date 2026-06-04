using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class FazQ1_RemoveCariKartLegacyBankaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankaAdi",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "Iban",
                schema: "muhasebe",
                table: "CariKartlar");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankaAdi",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Iban",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: true);
        }
    }
}
