using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260413170000_SplitRestaurantPermissionsForMicroAuthorization")]
public partial class SplitRestaurantPermissionsForMicroAuthorization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @Domains TABLE ([Domain] nvarchar(128) NOT NULL);
            INSERT INTO @Domains ([Domain])
            VALUES
            (N'RestoranKategoriHavuzuYonetimi'),
            (N'GarsonServisYonetimi');

            DECLARE @Names TABLE ([Name] nvarchar(32) NOT NULL);
            INSERT INTO @Names ([Name]) VALUES (N'Menu'), (N'View'), (N'Manage');

            INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), n.[Name], d.[Domain], 0, @Now, @Now
            FROM @Domains d
            CROSS JOIN @Names n
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[Roles] r
                WHERE r.[Domain] = d.[Domain]
                  AND r.[Name] = n.[Name]
            );

            DECLARE @AdminGroups TABLE ([GroupId] uniqueidentifier PRIMARY KEY);
            INSERT INTO @AdminGroups ([GroupId])
            SELECT [Id] FROM [TODBase].[UserGroups]
            WHERE [Id] IN
            (
                '22222222-2222-2222-2222-222222222201',
                '22222222-2222-2222-2222-222222222202'
            )
              AND [IsDeleted] = 0;

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT NEWID(), g.[GroupId], r.[Id], 0, @Now, @Now, N'migration_restoran_micro_auth', N'migration_restoran_micro_auth'
            FROM @AdminGroups g
            INNER JOIN [TODBase].[Roles] r
                ON r.[Domain] IN (N'RestoranKategoriHavuzuYonetimi', N'GarsonServisYonetimi')
               AND r.[Name] IN (N'Menu', N'View', N'Manage')
               AND r.[IsDeleted] = 0
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[UserGroupRoles] ugr
                WHERE ugr.[UserGroupId] = g.[GroupId]
                  AND ugr.[RoleId] = r.[Id]
            );

            DECLARE @ManagerGroups TABLE ([GroupId] uniqueidentifier PRIMARY KEY);
            INSERT INTO @ManagerGroups ([GroupId])
            SELECT [Id] FROM [TODBase].[UserGroups]
            WHERE [Name] = N'RestoranYoneticiGrubu'
              AND [IsDeleted] = 0;

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT NEWID(), g.[GroupId], r.[Id], 0, @Now, @Now, N'migration_restoran_micro_auth', N'migration_restoran_micro_auth'
            FROM @ManagerGroups g
            INNER JOIN [TODBase].[Roles] r
                ON
                (
                    r.[Domain] = N'RestoranKategoriHavuzuYonetimi'
                    AND r.[Name] IN (N'Menu', N'View', N'Manage')
                )
                OR
                (
                    r.[Domain] = N'GarsonServisYonetimi'
                    AND r.[Name] IN (N'Menu', N'View', N'Manage')
                )
            WHERE r.[IsDeleted] = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [TODBase].[UserGroupRoles] ugr
                  WHERE ugr.[UserGroupId] = g.[GroupId]
                    AND ugr.[RoleId] = r.[Id]
              );

            DECLARE @WaiterGroups TABLE ([GroupId] uniqueidentifier PRIMARY KEY);
            INSERT INTO @WaiterGroups ([GroupId])
            SELECT [Id] FROM [TODBase].[UserGroups]
            WHERE [Name] = N'GarsonGrubu'
              AND [IsDeleted] = 0;

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT NEWID(), g.[GroupId], r.[Id], 0, @Now, @Now, N'migration_restoran_micro_auth', N'migration_restoran_micro_auth'
            FROM @WaiterGroups g
            INNER JOIN [TODBase].[Roles] r
                ON r.[Domain] = N'GarsonServisYonetimi'
               AND r.[Name] IN (N'Menu', N'View', N'Manage')
               AND r.[IsDeleted] = 0
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[UserGroupRoles] ugr
                WHERE ugr.[UserGroupId] = g.[GroupId]
                  AND ugr.[RoleId] = r.[Id]
            );

            DECLARE @KategoriHavuzuMenuItemId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'restoran-kategori-havuzu'
                  AND [IsDeleted] = 0
            );

            DECLARE @GarsonServisMenuItemId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'garson-servis'
                  AND [IsDeleted] = 0
            );

            DECLARE @KategoriHavuzuMenuRoleId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'RestoranKategoriHavuzuYonetimi'
                  AND [Name] = N'Menu'
            );

            DECLARE @GarsonServisMenuRoleId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'GarsonServisYonetimi'
                  AND [Name] = N'Menu'
            );

            IF @KategoriHavuzuMenuItemId IS NOT NULL AND @KategoriHavuzuMenuRoleId IS NOT NULL
            BEGIN
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                WHERE mir.[MenuItemId] = @KategoriHavuzuMenuItemId
                  AND r.[Domain] = N'RestoranMenuYonetimi'
                  AND r.[Name] = N'Menu';

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM [TODBase].[MenuItemRoles]
                    WHERE [MenuItemId] = @KategoriHavuzuMenuItemId
                      AND [RoleId] = @KategoriHavuzuMenuRoleId
                )
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (NEWID(), @KategoriHavuzuMenuItemId, @KategoriHavuzuMenuRoleId, 0, @Now, @Now, N'migration_restoran_micro_auth', N'migration_restoran_micro_auth');
            END;

            IF @GarsonServisMenuItemId IS NOT NULL AND @GarsonServisMenuRoleId IS NOT NULL
            BEGIN
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                WHERE mir.[MenuItemId] = @GarsonServisMenuItemId
                  AND r.[Domain] = N'RestoranSiparisYonetimi'
                  AND r.[Name] = N'Menu';

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM [TODBase].[MenuItemRoles]
                    WHERE [MenuItemId] = @GarsonServisMenuItemId
                      AND [RoleId] = @GarsonServisMenuRoleId
                )
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (NEWID(), @GarsonServisMenuItemId, @GarsonServisMenuRoleId, 0, @Now, @Now, N'migration_restoran_micro_auth', N'migration_restoran_micro_auth');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // no-op
    }
}
