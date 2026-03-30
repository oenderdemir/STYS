using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddTesisKonaklamaTipiIcerikOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TesisKonaklamaTipiIcerikOverridelari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    KonaklamaTipiIcerikKalemiId = table.Column<int>(type: "int", nullable: false),
                    DevreDisiMi = table.Column<bool>(type: "bit", nullable: false),
                    Miktar = table.Column<int>(type: "int", nullable: true),
                    Periyot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    KullanimTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    KullanimNoktasi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    KullanimBaslangicSaati = table.Column<TimeSpan>(type: "time", nullable: true),
                    KullanimBitisSaati = table.Column<TimeSpan>(type: "time", nullable: true),
                    CheckInGunuGecerliMi = table.Column<bool>(type: "bit", nullable: true),
                    CheckOutGunuGecerliMi = table.Column<bool>(type: "bit", nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_TesisKonaklamaTipiIcerikOverridelari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TesisKonaklamaTipiIcerikOverridelari_KonaklamaTipiIcerikKalemleri_KonaklamaTipiIcerikKalemiId",
                        column: x => x.KonaklamaTipiIcerikKalemiId,
                        principalSchema: "dbo",
                        principalTable: "KonaklamaTipiIcerikKalemleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TesisKonaklamaTipiIcerikOverridelari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TesisKonaklamaTipiIcerikOverridelari_KonaklamaTipiIcerikKalemiId",
                schema: "dbo",
                table: "TesisKonaklamaTipiIcerikOverridelari",
                column: "KonaklamaTipiIcerikKalemiId");

            migrationBuilder.CreateIndex(
                name: "IX_TesisKonaklamaTipiIcerikOverridelari_TesisId_KonaklamaTipiIcerikKalemiId",
                schema: "dbo",
                table: "TesisKonaklamaTipiIcerikOverridelari",
                columns: new[] { "TesisId", "KonaklamaTipiIcerikKalemiId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TesisKonaklamaTipiIcerikOverridelari",
                schema: "dbo");
        }
    }
}
