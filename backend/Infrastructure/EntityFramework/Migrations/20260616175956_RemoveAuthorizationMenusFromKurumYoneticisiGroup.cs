using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260616175956_RemoveAuthorizationMenusFromKurumYoneticisiGroup")]
public partial class RemoveAuthorizationMenusFromKurumYoneticisiGroup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AuditTag nvarchar(128) = N'migration_remove_kurum_yonetici_authorization_menus';
            DECLARE @KurumYoneticisiGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222205';

            UPDATE target
            SET [IsDeleted] = 1,
                [DeletedAt] = @Now,
                [UpdatedAt] = @Now,
                [UpdatedBy] = @AuditTag,
                [DeletedBy] = COALESCE(target.[DeletedBy], @AuditTag)
            FROM [TODBase].[UserGroupRoles] target
            INNER JOIN [TODBase].[Roles] r ON r.[Id] = target.[RoleId]
            WHERE target.[UserGroupId] = @KurumYoneticisiGroupId
              AND target.[IsDeleted] = 0
              AND (
                    (r.[Domain] = N'RoleManagement' AND r.[Name] = N'Menu')
                 OR (r.[Domain] = N'UserGroupManagement' AND r.[Name] = N'Menu')
                 OR (r.[Domain] = N'MenuManagement' AND r.[Name] = N'Menu')
                 OR (r.[Domain] = N'LisansYonetimi' AND r.[Name] = N'Menu')
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AuditTag nvarchar(128) = N'migration_remove_kurum_yonetici_authorization_menus';
            DECLARE @KurumYoneticisiGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222205';

            DECLARE @MenuRoles TABLE
            (
                [RoleId] uniqueidentifier NOT NULL PRIMARY KEY
            );

            INSERT INTO @MenuRoles ([RoleId])
            SELECT [Id]
            FROM [TODBase].[Roles]
            WHERE [IsDeleted] = 0
              AND (
                    ([Domain] = N'RoleManagement' AND [Name] = N'Menu')
                 OR ([Domain] = N'UserGroupManagement' AND [Name] = N'Menu')
                 OR ([Domain] = N'MenuManagement' AND [Name] = N'Menu')
                 OR ([Domain] = N'LisansYonetimi' AND [Name] = N'Menu')
              );

            UPDATE target
            SET [IsDeleted] = 0,
                [DeletedAt] = NULL,
                [DeletedBy] = NULL,
                [UpdatedAt] = @Now,
                [UpdatedBy] = @AuditTag
            FROM [TODBase].[UserGroupRoles] target
            INNER JOIN @MenuRoles source ON source.[RoleId] = target.[RoleId]
            WHERE target.[UserGroupId] = @KurumYoneticisiGroupId;

            INSERT INTO [TODBase].[UserGroupRoles]
            (
                [Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy]
            )
            SELECT
                NEWID(),
                @KurumYoneticisiGroupId,
                source.[RoleId],
                0,
                @Now,
                @Now,
                NULL,
                @AuditTag,
                @AuditTag,
                NULL
            FROM @MenuRoles source
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[UserGroupRoles] target
                WHERE target.[UserGroupId] = @KurumYoneticisiGroupId
                  AND target.[RoleId] = source.[RoleId]
            );
            """);
    }
}
