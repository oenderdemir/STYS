using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class FazQ1_CariKartCokluBankaHesabiModeli : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CariKartBankaHesaplari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CariKartId = table.Column<int>(type: "int", nullable: false),
                    BankaAdi = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SubeAdi = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    HesapNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_CariKartBankaHesaplari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CariKartBankaHesaplari_CariKartlar_CariKartId",
                        column: x => x.CariKartId,
                        principalSchema: "muhasebe",
                        principalTable: "CariKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CariKartBankaHesaplari_CariKartId",
                schema: "muhasebe",
                table: "CariKartBankaHesaplari",
                column: "CariKartId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CariKartBankaHesaplari_CariKartId_BankaAdi_SubeAdi_HesapNo",
                schema: "muhasebe",
                table: "CariKartBankaHesaplari",
                columns: new[] { "CariKartId", "BankaAdi", "SubeAdi", "HesapNo" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CariKartBankaHesaplari_Iban",
                schema: "muhasebe",
                table: "CariKartBankaHesaplari",
                column: "Iban",
                filter: "[IsDeleted] = 0 AND [Iban] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CariKartBankaHesaplari",
                schema: "muhasebe");
        }
    }
}
