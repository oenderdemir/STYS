using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class MakeRoomFeaturesFullyDynamic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TesisOdaTipiOzellikDegerleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisOdaTipiId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_TesisOdaTipiOzellikDegerleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TesisOdaTipiOzellikDegerleri_OdaOzellikleri_OdaOzellikId",
                        column: x => x.OdaOzellikId,
                        principalSchema: "dbo",
                        principalTable: "OdaOzellikleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TesisOdaTipiOzellikDegerleri_TesisOdaTipleri_TesisOdaTipiId",
                        column: x => x.TesisOdaTipiId,
                        principalSchema: "dbo",
                        principalTable: "TesisOdaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TesisOdaTipiOzellikDegerleri_OdaOzellikId",
                schema: "dbo",
                table: "TesisOdaTipiOzellikDegerleri",
                column: "OdaOzellikId");

            migrationBuilder.CreateIndex(
                name: "IX_TesisOdaTipiOzellikDegerleri_TesisOdaTipiId_OdaOzellikId",
                schema: "dbo",
                table: "TesisOdaTipiOzellikDegerleri",
                columns: new[] { "TesisOdaTipiId", "OdaOzellikId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @CreatedBy nvarchar(64) = 'migration';

                IF NOT EXISTS (SELECT 1 FROM [dbo].[OdaOzellikleri] WHERE [Kod] = 'BALKON_VAR_MI' AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[OdaOzellikleri] ([Kod], [Ad], [VeriTipi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES ('BALKON_VAR_MI', N'Balkon Var Mi', 'boolean', 1, 0, @Now, @Now, @CreatedBy, @CreatedBy);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[OdaOzellikleri] WHERE [Kod] = 'KLIMA_VAR_MI' AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[OdaOzellikleri] ([Kod], [Ad], [VeriTipi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES ('KLIMA_VAR_MI', N'Klima Var Mi', 'boolean', 1, 0, @Now, @Now, @CreatedBy, @CreatedBy);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[OdaOzellikleri] WHERE [Kod] = 'METREKARE' AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[OdaOzellikleri] ([Kod], [Ad], [VeriTipi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES ('METREKARE', N'Metrekare', 'number', 1, 0, @Now, @Now, @CreatedBy, @CreatedBy);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[OdaOzellikleri] WHERE [Kod] = 'EK_OZELLIKLER' AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[OdaOzellikleri] ([Kod], [Ad], [VeriTipi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES ('EK_OZELLIKLER', N'Ek Ozellikler', 'text', 1, 0, @Now, @Now, @CreatedBy, @CreatedBy);

                DECLARE @BalkonOzellikId int = (SELECT TOP 1 [Id] FROM [dbo].[OdaOzellikleri] WHERE [Kod] = 'BALKON_VAR_MI' AND [IsDeleted] = 0 ORDER BY [Id]);
                DECLARE @KlimaOzellikId int = (SELECT TOP 1 [Id] FROM [dbo].[OdaOzellikleri] WHERE [Kod] = 'KLIMA_VAR_MI' AND [IsDeleted] = 0 ORDER BY [Id]);
                DECLARE @MetrekareOzellikId int = (SELECT TOP 1 [Id] FROM [dbo].[OdaOzellikleri] WHERE [Kod] = 'METREKARE' AND [IsDeleted] = 0 ORDER BY [Id]);
                DECLARE @EkOzellikOzellikId int = (SELECT TOP 1 [Id] FROM [dbo].[OdaOzellikleri] WHERE [Kod] = 'EK_OZELLIKLER' AND [IsDeleted] = 0 ORDER BY [Id]);

                INSERT INTO [dbo].[TesisOdaTipiOzellikDegerleri]
                    ([TesisOdaTipiId], [OdaOzellikId], [Deger], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT
                    ot.[Id],
                    @BalkonOzellikId,
                    CASE WHEN ot.[BalkonVarMi] = 1 THEN 'true' ELSE 'false' END,
                    0, @Now, @Now, @CreatedBy, @CreatedBy
                FROM [dbo].[TesisOdaTipleri] ot
                WHERE @BalkonOzellikId IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[TesisOdaTipiOzellikDegerleri] t
                    WHERE t.[TesisOdaTipiId] = ot.[Id]
                      AND t.[OdaOzellikId] = @BalkonOzellikId
                      AND t.[IsDeleted] = 0);

                INSERT INTO [dbo].[TesisOdaTipiOzellikDegerleri]
                    ([TesisOdaTipiId], [OdaOzellikId], [Deger], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT
                    ot.[Id],
                    @KlimaOzellikId,
                    CASE WHEN ot.[KlimaVarMi] = 1 THEN 'true' ELSE 'false' END,
                    0, @Now, @Now, @CreatedBy, @CreatedBy
                FROM [dbo].[TesisOdaTipleri] ot
                WHERE @KlimaOzellikId IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[TesisOdaTipiOzellikDegerleri] t
                    WHERE t.[TesisOdaTipiId] = ot.[Id]
                      AND t.[OdaOzellikId] = @KlimaOzellikId
                      AND t.[IsDeleted] = 0);

                INSERT INTO [dbo].[TesisOdaTipiOzellikDegerleri]
                    ([TesisOdaTipiId], [OdaOzellikId], [Deger], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT
                    ot.[Id],
                    @MetrekareOzellikId,
                    CONVERT(varchar(64), CONVERT(decimal(18,2), ot.[Metrekare])),
                    0, @Now, @Now, @CreatedBy, @CreatedBy
                FROM [dbo].[TesisOdaTipleri] ot
                WHERE @MetrekareOzellikId IS NOT NULL
                  AND ot.[Metrekare] IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[TesisOdaTipiOzellikDegerleri] t
                    WHERE t.[TesisOdaTipiId] = ot.[Id]
                      AND t.[OdaOzellikId] = @MetrekareOzellikId
                      AND t.[IsDeleted] = 0);

                INSERT INTO [dbo].[OdaOzellikDegerleri]
                    ([OdaId], [OdaOzellikId], [Deger], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT
                    o.[Id],
                    @BalkonOzellikId,
                    CASE WHEN COALESCE(o.[BalkonVarMiOverride], ot.[BalkonVarMi]) = 1 THEN 'true' ELSE 'false' END,
                    0, @Now, @Now, @CreatedBy, @CreatedBy
                FROM [dbo].[Odalar] o
                INNER JOIN [dbo].[TesisOdaTipleri] ot ON ot.[Id] = o.[TesisOdaTipiId]
                WHERE @BalkonOzellikId IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[OdaOzellikDegerleri] d
                    WHERE d.[OdaId] = o.[Id]
                      AND d.[OdaOzellikId] = @BalkonOzellikId
                      AND d.[IsDeleted] = 0);

                INSERT INTO [dbo].[OdaOzellikDegerleri]
                    ([OdaId], [OdaOzellikId], [Deger], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT
                    o.[Id],
                    @KlimaOzellikId,
                    CASE WHEN COALESCE(o.[KlimaVarMiOverride], ot.[KlimaVarMi]) = 1 THEN 'true' ELSE 'false' END,
                    0, @Now, @Now, @CreatedBy, @CreatedBy
                FROM [dbo].[Odalar] o
                INNER JOIN [dbo].[TesisOdaTipleri] ot ON ot.[Id] = o.[TesisOdaTipiId]
                WHERE @KlimaOzellikId IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[OdaOzellikDegerleri] d
                    WHERE d.[OdaId] = o.[Id]
                      AND d.[OdaOzellikId] = @KlimaOzellikId
                      AND d.[IsDeleted] = 0);

                INSERT INTO [dbo].[OdaOzellikDegerleri]
                    ([OdaId], [OdaOzellikId], [Deger], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT
                    o.[Id],
                    @MetrekareOzellikId,
                    CONVERT(varchar(64), CONVERT(decimal(18,2), COALESCE(o.[MetrekareOverride], ot.[Metrekare]))),
                    0, @Now, @Now, @CreatedBy, @CreatedBy
                FROM [dbo].[Odalar] o
                INNER JOIN [dbo].[TesisOdaTipleri] ot ON ot.[Id] = o.[TesisOdaTipiId]
                WHERE @MetrekareOzellikId IS NOT NULL
                  AND COALESCE(o.[MetrekareOverride], ot.[Metrekare]) IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[OdaOzellikDegerleri] d
                    WHERE d.[OdaId] = o.[Id]
                      AND d.[OdaOzellikId] = @MetrekareOzellikId
                      AND d.[IsDeleted] = 0);

                INSERT INTO [dbo].[OdaOzellikDegerleri]
                    ([OdaId], [OdaOzellikId], [Deger], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT
                    o.[Id],
                    @EkOzellikOzellikId,
                    LTRIM(RTRIM(o.[EkOzellikler])),
                    0, @Now, @Now, @CreatedBy, @CreatedBy
                FROM [dbo].[Odalar] o
                WHERE @EkOzellikOzellikId IS NOT NULL
                  AND o.[EkOzellikler] IS NOT NULL
                  AND LTRIM(RTRIM(o.[EkOzellikler])) <> ''
                  AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[OdaOzellikDegerleri] d
                    WHERE d.[OdaId] = o.[Id]
                      AND d.[OdaOzellikId] = @EkOzellikOzellikId
                      AND d.[IsDeleted] = 0);
                """);

            migrationBuilder.DropColumn(
                name: "BalkonVarMi",
                schema: "dbo",
                table: "TesisOdaTipleri");

            migrationBuilder.DropColumn(
                name: "KlimaVarMi",
                schema: "dbo",
                table: "TesisOdaTipleri");

            migrationBuilder.DropColumn(
                name: "Metrekare",
                schema: "dbo",
                table: "TesisOdaTipleri");

            migrationBuilder.DropColumn(
                name: "BalkonVarMiOverride",
                schema: "dbo",
                table: "Odalar");

            migrationBuilder.DropColumn(
                name: "EkOzellikler",
                schema: "dbo",
                table: "Odalar");

            migrationBuilder.DropColumn(
                name: "KlimaVarMiOverride",
                schema: "dbo",
                table: "Odalar");

            migrationBuilder.DropColumn(
                name: "MetrekareOverride",
                schema: "dbo",
                table: "Odalar");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TesisOdaTipiOzellikDegerleri",
                schema: "dbo");

            migrationBuilder.AddColumn<bool>(
                name: "BalkonVarMi",
                schema: "dbo",
                table: "TesisOdaTipleri",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "KlimaVarMi",
                schema: "dbo",
                table: "TesisOdaTipleri",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Metrekare",
                schema: "dbo",
                table: "TesisOdaTipleri",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BalkonVarMiOverride",
                schema: "dbo",
                table: "Odalar",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EkOzellikler",
                schema: "dbo",
                table: "Odalar",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KlimaVarMiOverride",
                schema: "dbo",
                table: "Odalar",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MetrekareOverride",
                schema: "dbo",
                table: "Odalar",
                type: "decimal(10,2)",
                nullable: true);
        }
    }
}
