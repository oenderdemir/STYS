using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260413143000_FixKampBasvurularimAndRestaurantMenuAuthorizations")]
public partial class FixKampBasvurularimAndRestaurantMenuAuthorizations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @ResepsiyonistGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222204';

            DECLARE @KampBasvurularimMenuItemId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d181';
            DECLARE @KampDonemiAtamaMenuItemId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d131';

            DECLARE @KampBasvuruMenuRoleId uniqueidentifier = '6f5f2a90-79b3-4f3b-b2cf-2d0a00a00101';
            DECLARE @KampBasvuruViewRoleId uniqueidentifier = '6f5f2a90-79b3-4f3b-b2cf-2d0a00a00102';
            DECLARE @KampBasvuruManageRoleId uniqueidentifier = '6f5f2a90-79b3-4f3b-b2cf-2d0a00a00103';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampBasvuruYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@KampBasvuruMenuRoleId, N'Menu', N'KampBasvuruYonetimi', 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampBasvuruYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@KampBasvuruViewRoleId, N'View', N'KampBasvuruYonetimi', 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampBasvuruYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@KampBasvuruManageRoleId, N'Manage', N'KampBasvuruYonetimi', 0, @Now, @Now);

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT NEWID(), x.[UserGroupId], x.[RoleId], 0, @Now, @Now, N'migration_kamp_basvuru_auth', N'migration_kamp_basvuru_auth'
            FROM
            (
                VALUES
                (@AdminGroupId, @KampBasvuruMenuRoleId),
                (@AdminGroupId, @KampBasvuruViewRoleId),
                (@AdminGroupId, @KampBasvuruManageRoleId),
                (@TesisManagerGroupId, @KampBasvuruMenuRoleId),
                (@TesisManagerGroupId, @KampBasvuruViewRoleId),
                (@TesisManagerGroupId, @KampBasvuruManageRoleId),
                (@ResepsiyonistGroupId, @KampBasvuruMenuRoleId),
                (@ResepsiyonistGroupId, @KampBasvuruViewRoleId)
            ) x([UserGroupId], [RoleId])
            WHERE EXISTS (SELECT 1 FROM [TODBase].[UserGroups] ug WHERE ug.[Id] = x.[UserGroupId] AND ug.[IsDeleted] = 0)
              AND EXISTS (SELECT 1 FROM [TODBase].[Roles] r WHERE r.[Id] = x.[RoleId] AND r.[IsDeleted] = 0)
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [TODBase].[UserGroupRoles] ugr
                  WHERE ugr.[UserGroupId] = x.[UserGroupId]
                    AND ugr.[RoleId] = x.[RoleId]
              );

            IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampBasvurularimMenuItemId)
            BEGIN
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM [TODBase].[MenuItemRoles]
                    WHERE [MenuItemId] = @KampBasvurularimMenuItemId
                      AND [RoleId] = @KampBasvuruMenuRoleId
                )
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (NEWID(), @KampBasvurularimMenuItemId, @KampBasvuruMenuRoleId, 0, @Now, @Now, N'migration_kamp_basvuru_auth', N'migration_kamp_basvuru_auth');
            END;

            DECLARE @KampDonemiAtamaMenuRoleId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'KampDonemiTesisAtamaYonetimi'
                  AND [Name] = N'Menu'
            );

            IF @KampDonemiAtamaMenuRoleId IS NOT NULL
               AND EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampDonemiAtamaMenuItemId)
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM [TODBase].[MenuItemRoles]
                   WHERE [MenuItemId] = @KampDonemiAtamaMenuItemId
                     AND [RoleId] = @KampDonemiAtamaMenuRoleId
               )
            BEGIN
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (NEWID(), @KampDonemiAtamaMenuItemId, @KampDonemiAtamaMenuRoleId, 0, @Now, @Now, N'migration_kamp_basvuru_auth', N'migration_kamp_basvuru_auth');
            END;

            DECLARE @RestoranYonetimMenuRoleId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'RestoranYonetimi'
                  AND [Name] = N'Menu'
            );

            DECLARE @RestoranMasaMenuRoleId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'RestoranMasaYonetimi'
                  AND [Name] = N'Menu'
            );

            DECLARE @RestoranMenuMenuRoleId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'RestoranMenuYonetimi'
                  AND [Name] = N'Menu'
            );

            DECLARE @RestoranSiparisMenuRoleId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'RestoranSiparisYonetimi'
                  AND [Name] = N'Menu'
            );

            DECLARE @RestoranRootMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000001';
            DECLARE @RestoranYonetimMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000002';
            DECLARE @RestoranMasaMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000003';
            DECLARE @RestoranMenuYonetimMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000004';
            DECLARE @RestoranKategoriHavuzuMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000005';
            DECLARE @RestoranSiparisYonetimMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000006';
            DECLARE @RestoranGarsonServisMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000007';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @RestoranRootMenuId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@RestoranRootMenuId, N'Restoran', N'pi pi-shop', N'', NULL, NULL, 4, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');
            ELSE
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Restoran', [Icon] = N'pi pi-shop', [Route] = N'', [ParentId] = NULL, [MenuOrder] = 4, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @RestoranRootMenuId;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @RestoranYonetimMenuId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@RestoranYonetimMenuId, N'Restoran Yonetimi', N'pi pi-building', N'restoran-yonetimi', NULL, @RestoranRootMenuId, 0, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @RestoranMasaMenuId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@RestoranMasaMenuId, N'Masa Yonetimi', N'pi pi-table', N'restoran-masa-yonetimi', NULL, @RestoranRootMenuId, 1, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @RestoranMenuYonetimMenuId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@RestoranMenuYonetimMenuId, N'Menu Yonetimi', N'pi pi-list', N'restoran-menu-yonetimi', NULL, @RestoranRootMenuId, 2, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @RestoranKategoriHavuzuMenuId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@RestoranKategoriHavuzuMenuId, N'Kategori Havuzu', N'pi pi-sitemap', N'restoran-kategori-havuzu', NULL, @RestoranRootMenuId, 3, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @RestoranSiparisYonetimMenuId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@RestoranSiparisYonetimMenuId, N'Siparis Yonetimi', N'pi pi-shopping-cart', N'restoran-siparis-yonetimi', NULL, @RestoranRootMenuId, 4, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @RestoranGarsonServisMenuId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@RestoranGarsonServisMenuId, N'Garson Servis', N'pi pi-bolt', N'garson-servis', NULL, @RestoranRootMenuId, 5, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');

            IF @RestoranYonetimMenuRoleId IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranRootMenuId AND [RoleId] = @RestoranYonetimMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (NEWID(), @RestoranRootMenuId, @RestoranYonetimMenuRoleId, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranYonetimMenuId AND [RoleId] = @RestoranYonetimMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (NEWID(), @RestoranYonetimMenuId, @RestoranYonetimMenuRoleId, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');
            END;

            IF @RestoranMasaMenuRoleId IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranMasaMenuId AND [RoleId] = @RestoranMasaMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (NEWID(), @RestoranMasaMenuId, @RestoranMasaMenuRoleId, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');

            IF @RestoranMenuMenuRoleId IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranMenuYonetimMenuId AND [RoleId] = @RestoranMenuMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (NEWID(), @RestoranMenuYonetimMenuId, @RestoranMenuMenuRoleId, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranKategoriHavuzuMenuId AND [RoleId] = @RestoranMenuMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (NEWID(), @RestoranKategoriHavuzuMenuId, @RestoranMenuMenuRoleId, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');
            END;

            IF @RestoranSiparisMenuRoleId IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranSiparisYonetimMenuId AND [RoleId] = @RestoranSiparisMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (NEWID(), @RestoranSiparisYonetimMenuId, @RestoranSiparisMenuRoleId, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranGarsonServisMenuId AND [RoleId] = @RestoranSiparisMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (NEWID(), @RestoranGarsonServisMenuId, @RestoranSiparisMenuRoleId, 0, @Now, @Now, N'migration_restoran_menu_auth', N'migration_restoran_menu_auth');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @KampBasvuruMenuRoleId uniqueidentifier = '6f5f2a90-79b3-4f3b-b2cf-2d0a00a00101';
            DECLARE @KampBasvuruViewRoleId uniqueidentifier = '6f5f2a90-79b3-4f3b-b2cf-2d0a00a00102';
            DECLARE @KampBasvuruManageRoleId uniqueidentifier = '6f5f2a90-79b3-4f3b-b2cf-2d0a00a00103';

            DECLARE @RestoranRootMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000001';
            DECLARE @RestoranYonetimMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000002';
            DECLARE @RestoranMasaMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000003';
            DECLARE @RestoranMenuYonetimMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000004';
            DECLARE @RestoranKategoriHavuzuMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000005';
            DECLARE @RestoranSiparisYonetimMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000006';
            DECLARE @RestoranGarsonServisMenuId uniqueidentifier = '8f400001-1f20-4f1d-9f2a-1a0000000007';

            DELETE FROM [TODBase].[MenuItemRoles]
            WHERE [MenuItemId] IN
            (
                @RestoranRootMenuId,
                @RestoranYonetimMenuId,
                @RestoranMasaMenuId,
                @RestoranMenuYonetimMenuId,
                @RestoranKategoriHavuzuMenuId,
                @RestoranSiparisYonetimMenuId,
                @RestoranGarsonServisMenuId
            );

            DELETE FROM [TODBase].[MenuItems]
            WHERE [Id] IN
            (
                @RestoranYonetimMenuId,
                @RestoranMasaMenuId,
                @RestoranMenuYonetimMenuId,
                @RestoranKategoriHavuzuMenuId,
                @RestoranSiparisYonetimMenuId,
                @RestoranGarsonServisMenuId,
                @RestoranRootMenuId
            );

            DELETE FROM [TODBase].[MenuItemRoles]
            WHERE [RoleId] IN (@KampBasvuruMenuRoleId, @KampBasvuruViewRoleId, @KampBasvuruManageRoleId);

            DELETE FROM [TODBase].[UserGroupRoles]
            WHERE [RoleId] IN (@KampBasvuruMenuRoleId, @KampBasvuruViewRoleId, @KampBasvuruManageRoleId);

            DELETE FROM [TODBase].[Roles]
            WHERE [Id] IN (@KampBasvuruMenuRoleId, @KampBasvuruViewRoleId, @KampBasvuruManageRoleId);
            """);
    }
}

