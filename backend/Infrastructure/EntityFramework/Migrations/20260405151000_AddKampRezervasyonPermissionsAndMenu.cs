using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260405151000_AddKampRezervasyonPermissionsAndMenu")]
public partial class AddKampRezervasyonPermissionsAndMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @KampParentMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d101';
            DECLARE @KampRezervasyonMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d151';

            DECLARE @RezMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d251';
            DECLARE @RezViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d252';
            DECLARE @RezManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d253';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampRezervasyonYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@RezMenuRoleId, N'Menu', N'KampRezervasyonYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampRezervasyonYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@RezViewRoleId, N'View', N'KampRezervasyonYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampRezervasyonYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@RezManageRoleId, N'Manage', N'KampRezervasyonYonetimi', 0, @Now, @Now);

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @RezMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8e351', @AdminGroupId, @RezMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @RezViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8e352', @AdminGroupId, @RezViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @RezManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8e353', @AdminGroupId, @RezManageRoleId, 0, @Now, @Now);
            END

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @RezMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8e361', @TesisManagerGroupId, @RezMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @RezViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8e362', @TesisManagerGroupId, @RezViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @RezManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8e363', @TesisManagerGroupId, @RezManageRoleId, 0, @Now, @Now);
            END

            IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampParentMenuId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampRezervasyonMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KampRezervasyonMenuId, N'Rezervasyonlar', N'fa-solid fa-calendar-check', N'kamp-rezervasyonlari', NULL, @KampParentMenuId, 4, 0, @Now, @Now);
                ELSE
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Rezervasyonlar', [Icon] = N'fa-solid fa-calendar-check', [Route] = N'kamp-rezervasyonlari', [ParentId] = @KampParentMenuId, [MenuOrder] = 4, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                    WHERE [Id] = @KampRezervasyonMenuId;
            END

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KampRezervasyonMenuId AND [RoleId] = @RezMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8e451', @KampRezervasyonMenuId, @RezMenuRoleId, 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @KampRezervasyonMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d151';
            DECLARE @RezMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d251';
            DECLARE @RezViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d252';
            DECLARE @RezManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d253';

            DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KampRezervasyonMenuId;
            DELETE FROM [TODBase].[MenuItems] WHERE [Id] = @KampRezervasyonMenuId;
            DELETE FROM [TODBase].[UserGroupRoles] WHERE [RoleId] IN (@RezMenuRoleId, @RezViewRoleId, @RezManageRoleId);
            DELETE FROM [TODBase].[Roles] WHERE [Id] IN (@RezMenuRoleId, @RezViewRoleId, @RezManageRoleId);
            """);
    }
}
