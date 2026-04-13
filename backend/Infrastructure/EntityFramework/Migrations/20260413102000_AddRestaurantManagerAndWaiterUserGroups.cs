using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260413102000_AddRestaurantManagerAndWaiterUserGroups")]
public partial class AddRestaurantManagerAndWaiterUserGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @RestoranYoneticiGrupAdi nvarchar(128) = N'RestoranYoneticiGrubu';
            DECLARE @GarsonGrupAdi nvarchar(128) = N'GarsonGrubu';

            DECLARE @RestoranYoneticiGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222205';
            DECLARE @GarsonGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222206';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Name] = @RestoranYoneticiGrupAdi)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @RestoranYoneticiGroupId)
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@RestoranYoneticiGroupId, @RestoranYoneticiGrupAdi, 0, @Now, @Now);
                ELSE
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @RestoranYoneticiGrupAdi, 0, @Now, @Now);
            END;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Name] = @GarsonGrupAdi)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @GarsonGroupId)
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@GarsonGroupId, @GarsonGrupAdi, 0, @Now, @Now);
                ELSE
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @GarsonGrupAdi, 0, @Now, @Now);
            END;

            SET @RestoranYoneticiGroupId =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = @RestoranYoneticiGrupAdi
                ORDER BY [CreatedAt]
            );

            SET @GarsonGroupId =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = @GarsonGrupAdi
                ORDER BY [CreatedAt]
            );

            DECLARE @RequiredRoles TABLE
            (
                [Domain] nvarchar(128) NOT NULL,
                [Name] nvarchar(64) NOT NULL,
                [TargetGroup] nvarchar(32) NOT NULL
            );

            INSERT INTO @RequiredRoles ([Domain], [Name], [TargetGroup])
            VALUES
            (N'RestoranYonetimi', N'Menu', N'Manager'),
            (N'RestoranYonetimi', N'View', N'Manager'),
            (N'RestoranYonetimi', N'Manage', N'Manager'),
            (N'RestoranMasaYonetimi', N'Menu', N'Manager'),
            (N'RestoranMasaYonetimi', N'View', N'Manager'),
            (N'RestoranMasaYonetimi', N'Manage', N'Manager'),
            (N'RestoranMenuYonetimi', N'Menu', N'Manager'),
            (N'RestoranMenuYonetimi', N'View', N'Manager'),
            (N'RestoranMenuYonetimi', N'Manage', N'Manager'),
            (N'RestoranSiparisYonetimi', N'Menu', N'Manager'),
            (N'RestoranSiparisYonetimi', N'View', N'Manager'),
            (N'RestoranSiparisYonetimi', N'Manage', N'Manager'),
            (N'RestoranOdemeYonetimi', N'Menu', N'Manager'),
            (N'RestoranOdemeYonetimi', N'View', N'Manager'),
            (N'RestoranOdemeYonetimi', N'Manage', N'Manager'),
            (N'KullaniciAtama', N'RestoranYoneticisiAtanabilir', N'Manager'),
            (N'KullaniciAtama', N'RestoranGarsonuAtayabilir', N'Manager'),

            (N'RestoranYonetimi', N'Menu', N'Waiter'),
            (N'RestoranYonetimi', N'View', N'Waiter'),
            (N'RestoranSiparisYonetimi', N'Menu', N'Waiter'),
            (N'RestoranSiparisYonetimi', N'View', N'Waiter'),
            (N'RestoranSiparisYonetimi', N'Manage', N'Waiter'),
            (N'RestoranMenuYonetimi', N'View', N'Waiter'),
            (N'KullaniciAtama', N'RestoranGarsonuAtanabilir', N'Waiter');

            INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), rr.[Name], rr.[Domain], 0, @Now, @Now
            FROM (SELECT DISTINCT [Domain], [Name] FROM @RequiredRoles) rr
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[Roles] r
                WHERE r.[Domain] = rr.[Domain]
                  AND r.[Name] = rr.[Name]
            );

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(),
                   CASE rr.[TargetGroup]
                       WHEN N'Manager' THEN @RestoranYoneticiGroupId
                       WHEN N'Waiter' THEN @GarsonGroupId
                   END,
                   r.[Id],
                   0,
                   @Now,
                   @Now
            FROM @RequiredRoles rr
            INNER JOIN [TODBase].[Roles] r
                ON r.[Domain] = rr.[Domain]
               AND r.[Name] = rr.[Name]
            WHERE CASE rr.[TargetGroup]
                      WHEN N'Manager' THEN @RestoranYoneticiGroupId
                      WHEN N'Waiter' THEN @GarsonGroupId
                  END IS NOT NULL
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [TODBase].[UserGroupRoles] ugr
                  WHERE ugr.[UserGroupId] =
                        CASE rr.[TargetGroup]
                            WHEN N'Manager' THEN @RestoranYoneticiGroupId
                            WHEN N'Waiter' THEN @GarsonGroupId
                        END
                    AND ugr.[RoleId] = r.[Id]
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @RestoranYoneticiGrupAdi nvarchar(128) = N'RestoranYoneticiGrubu';
            DECLARE @GarsonGrupAdi nvarchar(128) = N'GarsonGrubu';

            DECLARE @RestoranYoneticiGroupId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = @RestoranYoneticiGrupAdi
                ORDER BY [CreatedAt]
            );

            DECLARE @GarsonGroupId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = @GarsonGrupAdi
                ORDER BY [CreatedAt]
            );

            DELETE FROM [TODBase].[UserGroupRoles]
            WHERE [UserGroupId] IN (@RestoranYoneticiGroupId, @GarsonGroupId);

            DELETE FROM [TODBase].[UserUserGroups]
            WHERE [UserGroupId] IN (@RestoranYoneticiGroupId, @GarsonGroupId);

            DELETE FROM [TODBase].[UserGroups]
            WHERE [Id] IN (@RestoranYoneticiGroupId, @GarsonGroupId);
            """);
    }
}
