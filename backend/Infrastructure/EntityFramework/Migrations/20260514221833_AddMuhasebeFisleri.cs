using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeFisleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MuhasebeFisler",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    MaliYil = table.Column<int>(type: "int", nullable: false),
                    Donem = table.Column<int>(type: "int", nullable: false),
                    FisNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    YevmiyeNo = table.Column<int>(type: "int", nullable: true),
                    FisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FisTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KaynakModul = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    KaynakId = table.Column<int>(type: "int", nullable: true),
                    Durum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToplamBorc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ToplamAlacak = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_MuhasebeFisler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MuhasebeFisSatirlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MuhasebeFisId = table.Column<int>(type: "int", nullable: false),
                    MuhasebeHesapPlaniId = table.Column<int>(type: "int", nullable: false),
                    SiraNo = table.Column<int>(type: "int", nullable: false),
                    Borc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Alacak = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Kur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CariKartId = table.Column<int>(type: "int", nullable: true),
                    TasinirKartId = table.Column<int>(type: "int", nullable: true),
                    DepoId = table.Column<int>(type: "int", nullable: true),
                    KasaBankaHesapId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_MuhasebeFisSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MuhasebeFisSatirlari_MuhasebeFisler_MuhasebeFisId",
                        column: x => x.MuhasebeFisId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeFisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MuhasebeFisSatirlari_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId",
                        column: x => x.MuhasebeHesapPlaniId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeHesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_Durum",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                column: "Durum");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                columns: new[] { "KaynakModul", "KaynakId" },
                filter: "[KaynakId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_TesisId_FisTarihi",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                columns: new[] { "TesisId", "FisTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisSatirlari_MuhasebeFisId",
                schema: "muhasebe",
                table: "MuhasebeFisSatirlari",
                column: "MuhasebeFisId");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisSatirlari_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "MuhasebeFisSatirlari",
                column: "MuhasebeHesapPlaniId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MuhasebeFisSatirlari",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "MuhasebeFisler",
                schema: "muhasebe");
        }
    }
}
