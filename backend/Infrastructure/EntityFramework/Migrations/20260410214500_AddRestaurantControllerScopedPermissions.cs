using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260410214500_AddRestaurantControllerScopedPermissions")]
public partial class AddRestaurantControllerScopedPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';

            DECLARE @Domains TABLE ([Domain] nvarchar(128) NOT NULL);
            INSERT INTO @Domains ([Domain])
            VALUES
            (N'RestoranYonetimi'),
            (N'RestoranMasaYonetimi'),
            (N'RestoranMenuYonetimi'),
            (N'RestoranSiparisYonetimi'),
            (N'RestoranOdemeYonetimi');

            DECLARE @RoleNames TABLE ([Name] nvarchar(32) NOT NULL);
            INSERT INTO @RoleNames ([Name])
            VALUES (N'Menu'), (N'View'), (N'Manage');

            ;WITH RoleMatrix AS
            (
                SELECT d.[Domain], r.[Name]
                FROM @Domains d
                CROSS JOIN @RoleNames r
            )
            INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), rm.[Name], rm.[Domain], 0, @Now, @Now
            FROM RoleMatrix rm
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[Roles] x
                WHERE x.[Domain] = rm.[Domain]
                  AND x.[Name] = rm.[Name]
            );

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
            BEGIN
                INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), @AdminGroupId, r.[Id], 0, @Now, @Now
                FROM [TODBase].[Roles] r
                INNER JOIN @Domains d ON d.[Domain] = r.[Domain]
                WHERE r.[Name] IN (N'Menu', N'View', N'Manage')
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [TODBase].[UserGroupRoles] ugr
                      WHERE ugr.[UserGroupId] = @AdminGroupId
                        AND ugr.[RoleId] = r.[Id]
                  );
            END;

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
            BEGIN
                INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), @TesisManagerGroupId, r.[Id], 0, @Now, @Now
                FROM [TODBase].[Roles] r
                INNER JOIN @Domains d ON d.[Domain] = r.[Domain]
                WHERE r.[Name] IN (N'Menu', N'View', N'Manage')
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [TODBase].[UserGroupRoles] ugr
                      WHERE ugr.[UserGroupId] = @TesisManagerGroupId
                        AND ugr.[RoleId] = r.[Id]
                  );
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE ugr
            FROM [TODBase].[UserGroupRoles] ugr
            INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
            WHERE r.[Domain] IN (N'RestoranYonetimi', N'RestoranMasaYonetimi', N'RestoranMenuYonetimi', N'RestoranSiparisYonetimi', N'RestoranOdemeYonetimi');

            DELETE FROM [TODBase].[Roles]
            WHERE [Domain] IN (N'RestoranYonetimi', N'RestoranMasaYonetimi', N'RestoranMenuYonetimi', N'RestoranSiparisYonetimi', N'RestoranOdemeYonetimi');
            """);
    }
}
