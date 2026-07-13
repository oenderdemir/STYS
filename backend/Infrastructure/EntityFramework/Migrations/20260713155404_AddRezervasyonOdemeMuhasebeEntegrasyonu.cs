using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddRezervasyonOdemeMuhasebeEntegrasyonu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RezervasyonMisafirVarsayilanCariKartId",
                schema: "dbo",
                table: "Tesisler",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RezervasyonTahsilatAlacakHesapTipi",
                schema: "dbo",
                table: "Tesisler",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Cari");

            migrationBuilder.AddColumn<int>(
                name: "KasaBankaHesapId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MuhasebeFisId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MuhasebeFisOlusturmaTarihi",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Durum",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Aktif");

            migrationBuilder.AddColumn<string>(
                name: "IptalAciklama",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IptalTarihi",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KasaBankaHesapId",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TahsilatOdemeBelgesiId",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CariKartId",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tesisler_RezervasyonMisafirVarsayilanCariKartId",
                schema: "dbo",
                table: "Tesisler",
                column: "RezervasyonMisafirVarsayilanCariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_TahsilatOdemeBelgeleri_KasaBankaHesapId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                column: "KasaBankaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_TahsilatOdemeBelgeleri_MuhasebeFisId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                column: "MuhasebeFisId");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonOdemeler_KasaBankaHesapId",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                column: "KasaBankaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonOdemeler_TahsilatOdemeBelgesiId",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                column: "TahsilatOdemeBelgesiId",
                unique: true,
                filter: "[TahsilatOdemeBelgesiId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_CariKartId",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_TahsilatOdemeBelgeleri_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                columns: new[] { "KaynakModul", "KaynakId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [KaynakId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Rezervasyonlar_CariKartlar_CariKartId",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "CariKartId",
                principalSchema: "muhasebe",
                principalTable: "CariKartlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RezervasyonOdemeler_KasaBankaHesaplari_KasaBankaHesapId",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                column: "KasaBankaHesapId",
                principalSchema: "muhasebe",
                principalTable: "KasaBankaHesaplari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RezervasyonOdemeler_TahsilatOdemeBelgeleri_TahsilatOdemeBelgesiId",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                column: "TahsilatOdemeBelgesiId",
                principalSchema: "muhasebe",
                principalTable: "TahsilatOdemeBelgeleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TahsilatOdemeBelgeleri_KasaBankaHesaplari_KasaBankaHesapId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                column: "KasaBankaHesapId",
                principalSchema: "muhasebe",
                principalTable: "KasaBankaHesaplari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TahsilatOdemeBelgeleri_MuhasebeFisler_MuhasebeFisId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                column: "MuhasebeFisId",
                principalSchema: "muhasebe",
                principalTable: "MuhasebeFisler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tesisler_CariKartlar_RezervasyonMisafirVarsayilanCariKartId",
                schema: "dbo",
                table: "Tesisler",
                column: "RezervasyonMisafirVarsayilanCariKartId",
                principalSchema: "muhasebe",
                principalTable: "CariKartlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rezervasyonlar_CariKartlar_CariKartId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropForeignKey(
                name: "FK_RezervasyonOdemeler_KasaBankaHesaplari_KasaBankaHesapId",
                schema: "dbo",
                table: "RezervasyonOdemeler");

            migrationBuilder.DropForeignKey(
                name: "FK_RezervasyonOdemeler_TahsilatOdemeBelgeleri_TahsilatOdemeBelgesiId",
                schema: "dbo",
                table: "RezervasyonOdemeler");

            migrationBuilder.DropForeignKey(
                name: "FK_TahsilatOdemeBelgeleri_KasaBankaHesaplari_KasaBankaHesapId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri");

            migrationBuilder.DropForeignKey(
                name: "FK_TahsilatOdemeBelgeleri_MuhasebeFisler_MuhasebeFisId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri");

            migrationBuilder.DropForeignKey(
                name: "FK_Tesisler_CariKartlar_RezervasyonMisafirVarsayilanCariKartId",
                schema: "dbo",
                table: "Tesisler");

            migrationBuilder.DropIndex(
                name: "IX_Tesisler_RezervasyonMisafirVarsayilanCariKartId",
                schema: "dbo",
                table: "Tesisler");

            migrationBuilder.DropIndex(
                name: "IX_TahsilatOdemeBelgeleri_KasaBankaHesapId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri");

            migrationBuilder.DropIndex(
                name: "IX_TahsilatOdemeBelgeleri_MuhasebeFisId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri");

            migrationBuilder.DropIndex(
                name: "IX_RezervasyonOdemeler_KasaBankaHesapId",
                schema: "dbo",
                table: "RezervasyonOdemeler");

            migrationBuilder.DropIndex(
                name: "IX_RezervasyonOdemeler_TahsilatOdemeBelgesiId",
                schema: "dbo",
                table: "RezervasyonOdemeler");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_CariKartId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_TahsilatOdemeBelgeleri_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri");

            migrationBuilder.DropColumn(
                name: "RezervasyonMisafirVarsayilanCariKartId",
                schema: "dbo",
                table: "Tesisler");

            migrationBuilder.DropColumn(
                name: "RezervasyonTahsilatAlacakHesapTipi",
                schema: "dbo",
                table: "Tesisler");

            migrationBuilder.DropColumn(
                name: "KasaBankaHesapId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri");

            migrationBuilder.DropColumn(
                name: "MuhasebeFisId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri");

            migrationBuilder.DropColumn(
                name: "MuhasebeFisOlusturmaTarihi",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri");

            migrationBuilder.DropColumn(
                name: "Durum",
                schema: "dbo",
                table: "RezervasyonOdemeler");

            migrationBuilder.DropColumn(
                name: "IptalAciklama",
                schema: "dbo",
                table: "RezervasyonOdemeler");

            migrationBuilder.DropColumn(
                name: "IptalTarihi",
                schema: "dbo",
                table: "RezervasyonOdemeler");

            migrationBuilder.DropColumn(
                name: "KasaBankaHesapId",
                schema: "dbo",
                table: "RezervasyonOdemeler");

            migrationBuilder.DropColumn(
                name: "TahsilatOdemeBelgesiId",
                schema: "dbo",
                table: "RezervasyonOdemeler");

            migrationBuilder.DropColumn(
                name: "CariKartId",
                schema: "dbo",
                table: "Rezervasyonlar");
        }
    }
}
