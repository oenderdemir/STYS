using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKampBasvuruCokluTercih : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KampBasvuruTercihleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampBasvuruId = table.Column<int>(type: "int", nullable: false),
                    TercihSirasi = table.Column<int>(type: "int", nullable: false),
                    KampDonemiId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    KonaklamaBirimiTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_KampBasvuruTercihleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampBasvuruTercihleri_KampBasvurulari_KampBasvuruId",
                        column: x => x.KampBasvuruId,
                        principalSchema: "dbo",
                        principalTable: "KampBasvurulari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KampBasvuruTercihleri_KampDonemleri_KampDonemiId",
                        column: x => x.KampDonemiId,
                        principalSchema: "dbo",
                        principalTable: "KampDonemleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KampBasvuruTercihleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruTercihleri_KampBasvuruId_TercihSirasi",
                schema: "dbo",
                table: "KampBasvuruTercihleri",
                columns: new[] { "KampBasvuruId", "TercihSirasi" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruTercihleri_KampDonemiId_TesisId",
                schema: "dbo",
                table: "KampBasvuruTercihleri",
                columns: new[] { "KampDonemiId", "TesisId" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruTercihleri_TesisId",
                schema: "dbo",
                table: "KampBasvuruTercihleri",
                column: "TesisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KampBasvuruTercihleri",
                schema: "dbo");
        }
    }
}
