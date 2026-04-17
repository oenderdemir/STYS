using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260417180000_AddLisansYonetimiMenuAndPermissions")]
    public partial class AddLisansYonetimiMenuAndPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';

                DECLARE @MenuRoleId uniqueidentifier = 'b1a2c3d4-e5f6-7890-abcd-ef0123456701';
                DECLARE @ViewRoleId uniqueidentifier = 'b1a2c3d4-e5f6-7890-abcd-ef0123456702';
                DECLARE @ManageRoleId uniqueidentifier = 'b1a2c3d4-e5f6-7890-abcd-ef0123456703';
                DECLARE @MenuItemId uniqueidentifier = 'b1a2c3d4-e5f6-7890-abcd-ef0123456704';

                -- Roller
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @MenuRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuRoleId, N'Menu', N'LisansYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ViewRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ViewRoleId, N'View', N'LisansYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ManageRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ManageRoleId, N'Manage', N'LisansYonetimi', 0, @Now, @Now);

                -- Admin grubuna rolleri ata
                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('b1a2c3d4-e5f6-7890-abcd-ef0123456711', @AdminGroupId, @MenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('b1a2c3d4-e5f6-7890-abcd-ef0123456712', @AdminGroupId, @ViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('b1a2c3d4-e5f6-7890-abcd-ef0123456713', @AdminGroupId, @ManageRoleId, 0, @Now, @Now);
                END

                -- Menu item
                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MenuItemId)
                        INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (@MenuItemId, N'Lisans Yonetimi', N'fa-solid fa-key', N'lisans-yonetimi', NULL, @MainMenuId, 99, 0, @Now, @Now);
                END

                -- Menu item role baglantisi
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES ('b1a2c3d4-e5f6-7890-abcd-ef0123456721', @MenuItemId, @MenuRoleId, 0, @Now, @Now);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [TODBase].[MenuItemRoles]
                WHERE [Id] = 'b1a2c3d4-e5f6-7890-abcd-ef0123456721';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Id] = 'b1a2c3d4-e5f6-7890-abcd-ef0123456704';

                DELETE FROM [TODBase].[UserGroupRoles]
                WHERE [Id] IN (
                    'b1a2c3d4-e5f6-7890-abcd-ef0123456711',
                    'b1a2c3d4-e5f6-7890-abcd-ef0123456712',
                    'b1a2c3d4-e5f6-7890-abcd-ef0123456713'
                );

                DELETE FROM [TODBase].[Roles]
                WHERE [Id] IN (
                    'b1a2c3d4-e5f6-7890-abcd-ef0123456701',
                    'b1a2c3d4-e5f6-7890-abcd-ef0123456702',
                    'b1a2c3d4-e5f6-7890-abcd-ef0123456703'
                );
                """);
        }
    }
}
