using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddCariKartMusteriIdentityUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VergiNoTcknNormalized",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_TesisId_VergiNoTcknNormalized_Musteri",
                schema: "muhasebe",
                table: "CariKartlar",
                columns: new[] { "TesisId", "VergiNoTcknNormalized" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1 AND [TesisId] IS NOT NULL AND [VergiNoTcknNormalized] IS NOT NULL AND [CariTipi] IN ('Musteri', 'KurumsalMusteri')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CariKartlar_TesisId_VergiNoTcknNormalized_Musteri",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "VergiNoTcknNormalized",
                schema: "muhasebe",
                table: "CariKartlar");
        }
    }
}
