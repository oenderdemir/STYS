using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260413180000_AddRestoranYoneticisiAtanabilirToRestoranYoneticiGrubu")]
public partial class AddRestoranYoneticisiAtanabilirToRestoranYoneticiGrubu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @RoleId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'KullaniciAtama'
                  AND [Name] = N'RestoranYoneticisiAtanabilir'
                  AND [IsDeleted] = 0
            );

            IF @RoleId IS NULL
            BEGIN
                SET @RoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RoleId, N'RestoranYoneticisiAtanabilir', N'KullaniciAtama', 0, @Now, @Now);
            END;

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT NEWID(), ug.[Id], @RoleId, 0, @Now, @Now, N'migration_restoran_yonetici_marker', N'migration_restoran_yonetici_marker'
            FROM [TODBase].[UserGroups] ug
            WHERE ug.[Name] = N'RestoranYoneticiGrubu'
              AND ug.[IsDeleted] = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [TODBase].[UserGroupRoles] ugr
                  WHERE ugr.[UserGroupId] = ug.[Id]
                    AND ugr.[RoleId] = @RoleId
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // no-op
    }
}

