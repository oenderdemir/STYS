using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddTesisMisafirTipleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TesisMisafirTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    MisafirTipiId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_TesisMisafirTipleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TesisMisafirTipleri_MisafirTipleri_MisafirTipiId",
                        column: x => x.MisafirTipiId,
                        principalSchema: "dbo",
                        principalTable: "MisafirTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TesisMisafirTipleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TesisMisafirTipleri_MisafirTipiId",
                schema: "dbo",
                table: "TesisMisafirTipleri",
                column: "MisafirTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_TesisMisafirTipleri_TesisId_MisafirTipiId",
                schema: "dbo",
                table: "TesisMisafirTipleri",
                columns: new[] { "TesisId", "MisafirTipiId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                INSERT INTO dbo.TesisMisafirTipleri
                (
                    TesisId,
                    MisafirTipiId,
                    AktifMi,
                    IsDeleted,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy
                )
                SELECT
                    t.Id,
                    m.Id,
                    CAST(1 AS bit),
                    CAST(0 AS bit),
                    GETUTCDATE(),
                    GETUTCDATE(),
                    N'migration',
                    N'migration'
                FROM dbo.Tesisler t
                CROSS JOIN dbo.MisafirTipleri m
                WHERE t.AktifMi = 1
                  AND t.IsDeleted = 0
                  AND m.AktifMi = 1
                  AND m.IsDeleted = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TesisMisafirTipleri",
                schema: "dbo");
        }
    }
}
