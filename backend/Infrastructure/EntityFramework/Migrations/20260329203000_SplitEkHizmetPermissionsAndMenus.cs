using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260329203000_SplitEkHizmetPermissionsAndMenus")]
public partial class SplitEkHizmetPermissionsAndMenus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';

            DECLARE @OldMenuId uniqueidentifier = 'd5a6b6de-d0db-4e78-82f0-7a98cf279924';

            DECLARE @TanimMenuRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a101';
            DECLARE @TanimViewRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a102';
            DECLARE @TanimManageRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a103';

            DECLARE @AtamaMenuRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a201';
            DECLARE @AtamaViewRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a202';
            DECLARE @AtamaManageRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a203';

            DECLARE @TarifeMenuRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a301';
            DECLARE @TarifeViewRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a302';
            DECLARE @TarifeManageRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a303';

            DECLARE @TanimMenuId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64b101';
            DECLARE @AtamaMenuId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64b201';
            DECLARE @TarifeMenuId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64b301';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'EkHizmetTanimYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TanimMenuRoleId, N'Menu', N'EkHizmetTanimYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'EkHizmetTanimYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TanimViewRoleId, N'View', N'EkHizmetTanimYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'EkHizmetTanimYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TanimManageRoleId, N'Manage', N'EkHizmetTanimYonetimi', 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'EkHizmetTesisAtamaYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaMenuRoleId, N'Menu', N'EkHizmetTesisAtamaYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'EkHizmetTesisAtamaYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaViewRoleId, N'View', N'EkHizmetTesisAtamaYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'EkHizmetTesisAtamaYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaManageRoleId, N'Manage', N'EkHizmetTesisAtamaYonetimi', 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'EkHizmetTarifeYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TarifeMenuRoleId, N'Menu', N'EkHizmetTarifeYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'EkHizmetTarifeYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TarifeViewRoleId, N'View', N'EkHizmetTarifeYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'EkHizmetTarifeYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TarifeManageRoleId, N'Manage', N'EkHizmetTarifeYonetimi', 0, @Now, @Now);

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TanimMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64c101', @AdminGroupId, @TanimMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TanimViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64c102', @AdminGroupId, @TanimViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TanimManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64c103', @AdminGroupId, @TanimManageRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AtamaMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64c201', @AdminGroupId, @AtamaMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AtamaViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64c202', @AdminGroupId, @AtamaViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AtamaManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64c203', @AdminGroupId, @AtamaManageRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TarifeMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64c301', @AdminGroupId, @TarifeMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TarifeViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64c302', @AdminGroupId, @TarifeViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TarifeManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64c303', @AdminGroupId, @TarifeManageRoleId, 0, @Now, @Now);
            END

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @AtamaMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64d201', @TesisManagerGroupId, @AtamaMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @AtamaViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64d202', @TesisManagerGroupId, @AtamaViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @AtamaManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64d203', @TesisManagerGroupId, @AtamaManageRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @TarifeMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64d301', @TesisManagerGroupId, @TarifeMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @TarifeViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64d302', @TesisManagerGroupId, @TarifeViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @TarifeManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64d303', @TesisManagerGroupId, @TarifeManageRoleId, 0, @Now, @Now);
            END

            IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @TanimMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TanimMenuId, N'Ek Hizmet Tanimlari', N'fa-solid fa-globe', N'ek-hizmet-tanimlari', NULL, @MainMenuId, 24, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @AtamaMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaMenuId, N'Ek Hizmet Atamalari', N'fa-solid fa-sitemap', N'ek-hizmet-atamalari', NULL, @MainMenuId, 25, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @TarifeMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TarifeMenuId, N'Ek Hizmet Tarifeleri', N'fa-solid fa-money-bill', N'ek-hizmet-tarifeleri', NULL, @MainMenuId, 26, 0, @Now, @Now);
            END

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @TanimMenuId AND [RoleId] = @TanimMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64e101', @TanimMenuId, @TanimMenuRoleId, 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @AtamaMenuId AND [RoleId] = @AtamaMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64e201', @AtamaMenuId, @AtamaMenuRoleId, 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @TarifeMenuId AND [RoleId] = @TarifeMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0b84f211-1b01-4055-b25e-084e1f64e301', @TarifeMenuId, @TarifeMenuRoleId, 0, @Now, @Now);

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
            DECLARE @OldMenuId uniqueidentifier = 'd5a6b6de-d0db-4e78-82f0-7a98cf279924';
            DECLARE @TanimMenuRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a101';
            DECLARE @TanimViewRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a102';
            DECLARE @TanimManageRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a103';
            DECLARE @AtamaMenuRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a201';
            DECLARE @AtamaViewRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a202';
            DECLARE @AtamaManageRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a203';
            DECLARE @TarifeMenuRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a301';
            DECLARE @TarifeViewRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a302';
            DECLARE @TarifeManageRoleId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64a303';
            DECLARE @TanimMenuId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64b101';
            DECLARE @AtamaMenuId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64b201';
            DECLARE @TarifeMenuId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64b301';

            DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] IN (@TanimMenuId, @AtamaMenuId, @TarifeMenuId);
            DELETE FROM [TODBase].[MenuItems] WHERE [Id] IN (@TanimMenuId, @AtamaMenuId, @TarifeMenuId);
            DELETE FROM [TODBase].[UserGroupRoles] WHERE [RoleId] IN (@TanimMenuRoleId, @TanimViewRoleId, @TanimManageRoleId, @AtamaMenuRoleId, @AtamaViewRoleId, @AtamaManageRoleId, @TarifeMenuRoleId, @TarifeViewRoleId, @TarifeManageRoleId);
            DELETE FROM [TODBase].[Roles] WHERE [Id] IN (@TanimMenuRoleId, @TanimViewRoleId, @TanimManageRoleId, @AtamaMenuRoleId, @AtamaViewRoleId, @AtamaManageRoleId, @TarifeMenuRoleId, @TarifeViewRoleId, @TarifeManageRoleId);

            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 0,
                [DeletedAt] = NULL,
                [UpdatedAt] = @Now
            WHERE [Id] = @OldMenuId;
            """);
    }
}
