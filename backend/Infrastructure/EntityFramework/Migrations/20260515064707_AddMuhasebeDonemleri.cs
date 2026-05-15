using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeDonemleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MuhasebeDonemler",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    MaliYil = table.Column<int>(type: "int", nullable: false),
                    DonemNo = table.Column<int>(type: "int", nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KapaliMi = table.Column<bool>(type: "bit", nullable: false),
                    KapanisTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_MuhasebeDonemler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MuhasebeDonemler_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeDonemler_KapaliMi",
                schema: "muhasebe",
                table: "MuhasebeDonemler",
                column: "KapaliMi");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeDonemler_TesisId_BaslangicTarihi_BitisTarihi",
                schema: "muhasebe",
                table: "MuhasebeDonemler",
                columns: new[] { "TesisId", "BaslangicTarihi", "BitisTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeDonemler_TesisId_MaliYil_DonemNo",
                schema: "muhasebe",
                table: "MuhasebeDonemler",
                columns: new[] { "TesisId", "MaliYil", "DonemNo" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MuhasebeDonemler",
                schema: "muhasebe");
        }
    }
}
