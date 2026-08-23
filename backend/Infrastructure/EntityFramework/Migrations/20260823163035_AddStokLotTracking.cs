using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddStokLotTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StokLotId",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StokLotlar",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    TasinirKartId = table.Column<int>(type: "int", nullable: false),
                    LotNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SonKullanmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_StokLotlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StokLotlar_TasinirKartlar_TasinirKartId",
                        column: x => x.TasinirKartId,
                        principalSchema: "muhasebe",
                        principalTable: "TasinirKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokLotlar_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_StokLotId",
                schema: "muhasebe",
                table: "StokHareketleri",
                column: "StokLotId",
                filter: "[IsDeleted] = 0 AND [StokLotId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StokLotlar_TasinirKartId",
                schema: "muhasebe",
                table: "StokLotlar",
                column: "TasinirKartId");

            migrationBuilder.CreateIndex(
                name: "IX_StokLotlar_TesisId_TasinirKartId_LotNo",
                schema: "muhasebe",
                table: "StokLotlar",
                columns: new[] { "TesisId", "TasinirKartId", "LotNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_StokHareketleri_StokLotlar_StokLotId",
                schema: "muhasebe",
                table: "StokHareketleri",
                column: "StokLotId",
                principalSchema: "muhasebe",
                principalTable: "StokLotlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StokHareketleri_StokLotlar_StokLotId",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropTable(
                name: "StokLotlar",
                schema: "muhasebe");

            migrationBuilder.DropIndex(
                name: "IX_StokHareketleri_StokLotId",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "StokLotId",
                schema: "muhasebe",
                table: "StokHareketleri");
        }
    }
}
