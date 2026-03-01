using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260301212000_SeedUserGroupManagementPermissions")]
    public partial class SeedUserGroupManagementPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = NULL;

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Name] = N'Yönetici Grubu')
                    SELECT @AdminGroupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'Yönetici Grubu';

                DECLARE @UserGroupManagementView uniqueidentifier = NEWID();
                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'UserGroupManagement' AND [Name] = 'View')
                    SELECT @UserGroupManagementView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserGroupManagement' AND [Name] = 'View';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@UserGroupManagementView, 'View', 'UserGroupManagement', 0, @Now, @Now);

                DECLARE @UserGroupManagementManage uniqueidentifier = NEWID();
                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'UserGroupManagement' AND [Name] = 'Manage')
                    SELECT @UserGroupManagementManage = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserGroupManagement' AND [Name] = 'Manage';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@UserGroupManagementManage, 'Manage', 'UserGroupManagement', 0, @Now, @Now);

                IF @AdminGroupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @UserGroupManagementView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @AdminGroupId, @UserGroupManagementView, 0, @Now, @Now);

                IF @AdminGroupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @UserGroupManagementManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @AdminGroupId, @UserGroupManagementManage, 0, @Now, @Now);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @AdminGroupId uniqueidentifier = NULL;
                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Name] = N'Yönetici Grubu')
                    SELECT @AdminGroupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'Yönetici Grubu';

                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE r.[Domain] = 'UserGroupManagement'
                  AND r.[Name] IN ('View', 'Manage')
                  AND (@AdminGroupId IS NULL OR ugr.[UserGroupId] = @AdminGroupId);

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] = 'UserGroupManagement'
                  AND [Name] IN ('View', 'Manage')
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [RoleId] = [TODBase].[Roles].[Id])
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [RoleId] = [TODBase].[Roles].[Id]);
                """);
        }
    }
}
