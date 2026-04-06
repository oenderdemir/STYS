using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260406120000_AddKampPuanKuraliPermissionsAndMenu")]
public partial class AddKampPuanKuraliPermissionsAndMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @KampParentMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d101';
            DECLARE @KampPuanKuraliMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d191';

            DECLARE @PuanMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d291';
            DECLARE @PuanViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d292';
            DECLARE @PuanManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d293';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampPuanKuraliYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@PuanMenuRoleId, N'Menu', N'KampPuanKuraliYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampPuanKuraliYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@PuanViewRoleId, N'View', N'KampPuanKuraliYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampPuanKuraliYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@PuanManageRoleId, N'Manage', N'KampPuanKuraliYonetimi', 0, @Now, @Now);

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @PuanMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d391', @AdminGroupId, @PuanMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @PuanViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d392', @AdminGroupId, @PuanViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @PuanManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d393', @AdminGroupId, @PuanManageRoleId, 0, @Now, @Now);
            END

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @PuanMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d394', @TesisManagerGroupId, @PuanMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @PuanViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d395', @TesisManagerGroupId, @PuanViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @PuanManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d396', @TesisManagerGroupId, @PuanManageRoleId, 0, @Now, @Now);
            END

            IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampParentMenuId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampPuanKuraliMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KampPuanKuraliMenuId, N'Puan Kurallari', N'fa-solid fa-sliders', N'kamp-puan-kurallari', NULL, @KampParentMenuId, 8, 0, @Now, @Now);
                ELSE
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Puan Kurallari', [Icon] = N'fa-solid fa-sliders', [Route] = N'kamp-puan-kurallari', [ParentId] = @KampParentMenuId, [MenuOrder] = 8, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                    WHERE [Id] = @KampPuanKuraliMenuId;
            END

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KampPuanKuraliMenuId AND [RoleId] = @PuanMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d491', @KampPuanKuraliMenuId, @PuanMenuRoleId, 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @KampPuanKuraliMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d191';
            DECLARE @PuanMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d291';
            DECLARE @PuanViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d292';
            DECLARE @PuanManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d293';

            DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KampPuanKuraliMenuId;
            DELETE FROM [TODBase].[MenuItems] WHERE [Id] = @KampPuanKuraliMenuId;
            DELETE FROM [TODBase].[UserGroupRoles] WHERE [RoleId] IN (@PuanMenuRoleId, @PuanViewRoleId, @PuanManageRoleId);
            DELETE FROM [TODBase].[Roles] WHERE [Id] IN (@PuanMenuRoleId, @PuanViewRoleId, @PuanManageRoleId);
            """);
    }
}
