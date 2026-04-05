using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKampBasvuruModulu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KampBasvurulari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampDonemiId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    KonaklamaBirimiTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BasvuruSahibiUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BasvuruSahibiAdiSoyadi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BasvuruSahibiTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HizmetYili = table.Column<int>(type: "int", nullable: false),
                    Kamp2023tenFaydalandiMi = table.Column<bool>(type: "bit", nullable: false),
                    Kamp2024tenFaydalandiMi = table.Column<bool>(type: "bit", nullable: false),
                    EvcilHayvanGetirecekMi = table.Column<bool>(type: "bit", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KatilimciSayisi = table.Column<int>(type: "int", nullable: false),
                    OncelikSirasi = table.Column<int>(type: "int", nullable: false),
                    Puan = table.Column<int>(type: "int", nullable: false),
                    GunlukToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DonemToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvansToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KalanOdemeTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UyariMesajlariJson = table.Column<string>(type: "nvarchar(max)", maxLength: 2048, nullable: true),
                    BuzdolabiTalepEdildiMi = table.Column<bool>(type: "bit", nullable: false),
                    TelevizyonTalepEdildiMi = table.Column<bool>(type: "bit", nullable: false),
                    KlimaTalepEdildiMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_KampBasvurulari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampBasvurulari_KampDonemleri_KampDonemiId",
                        column: x => x.KampDonemiId,
                        principalSchema: "dbo",
                        principalTable: "KampDonemleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KampBasvurulari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KampBasvuruKatilimcilari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampBasvuruId = table.Column<int>(type: "int", nullable: false),
                    AdSoyad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TcKimlikNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DogumTarihi = table.Column<DateTime>(type: "date", nullable: false),
                    BasvuruSahibiMi = table.Column<bool>(type: "bit", nullable: false),
                    KatilimciTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AkrabalikTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KimlikBilgileriDogrulandiMi = table.Column<bool>(type: "bit", nullable: false),
                    YemekTalepEdiyorMu = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_KampBasvuruKatilimcilari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampBasvuruKatilimcilari_KampBasvurulari_KampBasvuruId",
                        column: x => x.KampBasvuruId,
                        principalSchema: "dbo",
                        principalTable: "KampBasvurulari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruKatilimcilari_KampBasvuruId_TcKimlikNo",
                schema: "dbo",
                table: "KampBasvuruKatilimcilari",
                columns: new[] { "KampBasvuruId", "TcKimlikNo" },
                filter: "[IsDeleted] = 0 AND [TcKimlikNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_BasvuruSahibiUserId_KampDonemiId",
                schema: "dbo",
                table: "KampBasvurulari",
                columns: new[] { "BasvuruSahibiUserId", "KampDonemiId" },
                filter: "[IsDeleted] = 0 AND [BasvuruSahibiUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampBasvurulari",
                columns: new[] { "KampDonemiId", "TesisId", "Durum" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_TesisId",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "TesisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KampBasvuruKatilimcilari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampBasvurulari",
                schema: "dbo");
        }
    }
}
