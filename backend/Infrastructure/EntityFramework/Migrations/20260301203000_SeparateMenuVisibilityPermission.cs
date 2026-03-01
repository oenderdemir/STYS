using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260301203000_SeparateMenuVisibilityPermission")]
    public partial class SeparateMenuVisibilityPermission : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @RoleManagementMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'RoleManagement' AND [Name] = 'Menu');
                IF @RoleManagementMenu IS NULL
                BEGIN
                    SET @RoleManagementMenu = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@RoleManagementMenu, 'Menu', 'RoleManagement', 0, @Now, @Now);
                END

                DECLARE @UserManagementMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserManagement' AND [Name] = 'Menu');
                IF @UserManagementMenu IS NULL
                BEGIN
                    SET @UserManagementMenu = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@UserManagementMenu, 'Menu', 'UserManagement', 0, @Now, @Now);
                END

                DECLARE @UserGroupManagementMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserGroupManagement' AND [Name] = 'Menu');
                IF @UserGroupManagementMenu IS NULL
                BEGIN
                    SET @UserGroupManagementMenu = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@UserGroupManagementMenu, 'Menu', 'UserGroupManagement', 0, @Now, @Now);
                END

                DECLARE @MenuManagementMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'MenuManagement' AND [Name] = 'Menu');
                IF @MenuManagementMenu IS NULL
                BEGIN
                    SET @MenuManagementMenu = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuManagementMenu, 'Menu', 'MenuManagement', 0, @Now, @Now);
                END

                DECLARE @CountryManagementMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'CountryManagement' AND [Name] = 'Menu');
                IF @CountryManagementMenu IS NULL
                BEGIN
                    SET @CountryManagementMenu = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@CountryManagementMenu, 'Menu', 'CountryManagement', 0, @Now, @Now);
                END

                DECLARE @IlYonetimiMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'IlYonetimi' AND [Name] = 'Menu');
                IF @IlYonetimiMenu IS NULL
                BEGIN
                    SET @IlYonetimiMenu = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@IlYonetimiMenu, 'Menu', 'IlYonetimi', 0, @Now, @Now);
                END

                DECLARE @TesisYonetimiMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'TesisYonetimi' AND [Name] = 'Menu');
                IF @TesisYonetimiMenu IS NULL
                BEGIN
                    SET @TesisYonetimiMenu = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@TesisYonetimiMenu, 'Menu', 'TesisYonetimi', 0, @Now, @Now);
                END

                DECLARE @BinaYonetimiMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'BinaYonetimi' AND [Name] = 'Menu');
                IF @BinaYonetimiMenu IS NULL
                BEGIN
                    SET @BinaYonetimiMenu = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@BinaYonetimiMenu, 'Menu', 'BinaYonetimi', 0, @Now, @Now);
                END

                DECLARE @IsletmeAlaniYonetimiMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'IsletmeAlaniYonetimi' AND [Name] = 'Menu');
                IF @IsletmeAlaniYonetimiMenu IS NULL
                BEGIN
                    SET @IsletmeAlaniYonetimiMenu = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@IsletmeAlaniYonetimiMenu, 'Menu', 'IsletmeAlaniYonetimi', 0, @Now, @Now);
                END

                DECLARE @OdaTipiYonetimiMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaTipiYonetimi' AND [Name] = 'Menu');
                IF @OdaTipiYonetimiMenu IS NULL
                BEGIN
                    SET @OdaTipiYonetimiMenu = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaTipiYonetimiMenu, 'Menu', 'OdaTipiYonetimi', 0, @Now, @Now);
                END

                DECLARE @OdaYonetimiMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Menu');
                IF @OdaYonetimiMenu IS NULL
                BEGIN
                    SET @OdaYonetimiMenu = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaYonetimiMenu, 'Menu', 'OdaYonetimi', 0, @Now, @Now);
                END

                DECLARE @OdaOzellikYonetimiMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaOzellikYonetimi' AND [Name] = 'Menu');
                IF @OdaOzellikYonetimiMenu IS NULL
                BEGIN
                    SET @OdaOzellikYonetimiMenu = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaOzellikYonetimiMenu, 'Menu', 'OdaOzellikYonetimi', 0, @Now, @Now);
                END

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
                    ('odalar', 'OdaYonetimi'),
                    ('oda-siniflari', 'OdaTipiYonetimi'),
                    ('oda-ozellikler', 'OdaOzellikYonetimi');

                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                INNER JOIN @MenuRoleMap m ON m.[Route] = mi.[Route] AND m.[Domain] = r.[Domain]
                WHERE r.[Name] = 'View';

                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), mi.[Id], r.[Id], 0, @Now, @Now
                FROM [TODBase].[MenuItems] mi
                INNER JOIN @MenuRoleMap m ON m.[Route] = mi.[Route]
                INNER JOIN [TODBase].[Roles] r ON r.[Domain] = m.[Domain] AND r.[Name] = 'Menu'
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [TODBase].[MenuItemRoles] existing
                    WHERE existing.[MenuItemId] = mi.[Id]
                      AND existing.[RoleId] = r.[Id]);

                DECLARE @AdminGroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'Yönetici Grubu');
                DECLARE @TesisYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'TesisYoneticiGrubu');
                DECLARE @BinaYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'BinaYoneticiGrubu');

                INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), @AdminGroupId, r.[Id], 0, @Now, @Now
                FROM [TODBase].[Roles] r
                WHERE @AdminGroupId IS NOT NULL
                  AND r.[Name] = 'Menu'
                  AND r.[Domain] IN (
                      'RoleManagement',
                      'UserManagement',
                      'UserGroupManagement',
                      'MenuManagement',
                      'CountryManagement',
                      'IlYonetimi',
                      'TesisYonetimi',
                      'BinaYonetimi',
                      'IsletmeAlaniYonetimi',
                      'OdaTipiYonetimi',
                      'OdaYonetimi',
                      'OdaOzellikYonetimi')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [TODBase].[UserGroupRoles] ugr
                      WHERE ugr.[UserGroupId] = @AdminGroupId
                        AND ugr.[RoleId] = r.[Id]);

                INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), @TesisYoneticiGrupId, r.[Id], 0, @Now, @Now
                FROM [TODBase].[Roles] r
                WHERE @TesisYoneticiGrupId IS NOT NULL
                  AND r.[Name] = 'Menu'
                  AND r.[Domain] IN (
                      'IlYonetimi',
                      'TesisYonetimi',
                      'BinaYonetimi',
                      'IsletmeAlaniYonetimi',
                      'OdaTipiYonetimi',
                      'OdaYonetimi',
                      'OdaOzellikYonetimi')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [TODBase].[UserGroupRoles] ugr
                      WHERE ugr.[UserGroupId] = @TesisYoneticiGrupId
                        AND ugr.[RoleId] = r.[Id]);

                INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), @BinaYoneticiGrupId, r.[Id], 0, @Now, @Now
                FROM [TODBase].[Roles] r
                WHERE @BinaYoneticiGrupId IS NOT NULL
                  AND r.[Name] = 'Menu'
                  AND r.[Domain] IN (
                      'BinaYonetimi',
                      'OdaYonetimi',
                      'OdaTipiYonetimi',
                      'OdaOzellikYonetimi')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [TODBase].[UserGroupRoles] ugr
                      WHERE ugr.[UserGroupId] = @BinaYoneticiGrupId
                        AND ugr.[RoleId] = r.[Id]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
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
                    ('odalar', 'OdaYonetimi'),
                    ('oda-siniflari', 'OdaTipiYonetimi'),
                    ('oda-ozellikler', 'OdaOzellikYonetimi');

                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                INNER JOIN @MenuRoleMap m ON m.[Route] = mi.[Route] AND m.[Domain] = r.[Domain]
                WHERE r.[Name] = 'Menu';

                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), mi.[Id], r.[Id], 0, @Now, @Now
                FROM [TODBase].[MenuItems] mi
                INNER JOIN @MenuRoleMap m ON m.[Route] = mi.[Route]
                INNER JOIN [TODBase].[Roles] r ON r.[Domain] = m.[Domain] AND r.[Name] = 'View'
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [TODBase].[MenuItemRoles] existing
                    WHERE existing.[MenuItemId] = mi.[Id]
                      AND existing.[RoleId] = r.[Id]);

                DECLARE @AdminGroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'Yönetici Grubu');
                DECLARE @TesisYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'TesisYoneticiGrubu');
                DECLARE @BinaYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'BinaYoneticiGrubu');

                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE r.[Name] = 'Menu'
                  AND r.[Domain] IN (
                      'RoleManagement',
                      'UserManagement',
                      'UserGroupManagement',
                      'MenuManagement',
                      'CountryManagement',
                      'IlYonetimi',
                      'TesisYonetimi',
                      'BinaYonetimi',
                      'IsletmeAlaniYonetimi',
                      'OdaTipiYonetimi',
                      'OdaYonetimi',
                      'OdaOzellikYonetimi')
                  AND ugr.[UserGroupId] IN (@AdminGroupId, @TesisYoneticiGrupId, @BinaYoneticiGrupId);

                DELETE FROM [TODBase].[Roles]
                WHERE [Name] = 'Menu'
                  AND [Domain] IN (
                      'RoleManagement',
                      'UserManagement',
                      'UserGroupManagement',
                      'MenuManagement',
                      'CountryManagement',
                      'IlYonetimi',
                      'TesisYonetimi',
                      'BinaYonetimi',
                      'IsletmeAlaniYonetimi',
                      'OdaTipiYonetimi',
                      'OdaYonetimi',
                      'OdaOzellikYonetimi')
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [RoleId] = [TODBase].[Roles].[Id])
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [RoleId] = [TODBase].[Roles].[Id]);
                """);
        }
    }
}
