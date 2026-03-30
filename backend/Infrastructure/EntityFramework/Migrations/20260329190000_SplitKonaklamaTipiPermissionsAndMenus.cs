using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260329190000_SplitKonaklamaTipiPermissionsAndMenus")]
public partial class SplitKonaklamaTipiPermissionsAndMenus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';

            DECLARE @OldKonaklamaMenuId uniqueidentifier = '8dad95a9-f0f9-4341-a2f7-6d02009de8ec';
            DECLARE @TanimMenuRoleId uniqueidentifier = '8e3c64e2-4a7c-4df9-b17d-a9757bc72f01';
            DECLARE @TanimViewRoleId uniqueidentifier = '0d5f1bcc-0b56-48cd-a7e6-5ea3f5662e0b';
            DECLARE @TanimManageRoleId uniqueidentifier = '690a86cf-9510-4e8a-8de2-8d6f4a6d8b11';
            DECLARE @AtamaMenuRoleId uniqueidentifier = '9ddad2f8-f242-4d3e-b361-b4959080f3e4';
            DECLARE @AtamaViewRoleId uniqueidentifier = '9d3a3284-b11e-466f-8589-5c9d12304f4f';
            DECLARE @AtamaManageRoleId uniqueidentifier = 'f9f30f94-cfcf-4ef6-ae08-5846718b7f87';

            DECLARE @TanimMenuId uniqueidentifier = 'f778fcb3-f238-43d3-9f2a-86ba9bc2ab31';
            DECLARE @AtamaMenuId uniqueidentifier = '72fe4259-64f8-4415-9ebc-bf6d91276513';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KonaklamaTipiTanimYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TanimMenuRoleId, N'Menu', N'KonaklamaTipiTanimYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KonaklamaTipiTanimYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TanimViewRoleId, N'View', N'KonaklamaTipiTanimYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KonaklamaTipiTanimYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TanimManageRoleId, N'Manage', N'KonaklamaTipiTanimYonetimi', 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KonaklamaTipiTesisAtamaYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaMenuRoleId, N'Menu', N'KonaklamaTipiTesisAtamaYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KonaklamaTipiTesisAtamaYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaViewRoleId, N'View', N'KonaklamaTipiTesisAtamaYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KonaklamaTipiTesisAtamaYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaManageRoleId, N'Manage', N'KonaklamaTipiTesisAtamaYonetimi', 0, @Now, @Now);

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TanimMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('278e8363-a5c2-47f4-9e58-e8d0515fdbd0', @AdminGroupId, @TanimMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TanimViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('928e0a61-6e22-426b-a460-d8ef539b34d8', @AdminGroupId, @TanimViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TanimManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('481ce8f1-18c8-4582-a95d-73af63949d3a', @AdminGroupId, @TanimManageRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AtamaMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('ced9f4f9-f0db-46d3-bbe1-db4126f1652a', @AdminGroupId, @AtamaMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AtamaViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('ab987cf1-6f1e-4032-9caa-d238bbd53ec5', @AdminGroupId, @AtamaViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AtamaManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('342ff0f3-1796-4f98-a551-d9d9601243b3', @AdminGroupId, @AtamaManageRoleId, 0, @Now, @Now);
            END

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @AtamaMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('8e9fb3bd-01db-4a17-8832-c66ec3a603e1', @TesisManagerGroupId, @AtamaMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @AtamaViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('0d84af31-9917-4201-a2dc-cfc94768febd', @TesisManagerGroupId, @AtamaViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @AtamaManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('2cca35c5-ecdb-4386-8131-80289fd6d367', @TesisManagerGroupId, @AtamaManageRoleId, 0, @Now, @Now);
            END

            IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @TanimMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TanimMenuId, N'Konaklama Tipi Tanimlari', N'fa-solid fa-bed', N'konaklama-tipi-tanimlari', NULL, @MainMenuId, 18, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @AtamaMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaMenuId, N'Konaklama Tipi Atamalari', N'fa-solid fa-hotel', N'konaklama-tipi-atamalari', NULL, @MainMenuId, 19, 0, @Now, @Now);
            END

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @TanimMenuId AND [RoleId] = @TanimMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('34b3cdb0-6f52-4937-a6a7-bbdf97b18f4a', @TanimMenuId, @TanimMenuRoleId, 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @AtamaMenuId AND [RoleId] = @AtamaMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('ff487fd1-41bc-4c34-ab4a-0e1716ddfaf5', @AtamaMenuId, @AtamaMenuRoleId, 0, @Now, @Now);

            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 1,
                [DeletedAt] = COALESCE([DeletedAt], @Now),
                [UpdatedAt] = @Now
            WHERE [Id] = @OldKonaklamaMenuId
              AND [IsDeleted] = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @OldKonaklamaMenuId uniqueidentifier = '8dad95a9-f0f9-4341-a2f7-6d02009de8ec';
            DECLARE @TanimMenuRoleId uniqueidentifier = '8e3c64e2-4a7c-4df9-b17d-a9757bc72f01';
            DECLARE @TanimViewRoleId uniqueidentifier = '0d5f1bcc-0b56-48cd-a7e6-5ea3f5662e0b';
            DECLARE @TanimManageRoleId uniqueidentifier = '690a86cf-9510-4e8a-8de2-8d6f4a6d8b11';
            DECLARE @AtamaMenuRoleId uniqueidentifier = '9ddad2f8-f242-4d3e-b361-b4959080f3e4';
            DECLARE @AtamaViewRoleId uniqueidentifier = '9d3a3284-b11e-466f-8589-5c9d12304f4f';
            DECLARE @AtamaManageRoleId uniqueidentifier = 'f9f30f94-cfcf-4ef6-ae08-5846718b7f87';
            DECLARE @TanimMenuId uniqueidentifier = 'f778fcb3-f238-43d3-9f2a-86ba9bc2ab31';
            DECLARE @AtamaMenuId uniqueidentifier = '72fe4259-64f8-4415-9ebc-bf6d91276513';

            DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] IN (@TanimMenuId, @AtamaMenuId);
            DELETE FROM [TODBase].[MenuItems] WHERE [Id] IN (@TanimMenuId, @AtamaMenuId);
            DELETE FROM [TODBase].[UserGroupRoles] WHERE [RoleId] IN (@TanimMenuRoleId, @TanimViewRoleId, @TanimManageRoleId, @AtamaMenuRoleId, @AtamaViewRoleId, @AtamaManageRoleId);
            DELETE FROM [TODBase].[Roles] WHERE [Id] IN (@TanimMenuRoleId, @TanimViewRoleId, @TanimManageRoleId, @AtamaMenuRoleId, @AtamaViewRoleId, @AtamaManageRoleId);

            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 0,
                [DeletedAt] = NULL,
                [UpdatedAt] = @Now
            WHERE [Id] = @OldKonaklamaMenuId;
            """);
    }
}
