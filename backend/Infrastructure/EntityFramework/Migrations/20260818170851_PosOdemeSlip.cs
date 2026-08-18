using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class PosOdemeSlip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PosOdemeSlipleri",
                schema: "entegrasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    PosOdemeIslemiId = table.Column<int>(type: "int", nullable: false),
                    Tip = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: "image/png"),
                    StoragePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    DosyaBoyutu = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    KaydedilmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KaynakKomutTipi = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_PosOdemeSlipleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosOdemeSlipleri_PosOdemeIslemleri_PosOdemeIslemiId",
                        column: x => x.PosOdemeIslemiId,
                        principalSchema: "entegrasyon",
                        principalTable: "PosOdemeIslemleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PosOdemeSlipleri_PosOdemeIslemiId",
                schema: "entegrasyon",
                table: "PosOdemeSlipleri",
                column: "PosOdemeIslemiId");

            migrationBuilder.CreateIndex(
                name: "IX_PosOdemeSlipleri_PosOdemeIslemiId_Tip",
                schema: "entegrasyon",
                table: "PosOdemeSlipleri",
                columns: new[] { "PosOdemeIslemiId", "Tip" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PosOdemeSlipleri",
                schema: "entegrasyon");
        }
    }
}
