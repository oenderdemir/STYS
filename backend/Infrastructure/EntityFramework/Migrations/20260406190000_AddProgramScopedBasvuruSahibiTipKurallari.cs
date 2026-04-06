using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260406190000_AddProgramScopedBasvuruSahibiTipKurallari")]
public partial class AddProgramScopedBasvuruSahibiTipKurallari : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "KatilimciBasinaPuan",
            schema: "dbo",
            table: "KampKuralSetleri",
            type: "int",
            nullable: false,
            defaultValue: 10);

        migrationBuilder.CreateTable(
            name: "KampProgramiBasvuruSahibiTipKurallari",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                KampProgramiId = table.Column<int>(type: "int", nullable: false),
                KampBasvuruSahibiTipiId = table.Column<int>(type: "int", nullable: false),
                OncelikSirasi = table.Column<int>(type: "int", nullable: false),
                TabanPuan = table.Column<int>(type: "int", nullable: false),
                HizmetYiliPuaniAktifMi = table.Column<bool>(type: "bit", nullable: false),
                EmekliBonusPuani = table.Column<int>(type: "int", nullable: false),
                VarsayilanKatilimciTipiKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                table.PrimaryKey("PK_KampProgramiBasvuruSahibiTipKurallari", x => x.Id);
                table.ForeignKey(
                    name: "FK_KampProgramiBasvuruSahibiTipKurallari_KampBasvuruSahibiTipleri_KampBasvuruSahibiTipiId",
                    column: x => x.KampBasvuruSahibiTipiId,
                    principalSchema: "dbo",
                    principalTable: "KampBasvuruSahibiTipleri",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_KampProgramiBasvuruSahibiTipKurallari_KampProgramlari_KampProgramiId",
                    column: x => x.KampProgramiId,
                    principalSchema: "dbo",
                    principalTable: "KampProgramlari",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_KampProgramiBasvuruSahibiTipKurallari_KampBasvuruSahibiTipiId",
            schema: "dbo",
            table: "KampProgramiBasvuruSahibiTipKurallari",
            column: "KampBasvuruSahibiTipiId");

        migrationBuilder.CreateIndex(
            name: "IX_KampProgramiBasvuruSahibiTipKurallari_KampProgramiId_KampBasvuruSahibiTipiId",
            schema: "dbo",
            table: "KampProgramiBasvuruSahibiTipKurallari",
            columns: new[] { "KampProgramiId", "KampBasvuruSahibiTipiId" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.Sql(
            """
            INSERT INTO [dbo].[KampProgramiBasvuruSahibiTipKurallari]
                ([KampProgramiId], [KampBasvuruSahibiTipiId], [OncelikSirasi], [TabanPuan], [HizmetYiliPuaniAktifMi], [EmekliBonusPuani], [VarsayilanKatilimciTipiKodu], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT
                p.[Id],
                t.[Id],
                t.[OncelikSirasi],
                t.[TabanPuan],
                t.[HizmetYiliPuaniAktifMi],
                t.[EmekliBonusPuani],
                t.[VarsayilanKatilimciTipiKodu],
                t.[AktifMi],
                0,
                SYSUTCDATETIME(),
                SYSUTCDATETIME(),
                N'migration',
                N'migration'
            FROM [dbo].[KampProgramlari] p
            CROSS JOIN [dbo].[KampBasvuruSahibiTipleri] t
            WHERE p.[IsDeleted] = 0
              AND p.[AktifMi] = 1
              AND t.[IsDeleted] = 0
              AND t.[AktifMi] = 1
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [dbo].[KampProgramiBasvuruSahibiTipKurallari] e
                  WHERE e.[KampProgramiId] = p.[Id]
                    AND e.[KampBasvuruSahibiTipiId] = t.[Id]
                    AND e.[IsDeleted] = 0
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "KampProgramiBasvuruSahibiTipKurallari",
            schema: "dbo");

        migrationBuilder.DropColumn(
            name: "KatilimciBasinaPuan",
            schema: "dbo",
            table: "KampKuralSetleri");
    }
}
