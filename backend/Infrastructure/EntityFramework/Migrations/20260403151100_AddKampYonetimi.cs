using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKampYonetimi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KampProgramlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_KampProgramlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampDonemleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampProgramiId = table.Column<int>(type: "int", nullable: false),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Yil = table.Column<int>(type: "int", nullable: false),
                    BasvuruBaslangicTarihi = table.Column<DateTime>(type: "date", nullable: false),
                    BasvuruBitisTarihi = table.Column<DateTime>(type: "date", nullable: false),
                    KonaklamaBaslangicTarihi = table.Column<DateTime>(type: "date", nullable: false),
                    KonaklamaBitisTarihi = table.Column<DateTime>(type: "date", nullable: false),
                    MinimumGece = table.Column<int>(type: "int", nullable: false),
                    MaksimumGece = table.Column<int>(type: "int", nullable: false),
                    OnayGerektirirMi = table.Column<bool>(type: "bit", nullable: false),
                    CekilisGerekliMi = table.Column<bool>(type: "bit", nullable: false),
                    AyniAileIcinTekBasvuruMu = table.Column<bool>(type: "bit", nullable: false),
                    IptalSonGun = table.Column<DateTime>(type: "date", nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_KampDonemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampDonemleri_KampProgramlari_KampProgramiId",
                        column: x => x.KampProgramiId,
                        principalSchema: "dbo",
                        principalTable: "KampProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KampDonemiTesisleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampDonemiId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    BasvuruyaAcikMi = table.Column<bool>(type: "bit", nullable: false),
                    ToplamKontenjan = table.Column<int>(type: "int", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_KampDonemiTesisleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampDonemiTesisleri_KampDonemleri_KampDonemiId",
                        column: x => x.KampDonemiId,
                        principalSchema: "dbo",
                        principalTable: "KampDonemleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KampDonemiTesisleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemiTesisleri_KampDonemiId_TesisId",
                schema: "dbo",
                table: "KampDonemiTesisleri",
                columns: new[] { "KampDonemiId", "TesisId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemiTesisleri_TesisId",
                schema: "dbo",
                table: "KampDonemiTesisleri",
                column: "TesisId");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KampProgramiId_Yil_Ad",
                schema: "dbo",
                table: "KampDonemleri",
                columns: new[] { "KampProgramiId", "Yil", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_Kod",
                schema: "dbo",
                table: "KampDonemleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramlari_Ad",
                schema: "dbo",
                table: "KampProgramlari",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramlari_Kod",
                schema: "dbo",
                table: "KampProgramlari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KampDonemiTesisleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampDonemleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampProgramlari",
                schema: "dbo");
        }
    }
}
