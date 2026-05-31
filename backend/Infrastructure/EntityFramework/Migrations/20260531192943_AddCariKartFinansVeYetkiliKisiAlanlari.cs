using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddCariKartFinansVeYetkiliKisiAlanlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcilisBakiyeTarihi",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcilisBakiyeTutari",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcilisBakiyeYonu",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

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

            migrationBuilder.CreateTable(
                name: "CariKartYetkiliKisileri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CariKartId = table.Column<int>(type: "int", nullable: false),
                    AdSoyad = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GorevUnvan = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Telefon = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Eposta = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CariKartYetkiliKisileri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CariKartYetkiliKisileri_CariKartlar_CariKartId",
                        column: x => x.CariKartId,
                        principalSchema: "muhasebe",
                        principalTable: "CariKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CariKartYetkiliKisileri_CariKartId",
                schema: "muhasebe",
                table: "CariKartYetkiliKisileri",
                column: "CariKartId",
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CariKartYetkiliKisileri",
                schema: "muhasebe");

            migrationBuilder.DropColumn(
                name: "AcilisBakiyeTarihi",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "AcilisBakiyeTutari",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "AcilisBakiyeYonu",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "BankaAdi",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "Iban",
                schema: "muhasebe",
                table: "CariKartlar");
        }
    }
}
