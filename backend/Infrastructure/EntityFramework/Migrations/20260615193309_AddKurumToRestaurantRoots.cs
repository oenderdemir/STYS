using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKurumToRestaurantRoots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_RestoranId_SiparisTarihi",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_RestoranMasaId_SiparisDurumu",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_SiparisNo",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_Restoranlar_TesisId_Ad",
                schema: "restoran",
                table: "Restoranlar");

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "restoran",
                table: "RestoranSiparisleri",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "restoran",
                table: "Restoranlar",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [dbo].[Kurumlar] WHERE [Kod] = N'DEFAULT' AND [IsDeleted] = 0)
BEGIN
    INSERT INTO [dbo].[Kurumlar]
    (
        [Kod],
        [Ad],
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

IF @DefaultKurumId IS NULL
BEGIN
    THROW 50000, 'Default kurum bulunamadi.', 1;
END;

UPDATE r
SET [KurumId] = COALESCE(t.[KurumId], @DefaultKurumId)
FROM [restoran].[Restoranlar] r
INNER JOIN [dbo].[Tesisler] t ON t.[Id] = r.[TesisId]
WHERE r.[KurumId] = 0;

UPDATE s
SET [KurumId] = COALESCE(r.[KurumId], @DefaultKurumId)
FROM [restoran].[RestoranSiparisleri] s
INNER JOIN [restoran].[Restoranlar] r ON r.[Id] = s.[RestoranId]
WHERE s.[KurumId] = 0;

UPDATE [restoran].[Restoranlar]
SET [KurumId] = @DefaultKurumId
WHERE [KurumId] = 0;

UPDATE [restoran].[RestoranSiparisleri]
SET [KurumId] = @DefaultKurumId
WHERE [KurumId] = 0;
");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_KurumId_RestoranId_SiparisTarihi",
                schema: "restoran",
                table: "RestoranSiparisleri",
                columns: new[] { "KurumId", "RestoranId", "SiparisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_KurumId_RestoranMasaId_SiparisDurumu",
                schema: "restoran",
                table: "RestoranSiparisleri",
                columns: new[] { "KurumId", "RestoranMasaId", "SiparisDurumu" },
                filter: "[IsDeleted] = 0 AND [RestoranMasaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_KurumId_SiparisNo",
                schema: "restoran",
                table: "RestoranSiparisleri",
                columns: new[] { "KurumId", "SiparisNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_RestoranId",
                schema: "restoran",
                table: "RestoranSiparisleri",
                column: "RestoranId");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_RestoranMasaId",
                schema: "restoran",
                table: "RestoranSiparisleri",
                column: "RestoranMasaId");

            migrationBuilder.CreateIndex(
                name: "IX_Restoranlar_KurumId_TesisId_Ad",
                schema: "restoran",
                table: "Restoranlar",
                columns: new[] { "KurumId", "TesisId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Restoranlar_TesisId",
                schema: "restoran",
                table: "Restoranlar",
                column: "TesisId");

            migrationBuilder.AddForeignKey(
                name: "FK_Restoranlar_Kurumlar_KurumId",
                schema: "restoran",
                table: "Restoranlar",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RestoranSiparisleri_Kurumlar_KurumId",
                schema: "restoran",
                table: "RestoranSiparisleri",
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
                name: "FK_Restoranlar_Kurumlar_KurumId",
                schema: "restoran",
                table: "Restoranlar");

            migrationBuilder.DropForeignKey(
                name: "FK_RestoranSiparisleri_Kurumlar_KurumId",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_KurumId_RestoranId_SiparisTarihi",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_KurumId_RestoranMasaId_SiparisDurumu",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_KurumId_SiparisNo",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_RestoranId",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_RestoranSiparisleri_RestoranMasaId",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropIndex(
                name: "IX_Restoranlar_KurumId_TesisId_Ad",
                schema: "restoran",
                table: "Restoranlar");

            migrationBuilder.DropIndex(
                name: "IX_Restoranlar_TesisId",
                schema: "restoran",
                table: "Restoranlar");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "restoran",
                table: "RestoranSiparisleri");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "restoran",
                table: "Restoranlar");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_RestoranId_SiparisTarihi",
                schema: "restoran",
                table: "RestoranSiparisleri",
                columns: new[] { "RestoranId", "SiparisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_RestoranMasaId_SiparisDurumu",
                schema: "restoran",
                table: "RestoranSiparisleri",
                columns: new[] { "RestoranMasaId", "SiparisDurumu" },
                filter: "[IsDeleted] = 0 AND [RestoranMasaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_SiparisNo",
                schema: "restoran",
                table: "RestoranSiparisleri",
                column: "SiparisNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Restoranlar_TesisId_Ad",
                schema: "restoran",
                table: "Restoranlar",
                columns: new[] { "TesisId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");
        }
    }
}
