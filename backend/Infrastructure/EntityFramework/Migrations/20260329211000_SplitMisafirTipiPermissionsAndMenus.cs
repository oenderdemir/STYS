using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260329211000_SplitMisafirTipiPermissionsAndMenus")]
public partial class SplitMisafirTipiPermissionsAndMenus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
            DECLARE @OldMenuId uniqueidentifier = 'c5f3b680-8903-4392-b4e2-2dd245ff4778';

            DECLARE @TanimMenuRoleId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10011';
            DECLARE @TanimViewRoleId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10012';
            DECLARE @TanimManageRoleId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10013';

            DECLARE @AtamaMenuRoleId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10021';
            DECLARE @AtamaViewRoleId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10022';
            DECLARE @AtamaManageRoleId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10023';

            DECLARE @TanimMenuId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10111';
            DECLARE @AtamaMenuId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10121';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'MisafirTipiTanimYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TanimMenuRoleId, N'Menu', N'MisafirTipiTanimYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'MisafirTipiTanimYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TanimViewRoleId, N'View', N'MisafirTipiTanimYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'MisafirTipiTanimYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TanimManageRoleId, N'Manage', N'MisafirTipiTanimYonetimi', 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'MisafirTipiTesisAtamaYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaMenuRoleId, N'Menu', N'MisafirTipiTesisAtamaYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'MisafirTipiTesisAtamaYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaViewRoleId, N'View', N'MisafirTipiTesisAtamaYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'MisafirTipiTesisAtamaYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaManageRoleId, N'Manage', N'MisafirTipiTesisAtamaYonetimi', 0, @Now, @Now);

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TanimMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('9af5588f-5467-4808-b3c6-fdb3e2f10211', @AdminGroupId, @TanimMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TanimViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('9af5588f-5467-4808-b3c6-fdb3e2f10212', @AdminGroupId, @TanimViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TanimManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('9af5588f-5467-4808-b3c6-fdb3e2f10213', @AdminGroupId, @TanimManageRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AtamaMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('9af5588f-5467-4808-b3c6-fdb3e2f10221', @AdminGroupId, @AtamaMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AtamaViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('9af5588f-5467-4808-b3c6-fdb3e2f10222', @AdminGroupId, @AtamaViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AtamaManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('9af5588f-5467-4808-b3c6-fdb3e2f10223', @AdminGroupId, @AtamaManageRoleId, 0, @Now, @Now);
            END

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @AtamaMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('9af5588f-5467-4808-b3c6-fdb3e2f10321', @TesisManagerGroupId, @AtamaMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @AtamaViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('9af5588f-5467-4808-b3c6-fdb3e2f10322', @TesisManagerGroupId, @AtamaViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @AtamaManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('9af5588f-5467-4808-b3c6-fdb3e2f10323', @TesisManagerGroupId, @AtamaManageRoleId, 0, @Now, @Now);
            END

            IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @TanimMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TanimMenuId, N'Misafir Tipi Tanimlari', N'fa-solid fa-users-gear', N'misafir-tipi-tanimlari', NULL, @MainMenuId, 20, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @AtamaMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaMenuId, N'Misafir Tipi Atamalari', N'fa-solid fa-user-check', N'misafir-tipi-atamalari', NULL, @MainMenuId, 21, 0, @Now, @Now);
            END

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @TanimMenuId AND [RoleId] = @TanimMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('9af5588f-5467-4808-b3c6-fdb3e2f10411', @TanimMenuId, @TanimMenuRoleId, 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @AtamaMenuId AND [RoleId] = @AtamaMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('9af5588f-5467-4808-b3c6-fdb3e2f10421', @AtamaMenuId, @AtamaMenuRoleId, 0, @Now, @Now);

            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 1,
                [DeletedAt] = COALESCE([DeletedAt], @Now),
                [UpdatedAt] = @Now
            WHERE [Id] = @OldMenuId
              AND [IsDeleted] = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @OldMenuId uniqueidentifier = 'c5f3b680-8903-4392-b4e2-2dd245ff4778';
            DECLARE @TanimMenuRoleId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10011';
            DECLARE @TanimViewRoleId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10012';
            DECLARE @TanimManageRoleId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10013';
            DECLARE @AtamaMenuRoleId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10021';
            DECLARE @AtamaViewRoleId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10022';
            DECLARE @AtamaManageRoleId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10023';
            DECLARE @TanimMenuId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10111';
            DECLARE @AtamaMenuId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10121';

            DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] IN (@TanimMenuId, @AtamaMenuId);
            DELETE FROM [TODBase].[MenuItems] WHERE [Id] IN (@TanimMenuId, @AtamaMenuId);
            DELETE FROM [TODBase].[UserGroupRoles] WHERE [RoleId] IN (@TanimMenuRoleId, @TanimViewRoleId, @TanimManageRoleId, @AtamaMenuRoleId, @AtamaViewRoleId, @AtamaManageRoleId);
            DELETE FROM [TODBase].[Roles] WHERE [Id] IN (@TanimMenuRoleId, @TanimViewRoleId, @TanimManageRoleId, @AtamaMenuRoleId, @AtamaViewRoleId, @AtamaManageRoleId);

            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 0,
                [DeletedAt] = NULL,
                [UpdatedAt] = @Now
            WHERE [Id] = @OldMenuId;
            """);
    }
}
