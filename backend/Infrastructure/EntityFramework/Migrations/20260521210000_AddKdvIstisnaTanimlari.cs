using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKdvIstisnaTanimlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KdvIstisnaTanimlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UygulamaTipi = table.Column<int>(type: "int", nullable: false),
                    SatisIslemlerindeKullanilirMi = table.Column<bool>(type: "bit", nullable: false),
                    AlisIslemlerindeKullanilirMi = table.Column<bool>(type: "bit", nullable: false),
                    YuklenilenKdvIndirilebilirMi = table.Column<bool>(type: "bit", nullable: false),
                    IadeHakkiVarMi = table.Column<bool>(type: "bit", nullable: false),
                    EBelgeKoduZorunluMu = table.Column<bool>(type: "bit", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    GecerlilikBaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GecerlilikBitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_KdvIstisnaTanimlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KdvIstisnaTanimlari_Kod",
                schema: "muhasebe",
                table: "KdvIstisnaTanimlari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KdvIstisnaTanimlari_UygulamaTipi_AktifMi",
                schema: "muhasebe",
                table: "KdvIstisnaTanimlari",
                columns: new[] { "UygulamaTipi", "AktifMi" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KdvIstisnaTanimlari",
                schema: "muhasebe");
        }
    }
}
