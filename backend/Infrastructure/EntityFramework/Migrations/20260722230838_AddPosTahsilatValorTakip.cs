using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPosTahsilatValorTakip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MuhasebeFisler_IptalEdilenFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler");

            migrationBuilder.AddColumn<int>(
                name: "KomisyonGiderHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KomisyonOrani",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValorGunTuru",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "TakvimGunu");

            migrationBuilder.AddColumn<bool>(
                name: "ValorGunundeOtomatikHesabaAktarMi",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PosTahsilatValorleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    TahsilatOdemeBelgesiId = table.Column<int>(type: "int", nullable: false),
                    KrediKartiHesapId = table.Column<int>(type: "int", nullable: false),
                    BagliBankaHesapId = table.Column<int>(type: "int", nullable: true),
                    KomisyonGiderHesapPlaniId = table.Column<int>(type: "int", nullable: true),
                    OdemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValorGunSayisi = table.Column<int>(type: "int", nullable: false),
                    ValorGunTuru = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BeklenenValorTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    OtomatikAktarimMi = table.Column<bool>(type: "bit", nullable: false),
                    KomisyonOraniSnapshot = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    BrutTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KomisyonTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    AktarimBaslamaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DenemeSayisi = table.Column<int>(type: "int", nullable: false),
                    SonDenemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AktarimTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MuhasebeFisId = table.Column<int>(type: "int", nullable: true),
                    TersKayitMuhasebeFisId = table.Column<int>(type: "int", nullable: true),
                    HataMesaji = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_PosTahsilatValorleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosTahsilatValorleri_KasaBankaHesaplari_BagliBankaHesapId",
                        column: x => x.BagliBankaHesapId,
                        principalSchema: "muhasebe",
                        principalTable: "KasaBankaHesaplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosTahsilatValorleri_KasaBankaHesaplari_KrediKartiHesapId",
                        column: x => x.KrediKartiHesapId,
                        principalSchema: "muhasebe",
                        principalTable: "KasaBankaHesaplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosTahsilatValorleri_MuhasebeFisler_MuhasebeFisId",
                        column: x => x.MuhasebeFisId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeFisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosTahsilatValorleri_MuhasebeHesapPlanlari_KomisyonGiderHesapPlaniId",
                        column: x => x.KomisyonGiderHesapPlaniId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeHesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosTahsilatValorleri_TahsilatOdemeBelgeleri_TahsilatOdemeBelgesiId",
                        column: x => x.TahsilatOdemeBelgesiId,
                        principalSchema: "muhasebe",
                        principalTable: "TahsilatOdemeBelgeleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosTahsilatValorDegisiklikGecmisleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PosTahsilatValorId = table.Column<int>(type: "int", nullable: false),
                    IslemTipi = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OncekiDegerJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    YeniDegerJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_PosTahsilatValorDegisiklikGecmisleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosTahsilatValorDegisiklikGecmisleri_PosTahsilatValorleri_PosTahsilatValorId",
                        column: x => x.PosTahsilatValorId,
                        principalSchema: "muhasebe",
                        principalTable: "PosTahsilatValorleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_IptalEdilenFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                column: "IptalEdilenFisId",
                unique: true,
                filter: "[IptalEdilenFisId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KasaBankaHesaplari_KomisyonGiderHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                column: "KomisyonGiderHesapPlaniId");

            migrationBuilder.CreateIndex(
                name: "IX_PosTahsilatValorDegisiklikGecmisleri_PosTahsilatValorId",
                schema: "muhasebe",
                table: "PosTahsilatValorDegisiklikGecmisleri",
                column: "PosTahsilatValorId");

            migrationBuilder.CreateIndex(
                name: "IX_PosTahsilatValorleri_BagliBankaHesapId",
                schema: "muhasebe",
                table: "PosTahsilatValorleri",
                column: "BagliBankaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_PosTahsilatValorleri_Durum_BeklenenValorTarihi",
                schema: "muhasebe",
                table: "PosTahsilatValorleri",
                columns: new[] { "Durum", "BeklenenValorTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_PosTahsilatValorleri_KomisyonGiderHesapPlaniId",
                schema: "muhasebe",
                table: "PosTahsilatValorleri",
                column: "KomisyonGiderHesapPlaniId");

            migrationBuilder.CreateIndex(
                name: "IX_PosTahsilatValorleri_KrediKartiHesapId",
                schema: "muhasebe",
                table: "PosTahsilatValorleri",
                column: "KrediKartiHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_PosTahsilatValorleri_MuhasebeFisId",
                schema: "muhasebe",
                table: "PosTahsilatValorleri",
                column: "MuhasebeFisId");

            migrationBuilder.CreateIndex(
                name: "IX_PosTahsilatValorleri_TahsilatOdemeBelgesiId",
                schema: "muhasebe",
                table: "PosTahsilatValorleri",
                column: "TahsilatOdemeBelgesiId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PosTahsilatValorleri_TesisId_OtomatikAktarimMi_Durum",
                schema: "muhasebe",
                table: "PosTahsilatValorleri",
                columns: new[] { "TesisId", "OtomatikAktarimMi", "Durum" });

            migrationBuilder.AddForeignKey(
                name: "FK_KasaBankaHesaplari_MuhasebeHesapPlanlari_KomisyonGiderHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                column: "KomisyonGiderHesapPlaniId",
                principalSchema: "muhasebe",
                principalTable: "MuhasebeHesapPlanlari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KasaBankaHesaplari_MuhasebeHesapPlanlari_KomisyonGiderHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropTable(
                name: "PosTahsilatValorDegisiklikGecmisleri",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "PosTahsilatValorleri",
                schema: "muhasebe");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeFisler_IptalEdilenFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler");

            migrationBuilder.DropIndex(
                name: "IX_KasaBankaHesaplari_KomisyonGiderHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "KomisyonGiderHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "KomisyonOrani",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "ValorGunTuru",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "ValorGunundeOtomatikHesabaAktarMi",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_IptalEdilenFisId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                column: "IptalEdilenFisId");
        }
    }
}
