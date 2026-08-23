using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddSerialTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TakipTipi",
                schema: "muhasebe",
                table: "TasinirKartlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Yok");

            migrationBuilder.Sql("""
                UPDATE [muhasebe].[TasinirKartlar]
                SET [TakipTipi] = CASE
                    WHEN [TakipliMi] = 1 THEN 'Lot'
                    ELSE 'Yok'
                END
                WHERE [IsDeleted] = 0
                """);

            migrationBuilder.AddColumn<int>(
                name: "StokSeriId",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StokSeriler",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    TasinirKartId = table.Column<int>(type: "int", nullable: false),
                    SeriNo = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_StokSeriler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StokSeriler_TasinirKartlar_TasinirKartId",
                        column: x => x.TasinirKartId,
                        principalSchema: "muhasebe",
                        principalTable: "TasinirKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokSeriler_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_StokSeriId",
                schema: "muhasebe",
                table: "StokHareketleri",
                column: "StokSeriId",
                filter: "[IsDeleted] = 0 AND [StokSeriId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StokSeriler_TasinirKartId",
                schema: "muhasebe",
                table: "StokSeriler",
                column: "TasinirKartId");

            migrationBuilder.CreateIndex(
                name: "IX_StokSeriler_TesisId_TasinirKartId_SeriNo",
                schema: "muhasebe",
                table: "StokSeriler",
                columns: new[] { "TesisId", "TasinirKartId", "SeriNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_StokHareketleri_StokSeriler_StokSeriId",
                schema: "muhasebe",
                table: "StokHareketleri",
                column: "StokSeriId",
                principalSchema: "muhasebe",
                principalTable: "StokSeriler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StokHareketleri_StokSeriler_StokSeriId",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropTable(
                name: "StokSeriler",
                schema: "muhasebe");

            migrationBuilder.DropIndex(
                name: "IX_StokHareketleri_StokSeriId",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "TakipTipi",
                schema: "muhasebe",
                table: "TasinirKartlar");

            migrationBuilder.DropColumn(
                name: "StokSeriId",
                schema: "muhasebe",
                table: "StokHareketleri");
        }
    }
}
