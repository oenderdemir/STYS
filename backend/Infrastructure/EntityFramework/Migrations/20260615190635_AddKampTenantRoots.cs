using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKampTenantRoots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KampRezervasyonlari_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropIndex(
                name: "IX_KampRezervasyonlari_RezervasyonNo",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropIndex(
                name: "IX_KampProgramlari_Yil_Ad",
                schema: "dbo",
                table: "KampProgramlari");

            migrationBuilder.DropIndex(
                name: "IX_KampProgramlari_Yil_Kod",
                schema: "dbo",
                table: "KampProgramlari");

            migrationBuilder.DropIndex(
                name: "IX_KampDonemleri_KampProgramiId_Ad",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropIndex(
                name: "IX_KampDonemleri_Kod",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampRezervasyonlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampProgramlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampDonemleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampBasvurulari",
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
        N'migration',
        NULL,
        NULL
    );
END;

DECLARE @DefaultKurumId int = (
    SELECT TOP (1) [Id]
    FROM [dbo].[Kurumlar]
    WHERE [Kod] = N'DEFAULT' AND [IsDeleted] = 0
    ORDER BY [Id]
);

UPDATE p
SET p.[KurumId] = @DefaultKurumId
FROM [dbo].[KampProgramlari] p
WHERE p.[KurumId] IS NULL;

UPDATE d
SET d.[KurumId] = p.[KurumId]
FROM [dbo].[KampDonemleri] d
INNER JOIN [dbo].[KampProgramlari] p ON p.[Id] = d.[KampProgramiId]
WHERE d.[KurumId] IS NULL
  AND p.[KurumId] IS NOT NULL;

UPDATE b
SET b.[KurumId] = d.[KurumId]
FROM [dbo].[KampBasvurulari] b
INNER JOIN [dbo].[KampDonemleri] d ON d.[Id] = b.[KampDonemiId]
WHERE b.[KurumId] IS NULL
  AND d.[KurumId] IS NOT NULL;

UPDATE r
SET r.[KurumId] = b.[KurumId]
FROM [dbo].[KampRezervasyonlari] r
INNER JOIN [dbo].[KampBasvurulari] b ON b.[Id] = r.[KampBasvuruId]
WHERE r.[KurumId] IS NULL
  AND b.[KurumId] IS NOT NULL;

UPDATE p
SET p.[KurumId] = @DefaultKurumId
FROM [dbo].[KampProgramlari] p
WHERE p.[KurumId] IS NULL;

UPDATE d
SET d.[KurumId] = @DefaultKurumId
FROM [dbo].[KampDonemleri] d
WHERE d.[KurumId] IS NULL;

UPDATE b
SET b.[KurumId] = @DefaultKurumId
FROM [dbo].[KampBasvurulari] b
WHERE b.[KurumId] IS NULL;

UPDATE r
SET r.[KurumId] = @DefaultKurumId
FROM [dbo].[KampRezervasyonlari] r
WHERE r.[KurumId] IS NULL;
");

            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampRezervasyonlari",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampProgramlari",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampDonemleri",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampBasvurulari",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_KampDonemiId",
                schema: "dbo",
                table: "KampRezervasyonlari",
                column: "KampDonemiId");

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_KurumId_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampRezervasyonlari",
                columns: new[] { "KurumId", "KampDonemiId", "TesisId", "Durum" });

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_KurumId_RezervasyonNo",
                schema: "dbo",
                table: "KampRezervasyonlari",
                columns: new[] { "KurumId", "RezervasyonNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramlari_KurumId_Yil_Ad",
                schema: "dbo",
                table: "KampProgramlari",
                columns: new[] { "KurumId", "Yil", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramlari_KurumId_Yil_Kod",
                schema: "dbo",
                table: "KampProgramlari",
                columns: new[] { "KurumId", "Yil", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KampProgramiId",
                schema: "dbo",
                table: "KampDonemleri",
                column: "KampProgramiId");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KurumId_KampProgramiId_Ad",
                schema: "dbo",
                table: "KampDonemleri",
                columns: new[] { "KurumId", "KampProgramiId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KurumId_Kod",
                schema: "dbo",
                table: "KampDonemleri",
                columns: new[] { "KurumId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_KampDonemiId",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "KampDonemiId");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_KurumId_BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari",
                columns: new[] { "KurumId", "BasvuruNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_KurumId_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampBasvurulari",
                columns: new[] { "KurumId", "KampDonemiId", "TesisId", "Durum" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_KampBasvurulari_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KampDonemleri_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampDonemleri",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KampProgramlari_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampProgramlari",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KampRezervasyonlari_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampRezervasyonlari",
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
                name: "FK_KampBasvurulari_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropForeignKey(
                name: "FK_KampDonemleri_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropForeignKey(
                name: "FK_KampProgramlari_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampProgramlari");

            migrationBuilder.DropForeignKey(
                name: "FK_KampRezervasyonlari_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropIndex(
                name: "IX_KampRezervasyonlari_KampDonemiId",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropIndex(
                name: "IX_KampRezervasyonlari_KurumId_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropIndex(
                name: "IX_KampRezervasyonlari_KurumId_RezervasyonNo",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropIndex(
                name: "IX_KampProgramlari_KurumId_Yil_Ad",
                schema: "dbo",
                table: "KampProgramlari");

            migrationBuilder.DropIndex(
                name: "IX_KampProgramlari_KurumId_Yil_Kod",
                schema: "dbo",
                table: "KampProgramlari");

            migrationBuilder.DropIndex(
                name: "IX_KampDonemleri_KampProgramiId",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropIndex(
                name: "IX_KampDonemleri_KurumId_KampProgramiId_Ad",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropIndex(
                name: "IX_KampDonemleri_KurumId_Kod",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_KampDonemiId",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_KurumId_BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_KurumId_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "dbo",
                table: "KampRezervasyonlari");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "dbo",
                table: "KampProgramlari");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampRezervasyonlari",
                columns: new[] { "KampDonemiId", "TesisId", "Durum" });

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_RezervasyonNo",
                schema: "dbo",
                table: "KampRezervasyonlari",
                column: "RezervasyonNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramlari_Yil_Ad",
                schema: "dbo",
                table: "KampProgramlari",
                columns: new[] { "Yil", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramlari_Yil_Kod",
                schema: "dbo",
                table: "KampProgramlari",
                columns: new[] { "Yil", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KampProgramiId_Ad",
                schema: "dbo",
                table: "KampDonemleri",
                columns: new[] { "KampProgramiId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_Kod",
                schema: "dbo",
                table: "KampDonemleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "BasvuruNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampBasvurulari",
                columns: new[] { "KampDonemiId", "TesisId", "Durum" },
                filter: "[IsDeleted] = 0");
        }
    }
}
