using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260327205752_AddTesisKonaklamaTipleri")]
    public partial class AddTesisKonaklamaTipleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TesisKonaklamaTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    KonaklamaTipiId = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TesisKonaklamaTipleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TesisKonaklamaTipleri_KonaklamaTipleri_KonaklamaTipiId",
                        column: x => x.KonaklamaTipiId,
                        principalSchema: "dbo",
                        principalTable: "KonaklamaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TesisKonaklamaTipleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TesisKonaklamaTipleri_KonaklamaTipiId",
                schema: "dbo",
                table: "TesisKonaklamaTipleri",
                column: "KonaklamaTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_TesisKonaklamaTipleri_TesisId_KonaklamaTipiId",
                schema: "dbo",
                table: "TesisKonaklamaTipleri",
                columns: new[] { "TesisId", "KonaklamaTipiId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                INSERT INTO dbo.TesisKonaklamaTipleri
                (
                    TesisId,
                    KonaklamaTipiId,
                    AktifMi,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy,
                    IsDeleted
                )
                SELECT
                    t.Id,
                    k.Id,
                    CAST(1 AS bit),
                    GETUTCDATE(),
                    GETUTCDATE(),
                    N'migration',
                    N'migration',
                    CAST(0 AS bit)
                FROM dbo.Tesisler t
                CROSS JOIN dbo.KonaklamaTipleri k
                WHERE t.AktifMi = 1
                  AND t.IsDeleted = 0
                  AND k.AktifMi = 1
                  AND k.IsDeleted = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TesisKonaklamaTipleri",
                schema: "dbo");
        }
    }
}

