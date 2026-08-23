using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddFifoStockCostLayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StokMaliyetKatmanlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    DepoId = table.Column<int>(type: "int", nullable: false),
                    TasinirKartId = table.Column<int>(type: "int", nullable: false),
                    KaynakStokHareketId = table.Column<int>(type: "int", nullable: false),
                    GirisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IlkMiktar = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    KalanMiktar = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BirimMaliyet = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
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
                    table.PrimaryKey("PK_StokMaliyetKatmanlari", x => x.Id);
                    table.CheckConstraint("CK_StokMaliyetKatmanlari_BirimMaliyet", "[BirimMaliyet] >= 0");
                    table.CheckConstraint("CK_StokMaliyetKatmanlari_IlkMiktar", "[IlkMiktar] > 0");
                    table.CheckConstraint("CK_StokMaliyetKatmanlari_KalanMiktar", "[KalanMiktar] >= 0 AND [KalanMiktar] <= [IlkMiktar]");
                    table.ForeignKey(
                        name: "FK_StokMaliyetKatmanlari_Depolar_DepoId",
                        column: x => x.DepoId,
                        principalSchema: "muhasebe",
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokMaliyetKatmanlari_StokHareketleri_KaynakStokHareketId",
                        column: x => x.KaynakStokHareketId,
                        principalSchema: "muhasebe",
                        principalTable: "StokHareketleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokMaliyetKatmanlari_TasinirKartlar_TasinirKartId",
                        column: x => x.TasinirKartId,
                        principalSchema: "muhasebe",
                        principalTable: "TasinirKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokMaliyetKatmanlari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StokMaliyetKatmanTuketimleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CikisStokHareketId = table.Column<int>(type: "int", nullable: false),
                    StokMaliyetKatmaniId = table.Column<int>(type: "int", nullable: false),
                    Miktar = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BirimMaliyet = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_StokMaliyetKatmanTuketimleri", x => x.Id);
                    table.CheckConstraint("CK_StokMaliyetKatmanTuketimleri_BirimMaliyet", "[BirimMaliyet] >= 0");
                    table.CheckConstraint("CK_StokMaliyetKatmanTuketimleri_Miktar", "[Miktar] > 0");
                    table.CheckConstraint("CK_StokMaliyetKatmanTuketimleri_Tutar", "[Tutar] >= 0");
                    table.ForeignKey(
                        name: "FK_StokMaliyetKatmanTuketimleri_StokHareketleri_CikisStokHareketId",
                        column: x => x.CikisStokHareketId,
                        principalSchema: "muhasebe",
                        principalTable: "StokHareketleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokMaliyetKatmanTuketimleri_StokMaliyetKatmanlari_StokMaliyetKatmaniId",
                        column: x => x.StokMaliyetKatmaniId,
                        principalSchema: "muhasebe",
                        principalTable: "StokMaliyetKatmanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StokMaliyetKatmanlari_DepoId_TasinirKartId_GirisTarihi_KaynakStokHareketId",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari",
                columns: new[] { "DepoId", "TasinirKartId", "GirisTarihi", "KaynakStokHareketId" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StokMaliyetKatmanlari_KaynakStokHareketId",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari",
                column: "KaynakStokHareketId");

            migrationBuilder.CreateIndex(
                name: "IX_StokMaliyetKatmanlari_TasinirKartId",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari",
                column: "TasinirKartId");

            migrationBuilder.CreateIndex(
                name: "IX_StokMaliyetKatmanlari_TesisId_DepoId_TasinirKartId_KalanMiktar",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari",
                columns: new[] { "TesisId", "DepoId", "TasinirKartId", "KalanMiktar" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StokMaliyetKatmanTuketimleri_CikisStokHareketId_StokMaliyetKatmaniId",
                schema: "muhasebe",
                table: "StokMaliyetKatmanTuketimleri",
                columns: new[] { "CikisStokHareketId", "StokMaliyetKatmaniId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StokMaliyetKatmanTuketimleri_StokMaliyetKatmaniId",
                schema: "muhasebe",
                table: "StokMaliyetKatmanTuketimleri",
                column: "StokMaliyetKatmaniId",
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StokMaliyetKatmanTuketimleri",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "StokMaliyetKatmanlari",
                schema: "muhasebe");
        }
    }
}
