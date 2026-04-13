using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260413162000_NormalizeRestaurantGroupRoleAssignments")]
public partial class NormalizeRestaurantGroupRoleAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @GarsonGroups TABLE ([GroupId] uniqueidentifier PRIMARY KEY);
            DECLARE @RestoranYoneticiGroups TABLE ([GroupId] uniqueidentifier PRIMARY KEY);

            INSERT INTO @GarsonGroups ([GroupId])
            SELECT [Id]
            FROM [TODBase].[UserGroups]
            WHERE [Name] = N'GarsonGrubu'
              AND [IsDeleted] = 0;

            INSERT INTO @RestoranYoneticiGroups ([GroupId])
            SELECT [Id]
            FROM [TODBase].[UserGroups]
            WHERE [Name] = N'RestoranYoneticiGrubu'
              AND [IsDeleted] = 0;

            DECLARE @GarsonRequiredRoles TABLE ([Domain] nvarchar(128) NOT NULL, [Name] nvarchar(64) NOT NULL);
            INSERT INTO @GarsonRequiredRoles ([Domain], [Name])
            VALUES
            (N'KullaniciTipi', N'UIUser'),
            (N'RestoranYonetimi', N'Menu'),
            (N'RestoranYonetimi', N'View'),
            (N'RestoranSiparisYonetimi', N'Menu'),
            (N'RestoranSiparisYonetimi', N'View'),
            (N'RestoranSiparisYonetimi', N'Manage'),
            (N'RestoranMenuYonetimi', N'View');

            DECLARE @RestoranYoneticiRequiredRoles TABLE ([Domain] nvarchar(128) NOT NULL, [Name] nvarchar(64) NOT NULL);
            INSERT INTO @RestoranYoneticiRequiredRoles ([Domain], [Name])
            VALUES
            (N'KullaniciTipi', N'UIUser'),
            (N'RestoranYonetimi', N'Menu'),
            (N'RestoranYonetimi', N'View'),
            (N'RestoranYonetimi', N'Manage'),
            (N'RestoranMasaYonetimi', N'Menu'),
            (N'RestoranMasaYonetimi', N'View'),
            (N'RestoranMasaYonetimi', N'Manage'),
            (N'RestoranMenuYonetimi', N'Menu'),
            (N'RestoranMenuYonetimi', N'View'),
            (N'RestoranMenuYonetimi', N'Manage'),
            (N'RestoranSiparisYonetimi', N'Menu'),
            (N'RestoranSiparisYonetimi', N'View'),
            (N'RestoranSiparisYonetimi', N'Manage'),
            (N'RestoranOdemeYonetimi', N'Menu'),
            (N'RestoranOdemeYonetimi', N'View'),
            (N'RestoranOdemeYonetimi', N'Manage');

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT NEWID(), g.[GroupId], r.[Id], 0, @Now, @Now, N'migration_restoran_group_normalize', N'migration_restoran_group_normalize'
            FROM @GarsonGroups g
            CROSS JOIN @GarsonRequiredRoles rr
            INNER JOIN [TODBase].[Roles] r ON r.[Domain] = rr.[Domain] AND r.[Name] = rr.[Name] AND r.[IsDeleted] = 0
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[UserGroupRoles] ugr
                WHERE ugr.[UserGroupId] = g.[GroupId]
                  AND ugr.[RoleId] = r.[Id]
            );

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT NEWID(), g.[GroupId], r.[Id], 0, @Now, @Now, N'migration_restoran_group_normalize', N'migration_restoran_group_normalize'
            FROM @RestoranYoneticiGroups g
            CROSS JOIN @RestoranYoneticiRequiredRoles rr
            INNER JOIN [TODBase].[Roles] r ON r.[Domain] = rr.[Domain] AND r.[Name] = rr.[Name] AND r.[IsDeleted] = 0
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[UserGroupRoles] ugr
                WHERE ugr.[UserGroupId] = g.[GroupId]
                  AND ugr.[RoleId] = r.[Id]
            );

            DELETE ugr
            FROM [TODBase].[UserGroupRoles] ugr
            INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
            WHERE ugr.[UserGroupId] IN
            (
                SELECT [GroupId] FROM @GarsonGroups
                UNION
                SELECT [GroupId] FROM @RestoranYoneticiGroups
            )
              AND r.[Domain] IN
              (
                  N'KampProgramiYonetimi',
                  N'KampProgramiTanimYonetimi',
                  N'KampDonemiYonetimi',
                  N'KampDonemiTanimYonetimi',
                  N'KampDonemiTesisAtamaYonetimi',
                  N'KampTahsisYonetimi',
                  N'KampRezervasyonYonetimi',
                  N'KampIadeYonetimi',
                  N'KampPuanKuraliYonetimi',
                  N'KampTarifeYonetimi',
                  N'KampBasvuruYonetimi'
              );

            DECLARE @KampAtamaMenuItemId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'kamp-donemi-atamalari'
                  AND [IsDeleted] = 0
            );

            DECLARE @KampAtamaMenuRoleId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'KampDonemiTesisAtamaYonetimi'
                  AND [Name] = N'Menu'
            );

            IF @KampAtamaMenuItemId IS NOT NULL AND @KampAtamaMenuRoleId IS NOT NULL
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM [TODBase].[MenuItemRoles]
                   WHERE [MenuItemId] = @KampAtamaMenuItemId
                     AND [RoleId] = @KampAtamaMenuRoleId
               )
            BEGIN
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (NEWID(), @KampAtamaMenuItemId, @KampAtamaMenuRoleId, 0, @Now, @Now, N'migration_restoran_group_normalize', N'migration_restoran_group_normalize');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // no-op
    }
}

