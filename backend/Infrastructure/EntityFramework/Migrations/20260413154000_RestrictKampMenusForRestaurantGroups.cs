using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260413154000_RestrictKampMenusForRestaurantGroups")]
public partial class RestrictKampMenusForRestaurantGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @GarsonGroupId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'GarsonGrubu'
                ORDER BY [CreatedAt]
            );

            DECLARE @RestoranYoneticiGroupId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'RestoranYoneticiGrubu'
                ORDER BY [CreatedAt]
            );

            IF @GarsonGroupId IS NOT NULL OR @RestoranYoneticiGroupId IS NOT NULL
            BEGIN
                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE ugr.[UserGroupId] IN (@GarsonGroupId, @RestoranYoneticiGroupId)
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
            END;

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

            IF @KampAtamaMenuItemId IS NOT NULL
               AND @KampAtamaMenuRoleId IS NOT NULL
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM [TODBase].[MenuItemRoles]
                   WHERE [MenuItemId] = @KampAtamaMenuItemId
                     AND [RoleId] = @KampAtamaMenuRoleId
               )
            BEGIN
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (NEWID(), @KampAtamaMenuItemId, @KampAtamaMenuRoleId, 0, @Now, @Now, N'migration_kamp_restrict_restoran_groups', N'migration_kamp_restrict_restoran_groups');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // no-op
    }
}

