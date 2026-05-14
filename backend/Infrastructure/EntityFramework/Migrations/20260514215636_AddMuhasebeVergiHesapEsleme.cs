using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeVergiHesapEsleme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MuhasebeVergiHesapEslemeleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: true),
                    VergiTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Oran = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AlisKdvHesapId = table.Column<int>(type: "int", nullable: false),
                    SatisKdvHesapId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_MuhasebeVergiHesapEslemeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MuhasebeVergiHesapEslemeleri_MuhasebeHesapPlanlari_AlisKdvHesapId",
                        column: x => x.AlisKdvHesapId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeHesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MuhasebeVergiHesapEslemeleri_MuhasebeHesapPlanlari_SatisKdvHesapId",
                        column: x => x.SatisKdvHesapId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeHesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeVergiHesapEslemeleri_AlisKdvHesapId",
                schema: "muhasebe",
                table: "MuhasebeVergiHesapEslemeleri",
                column: "AlisKdvHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeVergiHesapEslemeleri_SatisKdvHesapId",
                schema: "muhasebe",
                table: "MuhasebeVergiHesapEslemeleri",
                column: "SatisKdvHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeVergiHesapEslemeleri_TesisId_VergiTipi_Oran",
                schema: "muhasebe",
                table: "MuhasebeVergiHesapEslemeleri",
                columns: new[] { "TesisId", "VergiTipi", "Oran" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1 AND [TesisId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeVergiHesapEslemeleri_VergiTipi_Oran",
                schema: "muhasebe",
                table: "MuhasebeVergiHesapEslemeleri",
                columns: new[] { "VergiTipi", "Oran" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1 AND [TesisId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MuhasebeVergiHesapEslemeleri",
                schema: "muhasebe");
        }
    }
}
