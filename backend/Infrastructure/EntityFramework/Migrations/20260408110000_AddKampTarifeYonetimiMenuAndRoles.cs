using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260408110000_AddKampTarifeYonetimiMenuAndRoles")]
public partial class AddKampTarifeYonetimiMenuAndRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';

            DECLARE @MenuRoleId uniqueidentifier = '33333333-3333-3333-3333-333333333901';
            DECLARE @ViewRoleId uniqueidentifier = '33333333-3333-3333-3333-333333333902';
            DECLARE @ManageRoleId uniqueidentifier = '33333333-3333-3333-3333-333333333903';
            DECLARE @MenuItemId uniqueidentifier = '44444444-4444-4444-4444-444444444901';

            -- Create roles
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampTarifeYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@MenuRoleId, N'Menu', N'KampTarifeYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampTarifeYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@ViewRoleId, N'View', N'KampTarifeYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampTarifeYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@ManageRoleId, N'Manage', N'KampTarifeYonetimi', 0, @Now, @Now);

            -- Assign roles to admin group
            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('11111111-1111-1111-1111-111111119001', @AdminGroupId, @MenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('11111111-1111-1111-1111-111111119002', @AdminGroupId, @ViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('11111111-1111-1111-1111-111111119003', @AdminGroupId, @ManageRoleId, 0, @Now, @Now);
            END

            -- Assign roles to tesis manager group (only view)
            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('11111111-1111-1111-1111-111111119004', @TesisManagerGroupId, @MenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('11111111-1111-1111-1111-111111119005', @TesisManagerGroupId, @ViewRoleId, 0, @Now, @Now);
            END

            -- Create menu item
            IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MenuItemId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@MenuItemId, N'Kamp Tarifeler', N'fa-solid fa-tags', N'kamp-tarifeleri', NULL, @MainMenuId, 9, 0, @Now, @Now);
            END

            -- Assign menu to role
            IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MenuItemId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('11111111-1111-1111-1111-111111119101', @MenuItemId, @MenuRoleId, 0, @Now, @Now);
            END
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            -- Cleanup
            DELETE FROM [TODBase].[MenuItemRoles] WHERE [Id] IN ('11111111-1111-1111-1111-111111119101');
            DELETE FROM [TODBase].[MenuItems] WHERE [Id] = '44444444-4444-4444-4444-444444444901';
            DELETE FROM [TODBase].[UserGroupRoles] WHERE [Id] IN ('11111111-1111-1111-1111-111111119001', '11111111-1111-1111-1111-111111119002', '11111111-1111-1111-1111-111111119003', '11111111-1111-1111-1111-111111119004', '11111111-1111-1111-1111-111111119005');
            DELETE FROM [TODBase].[Roles] WHERE [Domain] = N'KampTarifeYonetimi';
            """
        );
    }
}
