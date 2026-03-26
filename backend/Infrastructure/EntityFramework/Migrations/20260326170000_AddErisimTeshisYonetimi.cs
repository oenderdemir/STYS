using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260326170000_AddErisimTeshisYonetimi")]
    public partial class AddErisimTeshisYonetimi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';

                DECLARE @MenuRoleId uniqueidentifier = '2f86f8f6-04ec-44e6-a41b-50d0f2e66001';
                DECLARE @ViewRoleId uniqueidentifier = '2f86f8f6-04ec-44e6-a41b-50d0f2e66002';
                DECLARE @ManageRoleId uniqueidentifier = '2f86f8f6-04ec-44e6-a41b-50d0f2e66003';
                DECLARE @MenuItemId uniqueidentifier = '2f86f8f6-04ec-44e6-a41b-50d0f2e66004';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @MenuRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuRoleId, N'Menu', N'ErisimTeshisYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ViewRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ViewRoleId, N'View', N'ErisimTeshisYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ManageRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ManageRoleId, N'Manage', N'ErisimTeshisYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('2f86f8f6-04ec-44e6-a41b-50d0f2e66011', @AdminGroupId, @MenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('2f86f8f6-04ec-44e6-a41b-50d0f2e66012', @AdminGroupId, @ViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('2f86f8f6-04ec-44e6-a41b-50d0f2e66013', @AdminGroupId, @ManageRoleId, 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MenuItemId)
                        INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (@MenuItemId, N'Erisim Teshis', N'fa-solid fa-shield-halved', N'erisim-teshis', NULL, @MainMenuId, 25, 0, @Now, @Now);
                END

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES ('2f86f8f6-04ec-44e6-a41b-50d0f2e66021', @MenuItemId, @MenuRoleId, 0, @Now, @Now);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [TODBase].[MenuItemRoles]
                WHERE [Id] = '2f86f8f6-04ec-44e6-a41b-50d0f2e66021';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Id] = '2f86f8f6-04ec-44e6-a41b-50d0f2e66004';

                DELETE FROM [TODBase].[UserGroupRoles]
                WHERE [Id] IN (
                    '2f86f8f6-04ec-44e6-a41b-50d0f2e66011',
                    '2f86f8f6-04ec-44e6-a41b-50d0f2e66012',
                    '2f86f8f6-04ec-44e6-a41b-50d0f2e66013'
                );

                DELETE FROM [TODBase].[Roles]
                WHERE [Id] IN (
                    '2f86f8f6-04ec-44e6-a41b-50d0f2e66001',
                    '2f86f8f6-04ec-44e6-a41b-50d0f2e66002',
                    '2f86f8f6-04ec-44e6-a41b-50d0f2e66003'
                );
                """);
        }
    }
}
