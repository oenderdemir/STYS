using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260408120000_AddKampTarifeleriMenuToKampYonetimi")]
public partial class AddKampTarifeleriMenuToKampYonetimi : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';

            DECLARE @KampParentMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d101';
            DECLARE @KampTarifeleriMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d141';

            DECLARE @TarifeleriMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d241';
            DECLARE @TarifeleriViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d242';
            DECLARE @TarifeleriManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d243';

            -- Create roles if not exists
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampTarifeYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TarifeleriMenuRoleId, N'Menu', N'KampTarifeYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampTarifeYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TarifeleriViewRoleId, N'View', N'KampTarifeYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampTarifeYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TarifeleriManageRoleId, N'Manage', N'KampTarifeYonetimi', 0, @Now, @Now);

            -- Assign roles to admin group
            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TarifeleriMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d351', @AdminGroupId, @TarifeleriMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TarifeleriViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d352', @AdminGroupId, @TarifeleriViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TarifeleriManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d353', @AdminGroupId, @TarifeleriManageRoleId, 0, @Now, @Now);
            END

            -- Create menu item under Kamp Yönetimi
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampTarifeleriMenuId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@KampTarifeleriMenuId, N'Tarifeler', N'fa-solid fa-tags', N'kamp-tarifeleri', NULL, @KampParentMenuId, 3, 0, @Now, @Now);
            ELSE
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Tarifeler', [Icon] = N'fa-solid fa-tags', [Route] = N'kamp-tarifeleri', [ParentId] = @KampParentMenuId, [MenuOrder] = 3, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @KampTarifeleriMenuId;

            -- Create menu item role relationship
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KampTarifeleriMenuId AND [RoleId] = @TarifeleriMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d441', @KampTarifeleriMenuId, @TarifeleriMenuRoleId, 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @KampTarifeleriMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d141';
            DECLARE @TarifeleriMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d241';
            DECLARE @TarifeleriViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d242';
            DECLARE @TarifeleriManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d243';

            DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KampTarifeleriMenuId;
            DELETE FROM [TODBase].[MenuItems] WHERE [Id] = @KampTarifeleriMenuId;
            DELETE FROM [TODBase].[UserGroupRoles] WHERE [RoleId] IN (@TarifeleriMenuRoleId, @TarifeleriViewRoleId, @TarifeleriManageRoleId);
            DELETE FROM [TODBase].[Roles] WHERE [Id] IN (@TarifeleriMenuRoleId, @TarifeleriViewRoleId, @TarifeleriManageRoleId);
            """);
    }
}
