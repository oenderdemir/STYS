using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260412114000_AddRestaurantManagerAssignmentPermissions")]
public partial class AddRestaurantManagerAssignmentPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @BinaManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222203';
            DECLARE @ResepsiyonistGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222204';

            DECLARE @Domain nvarchar(128) = N'KullaniciAtama';
            DECLARE @AssignableRoleName nvarchar(64) = N'RestoranYoneticisiAtanabilir';
            DECLARE @AssignerRoleName nvarchar(64) = N'RestoranYoneticisiAtayabilir';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = @Domain AND [Name] = @AssignableRoleName)
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @AssignableRoleName, @Domain, 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = @Domain AND [Name] = @AssignerRoleName)
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @AssignerRoleName, @Domain, 0, @Now, @Now);

            DECLARE @AssignableRoleId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = @Domain
                  AND [Name] = @AssignableRoleName
            );

            DECLARE @AssignerRoleId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = @Domain
                  AND [Name] = @AssignerRoleName
            );

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), g.[GroupId], @AssignableRoleId, 0, @Now, @Now
            FROM (VALUES
                (@AdminGroupId),
                (@TesisManagerGroupId),
                (@BinaManagerGroupId),
                (@ResepsiyonistGroupId)
            ) g([GroupId])
            WHERE EXISTS (SELECT 1 FROM [TODBase].[UserGroups] ug WHERE ug.[Id] = g.[GroupId])
              AND @AssignableRoleId IS NOT NULL
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [TODBase].[UserGroupRoles] ugr
                  WHERE ugr.[UserGroupId] = g.[GroupId]
                    AND ugr.[RoleId] = @AssignableRoleId
              );

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), g.[GroupId], @AssignerRoleId, 0, @Now, @Now
            FROM (VALUES
                (@AdminGroupId),
                (@TesisManagerGroupId)
            ) g([GroupId])
            WHERE EXISTS (SELECT 1 FROM [TODBase].[UserGroups] ug WHERE ug.[Id] = g.[GroupId])
              AND @AssignerRoleId IS NOT NULL
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [TODBase].[UserGroupRoles] ugr
                  WHERE ugr.[UserGroupId] = g.[GroupId]
                    AND ugr.[RoleId] = @AssignerRoleId
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE ugr
            FROM [TODBase].[UserGroupRoles] ugr
            INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
            WHERE r.[Domain] = N'KullaniciAtama'
              AND r.[Name] IN (N'RestoranYoneticisiAtanabilir', N'RestoranYoneticisiAtayabilir');

            DELETE FROM [TODBase].[Roles]
            WHERE [Domain] = N'KullaniciAtama'
              AND [Name] IN (N'RestoranYoneticisiAtanabilir', N'RestoranYoneticisiAtayabilir');
            """);
    }
}

