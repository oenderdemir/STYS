using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKurumToRezervasyon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_ReferansNo",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_TesisId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [dbo].[Kurumlar] WHERE [Kod] = N'DEFAULT' AND [IsDeleted] = 0)
BEGIN
    INSERT INTO [dbo].[Kurumlar]
    (
        [Kod],
        [Ad],
        [VergiNo],
        [Telefon],
        [Eposta],
        [AktifMi],
        [IsDeleted],
        [CreatedAt],
        [UpdatedAt],
        [DeletedAt],
        [CreatedBy],
        [UpdatedBy],
        [DeletedBy]
    )
    VALUES
    (
        N'DEFAULT',
        N'Varsayilan Kurum',
        NULL,
        NULL,
        NULL,
        1,
        0,
        SYSUTCDATETIME(),
        NULL,
        NULL,
        NULL,
        NULL,
        NULL
    );
END;

UPDATE r
SET r.[KurumId] = t.[KurumId]
FROM [dbo].[Rezervasyonlar] r
INNER JOIN [dbo].[Tesisler] t ON t.[Id] = r.[TesisId]
WHERE r.[KurumId] IS NULL
  AND t.[KurumId] IS NOT NULL;

UPDATE r
SET r.[KurumId] = d.[Id]
FROM [dbo].[Rezervasyonlar] r
CROSS JOIN (
    SELECT TOP (1) [Id]
    FROM [dbo].[Kurumlar]
    WHERE [Kod] = N'DEFAULT' AND [IsDeleted] = 0
    ORDER BY [Id]
) d
WHERE r.[KurumId] IS NULL;
");

            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_KurumId_ReferansNo",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "KurumId", "ReferansNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_KurumId_RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "KurumId", "RezervasyonDurumu" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_KurumId_TesisId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "KurumId", "TesisId", "GirisTarihi", "CikisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_TesisId",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "TesisId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rezervasyonlar_Kurumlar_KurumId",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rezervasyonlar_Kurumlar_KurumId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_KurumId_ReferansNo",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_KurumId_RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_KurumId_TesisId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_TesisId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_ReferansNo",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "ReferansNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "RezervasyonDurumu",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_TesisId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "TesisId", "GirisTarihi", "CikisTarihi" },
                filter: "[IsDeleted] = 0");
        }
    }
}
