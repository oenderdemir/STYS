using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalStockCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StokSayimlar",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    DepoId = table.Column<int>(type: "int", nullable: false),
                    SayimTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
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
                    table.PrimaryKey("PK_StokSayimlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StokSayimlar_Depolar_DepoId",
                        column: x => x.DepoId,
                        principalSchema: "muhasebe",
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokSayimlar_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StokSayimSatirlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StokSayimId = table.Column<int>(type: "int", nullable: false),
                    TasinirKartId = table.Column<int>(type: "int", nullable: false),
                    StokLotId = table.Column<int>(type: "int", nullable: true),
                    StokSeriId = table.Column<int>(type: "int", nullable: true),
                    TakipTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StokKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TasinirKartAd = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Birim = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LotNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SonKullanmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SeriNo = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SistemMiktari = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SayilanMiktar = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FarkMiktari = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
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
                    table.PrimaryKey("PK_StokSayimSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StokSayimSatirlari_StokLotlar_StokLotId",
                        column: x => x.StokLotId,
                        principalSchema: "muhasebe",
                        principalTable: "StokLotlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokSayimSatirlari_StokSayimlar_StokSayimId",
                        column: x => x.StokSayimId,
                        principalSchema: "muhasebe",
                        principalTable: "StokSayimlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StokSayimSatirlari_StokSeriler_StokSeriId",
                        column: x => x.StokSeriId,
                        principalSchema: "muhasebe",
                        principalTable: "StokSeriler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokSayimSatirlari_TasinirKartlar_TasinirKartId",
                        column: x => x.TasinirKartId,
                        principalSchema: "muhasebe",
                        principalTable: "TasinirKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StokSayimlar_DepoId",
                schema: "muhasebe",
                table: "StokSayimlar",
                column: "DepoId");

            migrationBuilder.CreateIndex(
                name: "IX_StokSayimlar_TesisId_DepoId_SayimTarihi",
                schema: "muhasebe",
                table: "StokSayimlar",
                columns: new[] { "TesisId", "DepoId", "SayimTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StokSayimSatirlari_StokLotId",
                schema: "muhasebe",
                table: "StokSayimSatirlari",
                column: "StokLotId");

            migrationBuilder.CreateIndex(
                name: "IX_StokSayimSatirlari_StokSayimId_TasinirKartId_StokLotId_StokSeriId",
                schema: "muhasebe",
                table: "StokSayimSatirlari",
                columns: new[] { "StokSayimId", "TasinirKartId", "StokLotId", "StokSeriId" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StokSayimSatirlari_StokSeriId",
                schema: "muhasebe",
                table: "StokSayimSatirlari",
                column: "StokSeriId");

            migrationBuilder.CreateIndex(
                name: "IX_StokSayimSatirlari_TasinirKartId",
                schema: "muhasebe",
                table: "StokSayimSatirlari",
                column: "TasinirKartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StokSayimSatirlari",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "StokSayimlar",
                schema: "muhasebe");
        }
    }
}
