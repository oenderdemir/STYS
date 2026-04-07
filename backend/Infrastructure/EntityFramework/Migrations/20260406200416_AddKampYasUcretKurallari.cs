using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKampYasUcretKurallari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KampYasUcretKurallari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UcretsizCocukMaxYas = table.Column<int>(type: "int", nullable: false),
                    YarimUcretliCocukMaxYas = table.Column<int>(type: "int", nullable: false),
                    YemekOrani = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_KampYasUcretKurallari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KampYasUcretKurallari_AktifMi",
                schema: "dbo",
                table: "KampYasUcretKurallari",
                column: "AktifMi",
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM [dbo].[KampYasUcretKurallari] WHERE [IsDeleted] = 0)
                BEGIN
                    INSERT INTO [dbo].[KampYasUcretKurallari]
                    ([UcretsizCocukMaxYas], [YarimUcretliCocukMaxYas], [YemekOrani], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (2, 6, 0.50, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'system', N'system')
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KampYasUcretKurallari",
                schema: "dbo");
        }
    }
}
