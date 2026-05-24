using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class SyncMuhasebeModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SatisBelgeleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BelgeNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BelgeTipi = table.Column<int>(type: "int", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    KaynakModul = table.Column<int>(type: "int", nullable: false),
                    KaynakTipi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    KaynakId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TesisId = table.Column<int>(type: "int", nullable: true),
                    BelgeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VadeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MusteriUnvan = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    MusteriAdSoyad = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    MusteriVergiNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MusteriTcKimlikNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MusteriVergiDairesi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MusteriAdres = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MusteriEposta = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    MusteriTelefon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    KurumsalMi = table.Column<bool>(type: "bit", nullable: false),
                    ToplamMatrah = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ToplamKdv = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GenelToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RedNedeni = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResmiFaturaNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EBelgeUuid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MuhasebeOnayinaGonderilmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MuhasebeOnayTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FaturaKesimTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MusteriyeGonderimTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_SatisBelgeleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SatisBelgesiSatirlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SatisBelgesiId = table.Column<int>(type: "int", nullable: false),
                    SiraNo = table.Column<int>(type: "int", nullable: false),
                    SatirTipi = table.Column<int>(type: "int", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Miktar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Matrah = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KdvUygulamaTipi = table.Column<int>(type: "int", nullable: false),
                    KdvIstisnaTanimId = table.Column<int>(type: "int", nullable: true),
                    KdvIstisnaKodu = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    KdvIstisnaAciklamasi = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    KdvOrani = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    KdvTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SatirToplami = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KaynakSatirId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_SatisBelgesiSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SatisBelgesiSatirlari_KdvIstisnaTanimlari_KdvIstisnaTanimId",
                        column: x => x.KdvIstisnaTanimId,
                        principalSchema: "muhasebe",
                        principalTable: "KdvIstisnaTanimlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SatisBelgesiSatirlari_SatisBelgeleri_SatisBelgesiId",
                        column: x => x.SatisBelgesiId,
                        principalSchema: "muhasebe",
                        principalTable: "SatisBelgeleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_BelgeNo",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                column: "BelgeNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_Durum",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                column: "Durum");

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_KaynakModul",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                column: "KaynakModul");

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_KaynakModul_KaynakTipi_KaynakId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                columns: new[] { "KaynakModul", "KaynakTipi", "KaynakId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [KaynakId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_TesisId_BelgeTarihi",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                columns: new[] { "TesisId", "BelgeTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgesiSatirlari_KdvIstisnaTanimId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                column: "KdvIstisnaTanimId");

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgesiSatirlari_SatisBelgesiId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                column: "SatisBelgesiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SatisBelgesiSatirlari",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "SatisBelgeleri",
                schema: "muhasebe");
        }
    }
}
