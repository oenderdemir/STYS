using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKampProgramiParametreAyarlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KampProgramiParametreAyarlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampProgramiId = table.Column<int>(type: "int", nullable: false),
                    KamuAvansKisiBasi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DigerAvansKisiBasi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    VazgecmeIadeGunSayisi = table.Column<int>(type: "int", nullable: true),
                    GecBildirimGunlukKesintiyUzdesi = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    NoShowSuresiGun = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_KampProgramiParametreAyarlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampProgramiParametreAyarlari_KampProgramlari_KampProgramiId",
                        column: x => x.KampProgramiId,
                        principalSchema: "dbo",
                        principalTable: "KampProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramiParametreAyarlari_KampProgramiId",
                schema: "dbo",
                table: "KampProgramiParametreAyarlari",
                column: "KampProgramiId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                DECLARE @KamuAvans decimal(18,2) =
                    TRY_CONVERT(decimal(18,2), (SELECT TOP 1 [Deger] FROM [dbo].[KampParametreleri] WHERE [IsDeleted] = 0 AND [Kod] = N'KamuAvansKisiBasi'));
                DECLARE @DigerAvans decimal(18,2) =
                    TRY_CONVERT(decimal(18,2), (SELECT TOP 1 [Deger] FROM [dbo].[KampParametreleri] WHERE [IsDeleted] = 0 AND [Kod] = N'DigerAvansKisiBasi'));
                DECLARE @VazgecmeGun int =
                    TRY_CONVERT(int, (SELECT TOP 1 [Deger] FROM [dbo].[KampParametreleri] WHERE [IsDeleted] = 0 AND [Kod] = N'VazgecmeIadeGunSayisi'));
                DECLARE @GecKesinti decimal(18,4) =
                    TRY_CONVERT(decimal(18,4), (SELECT TOP 1 [Deger] FROM [dbo].[KampParametreleri] WHERE [IsDeleted] = 0 AND [Kod] = N'GecBildirimGunlukKesintiyUzdesi'));
                DECLARE @NoShowGun int =
                    TRY_CONVERT(int, (SELECT TOP 1 [Deger] FROM [dbo].[KampParametreleri] WHERE [IsDeleted] = 0 AND [Kod] = N'NoShowSuresiGun'));

                INSERT INTO [dbo].[KampProgramiParametreAyarlari]
                (
                    [KampProgramiId], [KamuAvansKisiBasi], [DigerAvansKisiBasi], [VazgecmeIadeGunSayisi], [GecBildirimGunlukKesintiyUzdesi], [NoShowSuresiGun],
                    [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                )
                SELECT
                    kp.[Id], @KamuAvans, @DigerAvans, @VazgecmeGun, @GecKesinti, @NoShowGun,
                    1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'system', N'system'
                FROM [dbo].[KampProgramlari] kp
                WHERE kp.[IsDeleted] = 0 AND kp.[AktifMi] = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[KampProgramiParametreAyarlari] p
                      WHERE p.[IsDeleted] = 0 AND p.[KampProgramiId] = kp.[Id]
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KampProgramiParametreAyarlari",
                schema: "dbo");
        }
    }
}
