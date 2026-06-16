using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260616192000_SeedTrabzonIl")]
public partial class SeedTrabzonIl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AuditTag nvarchar(128) = N'migration_seed_trabzon_il';

            IF EXISTS (SELECT 1 FROM [dbo].[Iller] WHERE [Ad] = N'Trabzon')
            BEGIN
                UPDATE [dbo].[Iller]
                SET [AktifMi] = 1,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now,
                    [UpdatedBy] = @AuditTag
                WHERE [Ad] = N'Trabzon';
            END
            ELSE
            BEGIN
                INSERT INTO [dbo].[Iller]
                    ([Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                VALUES
                    (N'Trabzon', 1, 0, @Now, @Now, NULL, @AuditTag, @AuditTag, NULL);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @AuditTag nvarchar(128) = N'migration_seed_trabzon_il';

            DELETE i
            FROM [dbo].[Iller] i
            WHERE i.[Ad] = N'Trabzon'
              AND i.[CreatedBy] = @AuditTag
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [dbo].[Tesisler] t
                  WHERE t.[IlId] = i.[Id]
              );
            """);
    }
}
