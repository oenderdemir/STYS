using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddRestoranIsletmeAlaniIliskisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IsletmeAlaniId",
                schema: "restoran",
                table: "Restoranlar",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE r
                SET r.[IsletmeAlaniId] = eslesen.[Id]
                FROM [restoran].[Restoranlar] r
                OUTER APPLY
                (
                    SELECT TOP (1) ia.[Id]
                    FROM [dbo].[IsletmeAlanlari] ia
                    INNER JOIN [dbo].[Binalar] b ON b.[Id] = ia.[BinaId]
                    INNER JOIN [dbo].[IsletmeAlaniSiniflari] sinif ON sinif.[Id] = ia.[IsletmeAlaniSinifiId]
                    WHERE ia.[IsDeleted] = 0
                      AND ia.[AktifMi] = 1
                      AND b.[IsDeleted] = 0
                      AND b.[AktifMi] = 1
                      AND b.[TesisId] = r.[TesisId]
                      AND sinif.[IsDeleted] = 0
                      AND sinif.[AktifMi] = 1
                      AND sinif.[Kod] = N'RESTORAN'
                    ORDER BY CASE WHEN ia.[OzelAd] = r.[Ad] THEN 0 ELSE 1 END, ia.[Id]
                ) eslesen
                WHERE r.[IsDeleted] = 0
                  AND r.[IsletmeAlaniId] IS NULL
                  AND eslesen.[Id] IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Restoranlar_IsletmeAlaniId",
                schema: "restoran",
                table: "Restoranlar",
                column: "IsletmeAlaniId",
                filter: "[IsDeleted] = 0 AND [IsletmeAlaniId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Restoranlar_IsletmeAlanlari_IsletmeAlaniId",
                schema: "restoran",
                table: "Restoranlar",
                column: "IsletmeAlaniId",
                principalSchema: "dbo",
                principalTable: "IsletmeAlanlari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Restoranlar_IsletmeAlanlari_IsletmeAlaniId",
                schema: "restoran",
                table: "Restoranlar");

            migrationBuilder.DropIndex(
                name: "IX_Restoranlar_IsletmeAlaniId",
                schema: "restoran",
                table: "Restoranlar");

            migrationBuilder.DropColumn(
                name: "IsletmeAlaniId",
                schema: "restoran",
                table: "Restoranlar");
        }
    }
}
