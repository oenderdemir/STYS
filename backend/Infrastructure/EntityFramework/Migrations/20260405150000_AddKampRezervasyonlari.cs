using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

public partial class AddKampRezervasyonlari : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "KampRezervasyonlari",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RezervasyonNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                KampBasvuruId = table.Column<int>(type: "int", nullable: false),
                KampDonemiId = table.Column<int>(type: "int", nullable: false),
                TesisId = table.Column<int>(type: "int", nullable: false),
                BasvuruSahibiAdiSoyadi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                BasvuruSahibiTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                KonaklamaBirimiTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                KatilimciSayisi = table.Column<int>(type: "int", nullable: false),
                DonemToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                AvansToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Durum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                IptalNedeni = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IptalTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_KampRezervasyonlari", x => x.Id);
                table.ForeignKey(
                    name: "FK_KampRezervasyonlari_KampBasvurulari_KampBasvuruId",
                    column: x => x.KampBasvuruId,
                    principalSchema: "dbo",
                    principalTable: "KampBasvurulari",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_KampRezervasyonlari_KampDonemleri_KampDonemiId",
                    column: x => x.KampDonemiId,
                    principalSchema: "dbo",
                    principalTable: "KampDonemleri",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_KampRezervasyonlari_Tesisler_TesisId",
                    column: x => x.TesisId,
                    principalSchema: "dbo",
                    principalTable: "Tesisler",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_KampRezervasyonlari_KampBasvuruId",
            schema: "dbo",
            table: "KampRezervasyonlari",
            column: "KampBasvuruId",
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_KampRezervasyonlari_KampDonemiId_TesisId_Durum",
            schema: "dbo",
            table: "KampRezervasyonlari",
            columns: new[] { "KampDonemiId", "TesisId", "Durum" });

        migrationBuilder.CreateIndex(
            name: "IX_KampRezervasyonlari_RezervasyonNo",
            schema: "dbo",
            table: "KampRezervasyonlari",
            column: "RezervasyonNo",
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_KampRezervasyonlari_TesisId",
            schema: "dbo",
            table: "KampRezervasyonlari",
            column: "TesisId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "KampRezervasyonlari",
            schema: "dbo");
    }
}
