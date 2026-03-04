using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddIsletmeAlaniSiniflariAndBinaAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IsletmeAlaniSiniflari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
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
                    table.PrimaryKey("PK_IsletmeAlaniSiniflari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeAlaniSiniflari_Ad",
                schema: "dbo",
                table: "IsletmeAlaniSiniflari",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeAlaniSiniflari_Kod",
                schema: "dbo",
                table: "IsletmeAlaniSiniflari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                SET IDENTITY_INSERT [dbo].[IsletmeAlaniSiniflari] ON;

                IF NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlaniSiniflari] WHERE [Id] = 1)
                    INSERT INTO [dbo].[IsletmeAlaniSiniflari] ([Id], [Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy])
                    VALUES (1, N'DIGER', N'Diger', 1, 0, SYSUTCDATETIME(), N'system');
                IF NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlaniSiniflari] WHERE [Id] = 2)
                    INSERT INTO [dbo].[IsletmeAlaniSiniflari] ([Id], [Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy])
                    VALUES (2, N'RESTORAN', N'Restoran', 1, 0, SYSUTCDATETIME(), N'system');
                IF NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlaniSiniflari] WHERE [Id] = 3)
                    INSERT INTO [dbo].[IsletmeAlaniSiniflari] ([Id], [Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy])
                    VALUES (3, N'KAFE', N'Kafe', 1, 0, SYSUTCDATETIME(), N'system');
                IF NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlaniSiniflari] WHERE [Id] = 4)
                    INSERT INTO [dbo].[IsletmeAlaniSiniflari] ([Id], [Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy])
                    VALUES (4, N'SPA', N'Spa', 1, 0, SYSUTCDATETIME(), N'system');
                IF NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlaniSiniflari] WHERE [Id] = 5)
                    INSERT INTO [dbo].[IsletmeAlaniSiniflari] ([Id], [Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy])
                    VALUES (5, N'TOPLANTI', N'Toplanti Salonu', 1, 0, SYSUTCDATETIME(), N'system');
                IF NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlaniSiniflari] WHERE [Id] = 6)
                    INSERT INTO [dbo].[IsletmeAlaniSiniflari] ([Id], [Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy])
                    VALUES (6, N'KONFERANS', N'Konferans Salonu', 1, 0, SYSUTCDATETIME(), N'system');
                IF NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlaniSiniflari] WHERE [Id] = 7)
                    INSERT INTO [dbo].[IsletmeAlaniSiniflari] ([Id], [Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy])
                    VALUES (7, N'FITNESS', N'Fitness', 1, 0, SYSUTCDATETIME(), N'system');
                IF NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlaniSiniflari] WHERE [Id] = 8)
                    INSERT INTO [dbo].[IsletmeAlaniSiniflari] ([Id], [Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy])
                    VALUES (8, N'HAVUZ', N'Havuz', 1, 0, SYSUTCDATETIME(), N'system');
                IF NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlaniSiniflari] WHERE [Id] = 9)
                    INSERT INTO [dbo].[IsletmeAlaniSiniflari] ([Id], [Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy])
                    VALUES (9, N'LOBI', N'Lobi', 1, 0, SYSUTCDATETIME(), N'system');
                IF NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlaniSiniflari] WHERE [Id] = 10)
                    INSERT INTO [dbo].[IsletmeAlaniSiniflari] ([Id], [Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy])
                    VALUES (10, N'DEPO', N'Depo', 1, 0, SYSUTCDATETIME(), N'system');

                SET IDENTITY_INSERT [dbo].[IsletmeAlaniSiniflari] OFF;
                """);

            migrationBuilder.DropIndex(
                name: "IX_IsletmeAlanlari_BinaId_Ad",
                schema: "dbo",
                table: "IsletmeAlanlari");

            migrationBuilder.AddColumn<int>(
                name: "IsletmeAlaniSinifiId",
                schema: "dbo",
                table: "IsletmeAlanlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OzelAd",
                schema: "dbo",
                table: "IsletmeAlanlari",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                """
                DECLARE @DigerSinifId int = 1;

                UPDATE IA
                SET IA.IsletmeAlaniSinifiId = S.Id,
                    IA.OzelAd = NULL
                FROM [dbo].[IsletmeAlanlari] IA
                INNER JOIN [dbo].[IsletmeAlaniSiniflari] S
                    ON UPPER(LTRIM(RTRIM(IA.[Ad]))) = UPPER(LTRIM(RTRIM(S.[Ad])));

                UPDATE IA
                SET IA.IsletmeAlaniSinifiId = ISNULL(IA.IsletmeAlaniSinifiId, @DigerSinifId),
                    IA.OzelAd = CASE
                        WHEN IA.IsletmeAlaniSinifiId IS NULL THEN NULLIF(LTRIM(RTRIM(IA.[Ad])), N'')
                        ELSE IA.OzelAd
                    END
                FROM [dbo].[IsletmeAlanlari] IA;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "IsletmeAlaniSinifiId",
                schema: "dbo",
                table: "IsletmeAlanlari",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeAlanlari_BinaId_IsletmeAlaniSinifiId",
                schema: "dbo",
                table: "IsletmeAlanlari",
                columns: new[] { "BinaId", "IsletmeAlaniSinifiId" },
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeAlanlari_IsletmeAlaniSinifiId",
                schema: "dbo",
                table: "IsletmeAlanlari",
                column: "IsletmeAlaniSinifiId");

            migrationBuilder.AddForeignKey(
                name: "FK_IsletmeAlanlari_IsletmeAlaniSiniflari_IsletmeAlaniSinifiId",
                schema: "dbo",
                table: "IsletmeAlanlari",
                column: "IsletmeAlaniSinifiId",
                principalSchema: "dbo",
                principalTable: "IsletmeAlaniSiniflari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "Ad",
                schema: "dbo",
                table: "IsletmeAlanlari");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ad",
                schema: "dbo",
                table: "IsletmeAlanlari",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE IA
                SET IA.[Ad] = COALESCE(NULLIF(LTRIM(RTRIM(IA.[OzelAd])), N''), S.[Ad], N'Isletme Alani')
                FROM [dbo].[IsletmeAlanlari] IA
                LEFT JOIN [dbo].[IsletmeAlaniSiniflari] S ON S.[Id] = IA.[IsletmeAlaniSinifiId];
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_IsletmeAlanlari_IsletmeAlaniSiniflari_IsletmeAlaniSinifiId",
                schema: "dbo",
                table: "IsletmeAlanlari");

            migrationBuilder.DropIndex(
                name: "IX_IsletmeAlanlari_BinaId_IsletmeAlaniSinifiId",
                schema: "dbo",
                table: "IsletmeAlanlari");

            migrationBuilder.DropIndex(
                name: "IX_IsletmeAlanlari_IsletmeAlaniSinifiId",
                schema: "dbo",
                table: "IsletmeAlanlari");

            migrationBuilder.DropColumn(
                name: "IsletmeAlaniSinifiId",
                schema: "dbo",
                table: "IsletmeAlanlari");

            migrationBuilder.DropColumn(
                name: "OzelAd",
                schema: "dbo",
                table: "IsletmeAlanlari");

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeAlanlari_BinaId_Ad",
                schema: "dbo",
                table: "IsletmeAlanlari",
                columns: new[] { "BinaId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.DropTable(
                name: "IsletmeAlaniSiniflari",
                schema: "dbo");
        }
    }
}
