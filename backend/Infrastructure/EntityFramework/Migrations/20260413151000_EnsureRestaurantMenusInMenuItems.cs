using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260413151000_EnsureRestaurantMenusInMenuItems")]
public partial class EnsureRestaurantMenusInMenuItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @RestoranYonetimiMenuRoleId uniqueidentifier;
            DECLARE @RestoranMasaYonetimiMenuRoleId uniqueidentifier;
            DECLARE @RestoranMenuYonetimiMenuRoleId uniqueidentifier;
            DECLARE @RestoranSiparisYonetimiMenuRoleId uniqueidentifier;

            SELECT TOP (1) @RestoranYonetimiMenuRoleId = [Id]
            FROM [TODBase].[Roles]
            WHERE [Domain] = N'RestoranYonetimi' AND [Name] = N'Menu';

            IF @RestoranYonetimiMenuRoleId IS NULL
            BEGIN
                SET @RestoranYonetimiMenuRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RestoranYonetimiMenuRoleId, N'Menu', N'RestoranYonetimi', 0, @Now, @Now);
            END;

            SELECT TOP (1) @RestoranMasaYonetimiMenuRoleId = [Id]
            FROM [TODBase].[Roles]
            WHERE [Domain] = N'RestoranMasaYonetimi' AND [Name] = N'Menu';

            IF @RestoranMasaYonetimiMenuRoleId IS NULL
            BEGIN
                SET @RestoranMasaYonetimiMenuRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RestoranMasaYonetimiMenuRoleId, N'Menu', N'RestoranMasaYonetimi', 0, @Now, @Now);
            END;

            SELECT TOP (1) @RestoranMenuYonetimiMenuRoleId = [Id]
            FROM [TODBase].[Roles]
            WHERE [Domain] = N'RestoranMenuYonetimi' AND [Name] = N'Menu';

            IF @RestoranMenuYonetimiMenuRoleId IS NULL
            BEGIN
                SET @RestoranMenuYonetimiMenuRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RestoranMenuYonetimiMenuRoleId, N'Menu', N'RestoranMenuYonetimi', 0, @Now, @Now);
            END;

            SELECT TOP (1) @RestoranSiparisYonetimiMenuRoleId = [Id]
            FROM [TODBase].[Roles]
            WHERE [Domain] = N'RestoranSiparisYonetimi' AND [Name] = N'Menu';

            IF @RestoranSiparisYonetimiMenuRoleId IS NULL
            BEGIN
                SET @RestoranSiparisYonetimiMenuRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RestoranSiparisYonetimiMenuRoleId, N'Menu', N'RestoranSiparisYonetimi', 0, @Now, @Now);
            END;

            DECLARE @RestoranRootMenuId uniqueidentifier;

            SELECT TOP (1) @RestoranRootMenuId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Restoran'
              AND [ParentId] IS NULL
              AND [IsDeleted] = 0;

            IF @RestoranRootMenuId IS NULL
            BEGIN
                SET @RestoranRootMenuId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RestoranRootMenuId, N'Restoran', N'pi pi-shop', N'', NULL, NULL, 4, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Icon] = N'pi pi-shop',
                    [Route] = N'',
                    [ParentId] = NULL,
                    [MenuOrder] = 4,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now
                WHERE [Id] = @RestoranRootMenuId;
            END;

            DECLARE @RestoranYonetimiMenuItemId uniqueidentifier;
            DECLARE @RestoranMasaYonetimiMenuItemId uniqueidentifier;
            DECLARE @RestoranMenuYonetimiMenuItemId uniqueidentifier;
            DECLARE @RestoranKategoriHavuzuMenuItemId uniqueidentifier;
            DECLARE @RestoranSiparisYonetimiMenuItemId uniqueidentifier;
            DECLARE @GarsonServisMenuItemId uniqueidentifier;

            SELECT TOP (1) @RestoranYonetimiMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'restoran-yonetimi' AND [IsDeleted] = 0;
            IF @RestoranYonetimiMenuItemId IS NULL
            BEGIN
                SET @RestoranYonetimiMenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RestoranYonetimiMenuItemId, N'Restoran Yonetimi', N'pi pi-building', N'restoran-yonetimi', NULL, @RestoranRootMenuId, 0, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Restoran Yonetimi', [Icon] = N'pi pi-building', [ParentId] = @RestoranRootMenuId, [MenuOrder] = 0, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @RestoranYonetimiMenuItemId;
            END;

            SELECT TOP (1) @RestoranMasaYonetimiMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'restoran-masa-yonetimi' AND [IsDeleted] = 0;
            IF @RestoranMasaYonetimiMenuItemId IS NULL
            BEGIN
                SET @RestoranMasaYonetimiMenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RestoranMasaYonetimiMenuItemId, N'Masa Yonetimi', N'pi pi-table', N'restoran-masa-yonetimi', NULL, @RestoranRootMenuId, 1, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Masa Yonetimi', [Icon] = N'pi pi-table', [ParentId] = @RestoranRootMenuId, [MenuOrder] = 1, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @RestoranMasaYonetimiMenuItemId;
            END;

            SELECT TOP (1) @RestoranMenuYonetimiMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'restoran-menu-yonetimi' AND [IsDeleted] = 0;
            IF @RestoranMenuYonetimiMenuItemId IS NULL
            BEGIN
                SET @RestoranMenuYonetimiMenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RestoranMenuYonetimiMenuItemId, N'Menu Yonetimi', N'pi pi-list', N'restoran-menu-yonetimi', NULL, @RestoranRootMenuId, 2, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Menu Yonetimi', [Icon] = N'pi pi-list', [ParentId] = @RestoranRootMenuId, [MenuOrder] = 2, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @RestoranMenuYonetimiMenuItemId;
            END;

            SELECT TOP (1) @RestoranKategoriHavuzuMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'restoran-kategori-havuzu' AND [IsDeleted] = 0;
            IF @RestoranKategoriHavuzuMenuItemId IS NULL
            BEGIN
                SET @RestoranKategoriHavuzuMenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RestoranKategoriHavuzuMenuItemId, N'Kategori Havuzu', N'pi pi-sitemap', N'restoran-kategori-havuzu', NULL, @RestoranRootMenuId, 3, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Kategori Havuzu', [Icon] = N'pi pi-sitemap', [ParentId] = @RestoranRootMenuId, [MenuOrder] = 3, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @RestoranKategoriHavuzuMenuItemId;
            END;

            SELECT TOP (1) @RestoranSiparisYonetimiMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'restoran-siparis-yonetimi' AND [IsDeleted] = 0;
            IF @RestoranSiparisYonetimiMenuItemId IS NULL
            BEGIN
                SET @RestoranSiparisYonetimiMenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RestoranSiparisYonetimiMenuItemId, N'Siparis Yonetimi', N'pi pi-shopping-cart', N'restoran-siparis-yonetimi', NULL, @RestoranRootMenuId, 4, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Siparis Yonetimi', [Icon] = N'pi pi-shopping-cart', [ParentId] = @RestoranRootMenuId, [MenuOrder] = 4, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @RestoranSiparisYonetimiMenuItemId;
            END;

            SELECT TOP (1) @GarsonServisMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'garson-servis' AND [IsDeleted] = 0;
            IF @GarsonServisMenuItemId IS NULL
            BEGIN
                SET @GarsonServisMenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@GarsonServisMenuItemId, N'Garson Servis', N'pi pi-bolt', N'garson-servis', NULL, @RestoranRootMenuId, 5, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Garson Servis', [Icon] = N'pi pi-bolt', [ParentId] = @RestoranRootMenuId, [MenuOrder] = 5, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @GarsonServisMenuItemId;
            END;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranRootMenuId AND [RoleId] = @RestoranYonetimiMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @RestoranRootMenuId, @RestoranYonetimiMenuRoleId, 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranYonetimiMenuItemId AND [RoleId] = @RestoranYonetimiMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @RestoranYonetimiMenuItemId, @RestoranYonetimiMenuRoleId, 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranMasaYonetimiMenuItemId AND [RoleId] = @RestoranMasaYonetimiMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @RestoranMasaYonetimiMenuItemId, @RestoranMasaYonetimiMenuRoleId, 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranMenuYonetimiMenuItemId AND [RoleId] = @RestoranMenuYonetimiMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @RestoranMenuYonetimiMenuItemId, @RestoranMenuYonetimiMenuRoleId, 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranKategoriHavuzuMenuItemId AND [RoleId] = @RestoranMenuYonetimiMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @RestoranKategoriHavuzuMenuItemId, @RestoranMenuYonetimiMenuRoleId, 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RestoranSiparisYonetimiMenuItemId AND [RoleId] = @RestoranSiparisYonetimiMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @RestoranSiparisYonetimiMenuItemId, @RestoranSiparisYonetimiMenuRoleId, 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @GarsonServisMenuItemId AND [RoleId] = @RestoranSiparisYonetimiMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @GarsonServisMenuItemId, @RestoranSiparisYonetimiMenuRoleId, 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE mir
            FROM [TODBase].[MenuItemRoles] mir
            INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
            WHERE mi.[Route] IN (N'restoran-yonetimi', N'restoran-masa-yonetimi', N'restoran-menu-yonetimi', N'restoran-kategori-havuzu', N'restoran-siparis-yonetimi', N'garson-servis')
               OR (mi.[Label] = N'Restoran' AND mi.[ParentId] IS NULL);

            DELETE FROM [TODBase].[MenuItems]
            WHERE [Route] IN (N'restoran-yonetimi', N'restoran-masa-yonetimi', N'restoran-menu-yonetimi', N'restoran-kategori-havuzu', N'restoran-siparis-yonetimi', N'garson-servis');

            DELETE FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Restoran'
              AND [ParentId] IS NULL
              AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] x WHERE x.[ParentId] = [TODBase].[MenuItems].[Id] AND x.[IsDeleted] = 0);
            """);
    }
}

