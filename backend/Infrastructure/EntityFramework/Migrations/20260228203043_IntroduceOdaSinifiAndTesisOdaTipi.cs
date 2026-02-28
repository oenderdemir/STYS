using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class IntroduceOdaSinifiAndTesisOdaTipi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Odalar_OdaTipleri_OdaTipiId",
                schema: "dbo",
                table: "Odalar");

            migrationBuilder.DropIndex(
                name: "IX_Odalar_OdaTipiId",
                schema: "dbo",
                table: "Odalar");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OdaTipleri",
                schema: "dbo",
                table: "OdaTipleri");

            migrationBuilder.DropIndex(
                name: "IX_OdaTipleri_Ad",
                schema: "dbo",
                table: "OdaTipleri");

            migrationBuilder.RenameTable(
                name: "OdaTipleri",
                schema: "dbo",
                newName: "OdaSiniflari",
                newSchema: "dbo");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OdaSiniflari",
                schema: "dbo",
                table: "OdaSiniflari",
                column: "Id");

            migrationBuilder.AddColumn<string>(
                name: "Kod",
                schema: "dbo",
                table: "OdaSiniflari",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TesisOdaTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    OdaSinifiId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PaylasimliMi = table.Column<bool>(type: "bit", nullable: false),
                    Kapasite = table.Column<int>(type: "int", nullable: false),
                    BalkonVarMi = table.Column<bool>(type: "bit", nullable: false),
                    KlimaVarMi = table.Column<bool>(type: "bit", nullable: false),
                    Metrekare = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
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
                    table.PrimaryKey("PK_TesisOdaTipleri", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "TesisOdaTipiId",
                schema: "dbo",
                table: "Odalar",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                UPDATE [dbo].[OdaSiniflari]
                SET [Kod] = CONCAT('SINIF_', [Id])
                WHERE [Kod] IS NULL OR LTRIM(RTRIM([Kod])) = '';

                INSERT INTO [dbo].[TesisOdaTipleri] (
                    [TesisId], [OdaSinifiId], [Ad], [PaylasimliMi], [Kapasite], [BalkonVarMi], [KlimaVarMi], [Metrekare], [AktifMi], [IsDeleted],
                    [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy]
                )
                SELECT
                    t.[Id],
                    os.[Id],
                    os.[Ad],
                    os.[PaylasimliMi],
                    os.[Kapasite],
                    os.[BalkonVarMi],
                    os.[KlimaVarMi],
                    os.[Metrekare],
                    CASE WHEN t.[AktifMi] = 1 AND os.[AktifMi] = 1 THEN 1 ELSE 0 END,
                    0,
                    COALESCE(os.[CreatedAt], @Now),
                    @Now,
                    NULL,
                    COALESCE(os.[CreatedBy], 'migration'),
                    'migration',
                    NULL
                FROM [dbo].[Tesisler] t
                CROSS JOIN [dbo].[OdaSiniflari] os
                WHERE t.[IsDeleted] = 0
                  AND os.[IsDeleted] = 0;

                UPDATE o
                SET [TesisOdaTipiId] = tot.[Id]
                FROM [dbo].[Odalar] o
                INNER JOIN [dbo].[Binalar] b ON b.[Id] = o.[BinaId]
                INNER JOIN [dbo].[TesisOdaTipleri] tot
                    ON tot.[TesisId] = b.[TesisId]
                   AND tot.[OdaSinifiId] = o.[OdaTipiId]
                WHERE o.[TesisOdaTipiId] IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Kod",
                schema: "dbo",
                table: "OdaSiniflari",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TesisOdaTipiId",
                schema: "dbo",
                table: "Odalar",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "OdaTipiId",
                schema: "dbo",
                table: "Odalar");

            migrationBuilder.DropColumn(
                name: "PaylasimliMi",
                schema: "dbo",
                table: "OdaSiniflari");

            migrationBuilder.DropColumn(
                name: "Kapasite",
                schema: "dbo",
                table: "OdaSiniflari");

            migrationBuilder.DropColumn(
                name: "BalkonVarMi",
                schema: "dbo",
                table: "OdaSiniflari");

            migrationBuilder.DropColumn(
                name: "KlimaVarMi",
                schema: "dbo",
                table: "OdaSiniflari");

            migrationBuilder.DropColumn(
                name: "Metrekare",
                schema: "dbo",
                table: "OdaSiniflari");

            migrationBuilder.CreateIndex(
                name: "IX_Odalar_TesisOdaTipiId",
                schema: "dbo",
                table: "Odalar",
                column: "TesisOdaTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_OdaSiniflari_Ad",
                schema: "dbo",
                table: "OdaSiniflari",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OdaSiniflari_Kod",
                schema: "dbo",
                table: "OdaSiniflari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TesisOdaTipleri_OdaSinifiId",
                schema: "dbo",
                table: "TesisOdaTipleri",
                column: "OdaSinifiId");

            migrationBuilder.CreateIndex(
                name: "IX_TesisOdaTipleri_TesisId_Ad",
                schema: "dbo",
                table: "TesisOdaTipleri",
                columns: new[] { "TesisId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Odalar_TesisOdaTipleri_TesisOdaTipiId",
                schema: "dbo",
                table: "Odalar",
                column: "TesisOdaTipiId",
                principalSchema: "dbo",
                principalTable: "TesisOdaTipleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TesisOdaTipleri_OdaSiniflari_OdaSinifiId",
                schema: "dbo",
                table: "TesisOdaTipleri",
                column: "OdaSinifiId",
                principalSchema: "dbo",
                principalTable: "OdaSiniflari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TesisOdaTipleri_Tesisler_TesisId",
                schema: "dbo",
                table: "TesisOdaTipleri",
                column: "TesisId",
                principalSchema: "dbo",
                principalTable: "Tesisler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Odalar_TesisOdaTipleri_TesisOdaTipiId",
                schema: "dbo",
                table: "Odalar");

            migrationBuilder.DropForeignKey(
                name: "FK_TesisOdaTipleri_OdaSiniflari_OdaSinifiId",
                schema: "dbo",
                table: "TesisOdaTipleri");

            migrationBuilder.DropForeignKey(
                name: "FK_TesisOdaTipleri_Tesisler_TesisId",
                schema: "dbo",
                table: "TesisOdaTipleri");

            migrationBuilder.DropIndex(
                name: "IX_Odalar_TesisOdaTipiId",
                schema: "dbo",
                table: "Odalar");

            migrationBuilder.DropIndex(
                name: "IX_OdaSiniflari_Ad",
                schema: "dbo",
                table: "OdaSiniflari");

            migrationBuilder.DropIndex(
                name: "IX_OdaSiniflari_Kod",
                schema: "dbo",
                table: "OdaSiniflari");

            migrationBuilder.AddColumn<int>(
                name: "OdaTipiId",
                schema: "dbo",
                table: "Odalar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaylasimliMi",
                schema: "dbo",
                table: "OdaSiniflari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Kapasite",
                schema: "dbo",
                table: "OdaSiniflari",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "BalkonVarMi",
                schema: "dbo",
                table: "OdaSiniflari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "KlimaVarMi",
                schema: "dbo",
                table: "OdaSiniflari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Metrekare",
                schema: "dbo",
                table: "OdaSiniflari",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE os
                SET
                    [PaylasimliMi] = src.[PaylasimliMi],
                    [Kapasite] = src.[Kapasite],
                    [BalkonVarMi] = src.[BalkonVarMi],
                    [KlimaVarMi] = src.[KlimaVarMi],
                    [Metrekare] = src.[Metrekare]
                FROM [dbo].[OdaSiniflari] os
                OUTER APPLY (
                    SELECT TOP(1)
                        tot.[PaylasimliMi],
                        tot.[Kapasite],
                        tot.[BalkonVarMi],
                        tot.[KlimaVarMi],
                        tot.[Metrekare]
                    FROM [dbo].[TesisOdaTipleri] tot
                    WHERE tot.[OdaSinifiId] = os.[Id] AND tot.[IsDeleted] = 0
                    ORDER BY tot.[Id]
                ) src;

                UPDATE o
                SET [OdaTipiId] = tot.[OdaSinifiId]
                FROM [dbo].[Odalar] o
                INNER JOIN [dbo].[TesisOdaTipleri] tot ON tot.[Id] = o.[TesisOdaTipiId];
                """);

            migrationBuilder.AlterColumn<int>(
                name: "OdaTipiId",
                schema: "dbo",
                table: "Odalar",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "TesisOdaTipiId",
                schema: "dbo",
                table: "Odalar");

            migrationBuilder.DropTable(
                name: "TesisOdaTipleri",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "Kod",
                schema: "dbo",
                table: "OdaSiniflari");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OdaSiniflari",
                schema: "dbo",
                table: "OdaSiniflari");

            migrationBuilder.RenameTable(
                name: "OdaSiniflari",
                schema: "dbo",
                newName: "OdaTipleri",
                newSchema: "dbo");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OdaTipleri",
                schema: "dbo",
                table: "OdaTipleri",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Odalar_OdaTipiId",
                schema: "dbo",
                table: "Odalar",
                column: "OdaTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_OdaTipleri_Ad",
                schema: "dbo",
                table: "OdaTipleri",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Odalar_OdaTipleri_OdaTipiId",
                schema: "dbo",
                table: "Odalar",
                column: "OdaTipiId",
                principalSchema: "dbo",
                principalTable: "OdaTipleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
