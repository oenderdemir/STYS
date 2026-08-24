using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddSarfFisiUsageMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IsletmeAlaniAdSnapshot",
                schema: "muhasebe",
                table: "SarfFisleri",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OdaBinaAdiSnapshot",
                schema: "muhasebe",
                table: "SarfFisleri",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OdaId",
                schema: "muhasebe",
                table: "SarfFisleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OdaNoSnapshot",
                schema: "muhasebe",
                table: "SarfFisleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SarfNedeni",
                schema: "muhasebe",
                table: "SarfFisleri",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE sf
                SET sf.IsletmeAlaniAdSnapshot = COALESCE(NULLIF(LTRIM(RTRIM(ia.OzelAd)), N''), ias.Ad)
                FROM [muhasebe].[SarfFisleri] sf
                LEFT JOIN [dbo].[IsletmeAlanlari] ia ON ia.Id = sf.IsletmeAlaniId
                LEFT JOIN [dbo].[IsletmeAlaniSiniflari] ias ON ias.Id = ia.IsletmeAlaniSinifiId
                WHERE sf.IsDeleted = 0
                  AND sf.IsletmeAlaniId IS NOT NULL
                  AND sf.IsletmeAlaniAdSnapshot IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SarfFisleri_OdaId",
                schema: "muhasebe",
                table: "SarfFisleri",
                column: "OdaId",
                filter: "[IsDeleted] = 0 AND [OdaId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_SarfFisleri_Odalar_OdaId",
                schema: "muhasebe",
                table: "SarfFisleri",
                column: "OdaId",
                principalSchema: "dbo",
                principalTable: "Odalar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SarfFisleri_Odalar_OdaId",
                schema: "muhasebe",
                table: "SarfFisleri");

            migrationBuilder.DropIndex(
                name: "IX_SarfFisleri_OdaId",
                schema: "muhasebe",
                table: "SarfFisleri");

            migrationBuilder.DropColumn(
                name: "IsletmeAlaniAdSnapshot",
                schema: "muhasebe",
                table: "SarfFisleri");

            migrationBuilder.DropColumn(
                name: "OdaBinaAdiSnapshot",
                schema: "muhasebe",
                table: "SarfFisleri");

            migrationBuilder.DropColumn(
                name: "OdaId",
                schema: "muhasebe",
                table: "SarfFisleri");

            migrationBuilder.DropColumn(
                name: "OdaNoSnapshot",
                schema: "muhasebe",
                table: "SarfFisleri");

            migrationBuilder.DropColumn(
                name: "SarfNedeni",
                schema: "muhasebe",
                table: "SarfFisleri");
        }
    }
}
