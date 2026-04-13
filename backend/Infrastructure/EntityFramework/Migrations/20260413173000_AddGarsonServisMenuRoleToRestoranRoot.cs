using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260413173000_AddGarsonServisMenuRoleToRestoranRoot")]
public partial class AddGarsonServisMenuRoleToRestoranRoot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @RestoranRootMenuId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Restoran'
                  AND [ParentId] IS NULL
                  AND [IsDeleted] = 0
            );

            DECLARE @GarsonServisMenuRoleId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'GarsonServisYonetimi'
                  AND [Name] = N'Menu'
            );

            IF @RestoranRootMenuId IS NOT NULL
               AND @GarsonServisMenuRoleId IS NOT NULL
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM [TODBase].[MenuItemRoles]
                   WHERE [MenuItemId] = @RestoranRootMenuId
                     AND [RoleId] = @GarsonServisMenuRoleId
               )
            BEGIN
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (NEWID(), @RestoranRootMenuId, @GarsonServisMenuRoleId, 0, @Now, @Now, N'migration_garson_root_menu', N'migration_garson_root_menu');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // no-op
    }
}

