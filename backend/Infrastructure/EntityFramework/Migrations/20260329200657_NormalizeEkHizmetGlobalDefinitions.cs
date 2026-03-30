using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeEkHizmetGlobalDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GlobalEkHizmetTanimiId",
                schema: "dbo",
                table: "EkHizmetler",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GlobalEkHizmetTanimlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BirimAdi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PaketIcerikHizmetKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_GlobalEkHizmetTanimlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EkHizmetler_GlobalEkHizmetTanimiId",
                schema: "dbo",
                table: "EkHizmetler",
                column: "GlobalEkHizmetTanimiId");

            migrationBuilder.CreateIndex(
                name: "IX_EkHizmetler_TesisId_GlobalEkHizmetTanimiId",
                schema: "dbo",
                table: "EkHizmetler",
                columns: new[] { "TesisId", "GlobalEkHizmetTanimiId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [GlobalEkHizmetTanimiId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalEkHizmetTanimlari_Ad",
                schema: "dbo",
                table: "GlobalEkHizmetTanimlari",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_EkHizmetler_GlobalEkHizmetTanimlari_GlobalEkHizmetTanimiId",
                schema: "dbo",
                table: "EkHizmetler",
                column: "GlobalEkHizmetTanimiId",
                principalSchema: "dbo",
                principalTable: "GlobalEkHizmetTanimlari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                INSERT INTO [dbo].[GlobalEkHizmetTanimlari]
                    ([Ad], [Aciklama], [BirimAdi], [PaketIcerikHizmetKodu], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                SELECT
                    src.[Ad],
                    src.[Aciklama],
                    src.[BirimAdi],
                    src.[PaketIcerikHizmetKodu],
                    src.[AktifMi],
                    0,
                    @Now,
                    @Now,
                    NULL,
                    N'migration_normalize_ek_hizmet',
                    N'migration_normalize_ek_hizmet',
                    NULL
                FROM
                (
                    SELECT
                        eh.[Ad],
                        MAX(eh.[Aciklama]) AS [Aciklama],
                        MAX(eh.[BirimAdi]) AS [BirimAdi],
                        MAX(eh.[PaketIcerikHizmetKodu]) AS [PaketIcerikHizmetKodu],
                        CAST(MAX(CASE WHEN eh.[AktifMi] = 1 THEN 1 ELSE 0 END) AS bit) AS [AktifMi]
                    FROM [dbo].[EkHizmetler] eh
                    WHERE eh.[IsDeleted] = 0
                    GROUP BY eh.[Ad]
                ) src
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [dbo].[GlobalEkHizmetTanimlari] g
                    WHERE g.[Ad] = src.[Ad]
                      AND g.[IsDeleted] = 0
                );

                UPDATE eh
                SET
                    eh.[GlobalEkHizmetTanimiId] = g.[Id],
                    eh.[Ad] = g.[Ad],
                    eh.[Aciklama] = g.[Aciklama],
                    eh.[BirimAdi] = g.[BirimAdi],
                    eh.[PaketIcerikHizmetKodu] = g.[PaketIcerikHizmetKodu]
                FROM [dbo].[EkHizmetler] eh
                INNER JOIN [dbo].[GlobalEkHizmetTanimlari] g ON g.[Ad] = eh.[Ad]
                WHERE eh.[GlobalEkHizmetTanimiId] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EkHizmetler_GlobalEkHizmetTanimlari_GlobalEkHizmetTanimiId",
                schema: "dbo",
                table: "EkHizmetler");

            migrationBuilder.DropTable(
                name: "GlobalEkHizmetTanimlari",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_EkHizmetler_GlobalEkHizmetTanimiId",
                schema: "dbo",
                table: "EkHizmetler");

            migrationBuilder.DropIndex(
                name: "IX_EkHizmetler_TesisId_GlobalEkHizmetTanimiId",
                schema: "dbo",
                table: "EkHizmetler");

            migrationBuilder.DropColumn(
                name: "GlobalEkHizmetTanimiId",
                schema: "dbo",
                table: "EkHizmetler");
        }
    }
}
