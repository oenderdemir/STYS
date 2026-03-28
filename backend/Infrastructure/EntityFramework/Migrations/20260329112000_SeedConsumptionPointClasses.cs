using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260329112000_SeedConsumptionPointClasses")]
public class SeedConsumptionPointClasses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlaniSiniflari] WHERE [Kod] = N'BAR')
                INSERT INTO [dbo].[IsletmeAlaniSiniflari] ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy])
                VALUES (N'BAR', N'Bar', 1, 0, SYSUTCDATETIME(), N'system', SYSUTCDATETIME(), N'system');

            IF NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlaniSiniflari] WHERE [Kod] = N'ODA_SERVISI')
                INSERT INTO [dbo].[IsletmeAlaniSiniflari] ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy])
                VALUES (N'ODA_SERVISI', N'Oda Servisi', 1, 0, SYSUTCDATETIME(), N'system', SYSUTCDATETIME(), N'system');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM [dbo].[IsletmeAlaniSiniflari]
            WHERE [Kod] IN (N'BAR', N'ODA_SERVISI')
              AND NOT EXISTS (
                  SELECT 1
                  FROM [dbo].[IsletmeAlanlari] IA
                  WHERE IA.[IsletmeAlaniSinifiId] = [dbo].[IsletmeAlaniSiniflari].[Id]
              );
            """);
    }
}
