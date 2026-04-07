using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKampKonaklamaTarifeleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KampKonaklamaTarifeleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MinimumKisi = table.Column<int>(type: "int", nullable: false),
                    MaksimumKisi = table.Column<int>(type: "int", nullable: false),
                    KamuGunlukUcret = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DigerGunlukUcret = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BuzdolabiGunlukUcret = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TelevizyonGunlukUcret = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KlimaGunlukUcret = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_KampKonaklamaTarifeleri", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KampKonaklamaTarifeleri_AktifMi",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri",
                column: "AktifMi",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampKonaklamaTarifeleri_Kod",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                ;WITH Parametre AS (
                    SELECT Kod, Deger
                    FROM [dbo].[KampParametreleri]
                    WHERE [IsDeleted] = 0
                      AND [Kod] LIKE N'Konaklama.%'
                ),
                Ayrisim AS (
                    SELECT
                        SUBSTRING(Kod, 11, CHARINDEX(N'.', Kod, 11) - 11) AS BirimKod,
                        SUBSTRING(Kod, CHARINDEX(N'.', Kod, 11) + 1, 128) AS Alan,
                        Deger
                    FROM Parametre
                    WHERE CHARINDEX(N'.', Kod, 11) > 0
                ),
                PivotData AS (
                    SELECT
                        BirimKod,
                        MAX(CASE WHEN Alan = N'Ad' THEN Deger END) AS Ad,
                        MAX(CASE WHEN Alan = N'MinKisi' THEN TRY_CONVERT(int, Deger) END) AS MinimumKisi,
                        MAX(CASE WHEN Alan = N'MaksKisi' THEN TRY_CONVERT(int, Deger) END) AS MaksimumKisi,
                        MAX(CASE WHEN Alan = N'KamuGunluk' THEN TRY_CONVERT(decimal(18,2), Deger) END) AS KamuGunlukUcret,
                        MAX(CASE WHEN Alan = N'DigerGunluk' THEN TRY_CONVERT(decimal(18,2), Deger) END) AS DigerGunlukUcret,
                        MAX(CASE WHEN Alan = N'BuzdolabiGunluk' THEN TRY_CONVERT(decimal(18,2), Deger) END) AS BuzdolabiGunlukUcret,
                        MAX(CASE WHEN Alan = N'TelevizyonGunluk' THEN TRY_CONVERT(decimal(18,2), Deger) END) AS TelevizyonGunlukUcret,
                        MAX(CASE WHEN Alan = N'KlimaGunluk' THEN TRY_CONVERT(decimal(18,2), Deger) END) AS KlimaGunlukUcret
                    FROM Ayrisim
                    GROUP BY BirimKod
                )
                INSERT INTO [dbo].[KampKonaklamaTarifeleri]
                (
                    [Kod], [Ad], [MinimumKisi], [MaksimumKisi],
                    [KamuGunlukUcret], [DigerGunlukUcret], [BuzdolabiGunlukUcret], [TelevizyonGunlukUcret], [KlimaGunlukUcret],
                    [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                )
                SELECT
                    p.[BirimKod],
                    COALESCE(NULLIF(p.[Ad], N''), p.[BirimKod]),
                    p.[MinimumKisi],
                    p.[MaksimumKisi],
                    p.[KamuGunlukUcret],
                    p.[DigerGunlukUcret],
                    p.[BuzdolabiGunlukUcret],
                    p.[TelevizyonGunlukUcret],
                    p.[KlimaGunlukUcret],
                    1,
                    0,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME(),
                    N'system',
                    N'system'
                FROM PivotData p
                WHERE p.[MinimumKisi] IS NOT NULL
                  AND p.[MaksimumKisi] IS NOT NULL
                  AND p.[KamuGunlukUcret] IS NOT NULL
                  AND p.[DigerGunlukUcret] IS NOT NULL
                  AND p.[BuzdolabiGunlukUcret] IS NOT NULL
                  AND p.[TelevizyonGunlukUcret] IS NOT NULL
                  AND p.[KlimaGunlukUcret] IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [dbo].[KampKonaklamaTarifeleri] k
                      WHERE k.[IsDeleted] = 0
                        AND k.[Kod] = p.[BirimKod]
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KampKonaklamaTarifeleri",
                schema: "dbo");
        }
    }
}
