using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicOdaOzellikleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OdaOzellikleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VeriTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
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
                    table.PrimaryKey("PK_OdaOzellikleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OdaOzellikDegerleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OdaId = table.Column<int>(type: "int", nullable: false),
                    OdaOzellikId = table.Column<int>(type: "int", nullable: false),
                    Deger = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_OdaOzellikDegerleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OdaOzellikDegerleri_OdaOzellikleri_OdaOzellikId",
                        column: x => x.OdaOzellikId,
                        principalSchema: "dbo",
                        principalTable: "OdaOzellikleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdaOzellikDegerleri_Odalar_OdaId",
                        column: x => x.OdaId,
                        principalSchema: "dbo",
                        principalTable: "Odalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OdaOzellikDegerleri_OdaId_OdaOzellikId",
                schema: "dbo",
                table: "OdaOzellikDegerleri",
                columns: new[] { "OdaId", "OdaOzellikId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OdaOzellikDegerleri_OdaOzellikId",
                schema: "dbo",
                table: "OdaOzellikDegerleri",
                column: "OdaOzellikId");

            migrationBuilder.CreateIndex(
                name: "IX_OdaOzellikleri_Ad",
                schema: "dbo",
                table: "OdaOzellikleri",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OdaOzellikleri_Kod",
                schema: "dbo",
                table: "OdaOzellikleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OdaOzellikDegerleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OdaOzellikleri",
                schema: "dbo");
        }
    }
}
