using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeHesapBakiyeleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MuhasebeHesapBakiyeleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    MaliYil = table.Column<int>(type: "int", nullable: false),
                    Donem = table.Column<int>(type: "int", nullable: false),
                    MuhasebeHesapPlaniId = table.Column<int>(type: "int", nullable: false),
                    HesapKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HesapAdi = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    KonsolideMi = table.Column<bool>(type: "bit", nullable: false),
                    BorcToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AlacakToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BorcBakiye = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AlacakBakiye = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SonGuncellemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_MuhasebeHesapBakiyeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MuhasebeHesapBakiyeleri_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId",
                        column: x => x.MuhasebeHesapPlaniId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeHesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MuhasebeHesapBakiyeleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapBakiyeleri_HesapKodu",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                column: "HesapKodu");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapBakiyeleri_KonsolideMi",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                column: "KonsolideMi");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapBakiyeleri_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                column: "MuhasebeHesapPlaniId");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapBakiyeleri_TesisId_MaliYil_Donem",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                columns: new[] { "TesisId", "MaliYil", "Donem" });

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapBakiyeleri_TesisId_MaliYil_Donem_MuhasebeHesapPlaniId_KonsolideMi",
                schema: "muhasebe",
                table: "MuhasebeHesapBakiyeleri",
                columns: new[] { "TesisId", "MaliYil", "Donem", "MuhasebeHesapPlaniId", "KonsolideMi" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MuhasebeHesapBakiyeleri",
                schema: "muhasebe");
        }
    }
}
