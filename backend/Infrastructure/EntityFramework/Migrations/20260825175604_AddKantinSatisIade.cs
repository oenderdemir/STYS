using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKantinSatisIade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KantinSatisIadeleri",
                schema: "kantin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    KantinSatisId = table.Column<int>(type: "int", nullable: false),
                    IadeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    OlusturanKullaniciId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    KesinlesmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinansalIadeDurumu = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
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
                    table.PrimaryKey("PK_KantinSatisIadeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KantinSatisIadeleri_KantinSatislar_KantinSatisId",
                        column: x => x.KantinSatisId,
                        principalSchema: "kantin",
                        principalTable: "KantinSatislar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KantinSatisIadeSatirlari",
                schema: "kantin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KantinSatisIadeId = table.Column<int>(type: "int", nullable: false),
                    KantinSatisSatirId = table.Column<int>(type: "int", nullable: false),
                    Miktar = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TasinirKartId = table.Column<int>(type: "int", nullable: false),
                    StokKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UrunAdi = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Birim = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TakipTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LotNo = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SeriNo = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BirimSatisFiyati = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KdvOrani = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MaliyetBirimFiyat = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    MaliyetTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StokHareketId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_KantinSatisIadeSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KantinSatisIadeSatirlari_KantinSatisIadeleri_KantinSatisIadeId",
                        column: x => x.KantinSatisIadeId,
                        principalSchema: "kantin",
                        principalTable: "KantinSatisIadeleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KantinSatisIadeSatirlari_KantinSatisSatirlari_KantinSatisSatirId",
                        column: x => x.KantinSatisSatirId,
                        principalSchema: "kantin",
                        principalTable: "KantinSatisSatirlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KantinSatisIadeSatirlari_StokHareketleri_StokHareketId",
                        column: x => x.StokHareketId,
                        principalSchema: "muhasebe",
                        principalTable: "StokHareketleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisIadeleri_KantinSatisId",
                schema: "kantin",
                table: "KantinSatisIadeleri",
                column: "KantinSatisId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisIadeSatirlari_KantinSatisIadeId_KantinSatisSatirId",
                schema: "kantin",
                table: "KantinSatisIadeSatirlari",
                columns: new[] { "KantinSatisIadeId", "KantinSatisSatirId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisIadeSatirlari_KantinSatisSatirId",
                schema: "kantin",
                table: "KantinSatisIadeSatirlari",
                column: "KantinSatisSatirId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisIadeSatirlari_StokHareketId",
                schema: "kantin",
                table: "KantinSatisIadeSatirlari",
                column: "StokHareketId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [StokHareketId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KantinSatisIadeSatirlari",
                schema: "kantin");

            migrationBuilder.DropTable(
                name: "KantinSatisIadeleri",
                schema: "kantin");
        }
    }
}
