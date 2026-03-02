using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260302143000_EnforceMenuRoleOnlyForMenuItems")]
    public partial class EnforceMenuRoleOnlyForMenuItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @MenuRoleMap TABLE
                (
                    [Route] nvarchar(128) NOT NULL,
                    [Domain] nvarchar(128) NOT NULL
                );

                INSERT INTO @MenuRoleMap ([Route], [Domain])
                VALUES
                    ('yetkiler', 'RoleManagement'),
                    ('kullanici-gruplar', 'UserGroupManagement'),
                    ('kullanicilar', 'UserManagement'),
                    ('menuler', 'MenuManagement'),
                    ('ulkeler', 'CountryManagement'),
                    ('iller', 'IlYonetimi'),
                    ('tesisler', 'TesisYonetimi'),
                    ('binalar', 'BinaYonetimi'),
                    ('isletme-alanlari', 'IsletmeAlaniYonetimi'),
                    ('oda-tipleri', 'OdaTipiYonetimi'),
                    ('oda-siniflari', 'OdaTipiYonetimi'),
                    ('oda-ozellikler', 'OdaOzellikYonetimi'),
                    ('odalar', 'OdaYonetimi');

                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), 'Menu', map.[Domain], 0, @Now, @Now
                FROM (SELECT DISTINCT [Domain] FROM @MenuRoleMap) map
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [TODBase].[Roles] existing
                    WHERE existing.[Domain] = map.[Domain]
                      AND existing.[Name] = 'Menu');

                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                WHERE r.[Name] <> 'Menu';

                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                INNER JOIN @MenuRoleMap map ON map.[Route] = mi.[Route]
                WHERE r.[Name] = 'Menu'
                  AND r.[Domain] <> map.[Domain];

                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), mi.[Id], r.[Id], 0, @Now, @Now
                FROM [TODBase].[MenuItems] mi
                INNER JOIN @MenuRoleMap map ON map.[Route] = mi.[Route]
                INNER JOIN [TODBase].[Roles] r ON r.[Domain] = map.[Domain] AND r.[Name] = 'Menu'
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [TODBase].[MenuItemRoles] existing
                    WHERE existing.[MenuItemId] = mi.[Id]
                      AND existing.[RoleId] = r.[Id]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: this migration normalizes menu-role links.
        }
    }
}
