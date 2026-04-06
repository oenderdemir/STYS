using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260405170000_ReorganizeKampMenus")]
public partial class ReorganizeKampMenus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';

            DECLARE @KampParentMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d101';
            DECLARE @KampIadeMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d161';
            DECLARE @KampBasvuruMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d171';
            DECLARE @KampBenimBasvurularimMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d181';

            DECLARE @IadeMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d261';
            DECLARE @IadeViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d262';
            DECLARE @IadeManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d263';

            -- 1. Kamp Yonetimi ana menusunu top-level yap (Isletme altindan cikar)
            UPDATE [TODBase].[MenuItems]
            SET [ParentId] = NULL, [MenuOrder] = 3, [UpdatedAt] = @Now
            WHERE [Id] = @KampParentMenuId;

            -- 2. Iade Yonetimi rolleri
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampIadeYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@IadeMenuRoleId, N'Menu', N'KampIadeYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampIadeYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@IadeViewRoleId, N'View', N'KampIadeYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampIadeYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@IadeManageRoleId, N'Manage', N'KampIadeYonetimi', 0, @Now, @Now);

            -- Admin grubuna iade rolleri
            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @IadeMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d371', @AdminGroupId, @IadeMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @IadeViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d372', @AdminGroupId, @IadeViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @IadeManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d373', @AdminGroupId, @IadeManageRoleId, 0, @Now, @Now);
            END

            -- Tesis yoneticisi grubuna iade rolleri
            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @IadeMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d381', @TesisManagerGroupId, @IadeMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @IadeViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d382', @TesisManagerGroupId, @IadeViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @IadeManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d383', @TesisManagerGroupId, @IadeManageRoleId, 0, @Now, @Now);
            END

            -- 3. Iade Yonetimi menu item
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampIadeMenuId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@KampIadeMenuId, N'İade Yönetimi', N'fa-solid fa-wallet', N'kamp-iade-yonetimi', NULL, @KampParentMenuId, 5, 0, @Now, @Now);
            ELSE
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'İade Yönetimi', [Icon] = N'fa-solid fa-wallet', [Route] = N'kamp-iade-yonetimi', [ParentId] = @KampParentMenuId, [MenuOrder] = 5, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @KampIadeMenuId;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KampIadeMenuId AND [RoleId] = @IadeMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d461', @KampIadeMenuId, @IadeMenuRoleId, 0, @Now, @Now);

            -- 4. Basvuru Yap menu item (herkes gorebilir - rol kisitlamasi yok)
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampBasvuruMenuId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@KampBasvuruMenuId, N'Başvuru Yap', N'fa-solid fa-file-pen', N'kamp-basvurusu', NULL, @KampParentMenuId, 6, 0, @Now, @Now);
            ELSE
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Başvuru Yap', [Icon] = N'fa-solid fa-file-pen', [Route] = N'kamp-basvurusu', [ParentId] = @KampParentMenuId, [MenuOrder] = 6, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @KampBasvuruMenuId;

            -- 5. Basvurularim menu item (herkes gorebilir - rol kisitlamasi yok)
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampBenimBasvurularimMenuId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@KampBenimBasvurularimMenuId, N'Başvurularım', N'fa-solid fa-clipboard-list', N'kamp-basvurularim', NULL, @KampParentMenuId, 7, 0, @Now, @Now);
            ELSE
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Başvurularım', [Icon] = N'fa-solid fa-clipboard-list', [Route] = N'kamp-basvurularim', [ParentId] = @KampParentMenuId, [MenuOrder] = 7, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @KampBenimBasvurularimMenuId;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @IsletmeRootId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2002';
            DECLARE @KampParentMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d101';
            DECLARE @KampIadeMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d161';
            DECLARE @KampBasvuruMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d171';
            DECLARE @KampBenimBasvurularimMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d181';

            DECLARE @IadeMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d261';
            DECLARE @IadeViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d262';
            DECLARE @IadeManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d263';

            -- Kamp Yonetimi'ni Isletme altina geri al
            UPDATE [TODBase].[MenuItems]
            SET [ParentId] = @IsletmeRootId, [MenuOrder] = 9, [UpdatedAt] = @Now
            WHERE [Id] = @KampParentMenuId;

            -- Eklenen menu itemlari sil
            DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] IN (@KampIadeMenuId);
            DELETE FROM [TODBase].[MenuItems] WHERE [Id] IN (@KampIadeMenuId, @KampBasvuruMenuId, @KampBenimBasvurularimMenuId);

            -- Iade rolleri sil
            DELETE FROM [TODBase].[UserGroupRoles] WHERE [RoleId] IN (@IadeMenuRoleId, @IadeViewRoleId, @IadeManageRoleId);
            DELETE FROM [TODBase].[Roles] WHERE [Id] IN (@IadeMenuRoleId, @IadeViewRoleId, @IadeManageRoleId);
            """);
    }
}
