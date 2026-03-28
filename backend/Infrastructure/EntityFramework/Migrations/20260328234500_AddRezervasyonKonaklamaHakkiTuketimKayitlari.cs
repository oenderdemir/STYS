using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260328234500_AddRezervasyonKonaklamaHakkiTuketimKayitlari")]
public class AddRezervasyonKonaklamaHakkiTuketimKayitlari : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RezervasyonKonaklamaHakkiTuketimKayitlari",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RezervasyonId = table.Column<int>(type: "int", nullable: false),
                RezervasyonKonaklamaHakkiId = table.Column<int>(type: "int", nullable: false),
                IsletmeAlaniId = table.Column<int>(type: "int", nullable: true),
                TuketimTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                Miktar = table.Column<int>(type: "int", nullable: false),
                KullanimTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                KullanimNoktasi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                KullanimNoktasiAdiSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                TuketimNoktasiAdi = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                Aciklama = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AktifMi = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RezervasyonKonaklamaHakkiTuketimKayitlari", x => x.Id);
                table.ForeignKey(
                    name: "FK_RezervasyonKonaklamaHakkiTuketimKayitlari_IsletmeAlanlari_IsletmeAlaniId",
                    column: x => x.IsletmeAlaniId,
                    principalSchema: "dbo",
                    principalTable: "IsletmeAlanlari",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RezervasyonKonaklamaHakkiTuketimKayitlari_RezervasyonKonaklamaHaklari_RezervasyonKonaklamaHakkiId",
                    column: x => x.RezervasyonKonaklamaHakkiId,
                    principalSchema: "dbo",
                    principalTable: "RezervasyonKonaklamaHaklari",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RezervasyonKonaklamaHakkiTuketimKayitlari_Rezervasyonlar_RezervasyonId",
                    column: x => x.RezervasyonId,
                    principalSchema: "dbo",
                    principalTable: "Rezervasyonlar",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RezervasyonKonaklamaHakkiTuketimKayitlari_RezervasyonId_TuketimTarihi",
            schema: "dbo",
            table: "RezervasyonKonaklamaHakkiTuketimKayitlari",
            columns: new[] { "RezervasyonId", "TuketimTarihi" },
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_RezervasyonKonaklamaHakkiTuketimKayitlari_IsletmeAlaniId",
            schema: "dbo",
            table: "RezervasyonKonaklamaHakkiTuketimKayitlari",
            column: "IsletmeAlaniId");

        migrationBuilder.CreateIndex(
            name: "IX_RezervasyonKonaklamaHakkiTuketimKayitlari_RezervasyonKonaklamaHakkiId_TuketimTarihi",
            schema: "dbo",
            table: "RezervasyonKonaklamaHakkiTuketimKayitlari",
            columns: new[] { "RezervasyonKonaklamaHakkiId", "TuketimTarihi" },
            filter: "[IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RezervasyonKonaklamaHakkiTuketimKayitlari",
            schema: "dbo");
    }
}
