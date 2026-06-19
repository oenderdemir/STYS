using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260619123000_AddMuhasebeSpecificMenuPermissions")]
public partial class AddMuhasebeSpecificMenuPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @MuhasebeciGroupId uniqueidentifier = (
                SELECT TOP (1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'MuhasebeciGrubu' AND [IsDeleted] = 0
                ORDER BY [CreatedAt]
            );

            DECLARE @RequiredRoles TABLE
            (
                [Domain] nvarchar(128) NOT NULL,
                [Name] nvarchar(64) NOT NULL
            );

            INSERT INTO @RequiredRoles ([Domain], [Name])
            VALUES
            (N'MuhasebeDashboardYonetimi', N'Menu'),
            (N'MuhasebeDashboardYonetimi', N'View'),
            (N'MuhasebeSatisBelgeleriYonetimi', N'Menu'),
            (N'MuhasebeSatisBelgeleriYonetimi', N'View'),
            (N'MuhasebeSatisBelgeleriYonetimi', N'Manage'),
            (N'MuhasebeAlisBelgeleriYonetimi', N'Menu'),
            (N'MuhasebeAlisBelgeleriYonetimi', N'View'),
            (N'MuhasebeAlisBelgeleriYonetimi', N'Manage'),
            (N'MuhasebeKdvIstisnaTanimlariYonetimi', N'Menu'),
            (N'MuhasebeKdvIstisnaTanimlariYonetimi', N'View'),
            (N'MuhasebeKdvIstisnaTanimlariYonetimi', N'Manage'),
            (N'MuhasebeKdvHareketRaporuYonetimi', N'Menu'),
            (N'MuhasebeKdvHareketRaporuYonetimi', N'View'),
            (N'MuhasebeKdvOzetRaporuYonetimi', N'Menu'),
            (N'MuhasebeKdvOzetRaporuYonetimi', N'View'),
            (N'MuhasebeKdvBeyannameHazirlikKontrolYonetimi', N'Menu'),
            (N'MuhasebeKdvBeyannameHazirlikKontrolYonetimi', N'View');

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

            IF @MuhasebeciGroupId IS NOT NULL
            BEGIN
                INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), @MuhasebeciGroupId, r.[Id], 0, @Now, @Now
                FROM @RequiredRoles rr
                INNER JOIN [TODBase].[Roles] r
                    ON r.[Domain] = rr.[Domain]
                   AND r.[Name] = rr.[Name]
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [TODBase].[UserGroupRoles] ugr
                    WHERE ugr.[UserGroupId] = @MuhasebeciGroupId
                      AND ugr.[RoleId] = r.[Id]
                      AND ugr.[IsDeleted] = 0
                );
            END;

            IF @AdminGroupId IS NOT NULL
            BEGIN
                INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), @AdminGroupId, r.[Id], 0, @Now, @Now
                FROM @RequiredRoles rr
                INNER JOIN [TODBase].[Roles] r
                    ON r.[Domain] = rr.[Domain]
                   AND r.[Name] = rr.[Name]
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [TODBase].[UserGroupRoles] ugr
                    WHERE ugr.[UserGroupId] = @AdminGroupId
                      AND ugr.[RoleId] = r.[Id]
                      AND ugr.[IsDeleted] = 0
                );
            END;

            IF @TesisManagerGroupId IS NOT NULL
            BEGIN
                INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), @TesisManagerGroupId, r.[Id], 0, @Now, @Now
                FROM @RequiredRoles rr
                INNER JOIN [TODBase].[Roles] r
                    ON r.[Domain] = rr.[Domain]
                   AND r.[Name] = rr.[Name]
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [TODBase].[UserGroupRoles] ugr
                    WHERE ugr.[UserGroupId] = @TesisManagerGroupId
                      AND ugr.[RoleId] = r.[Id]
                      AND ugr.[IsDeleted] = 0
                );
            END;

            DECLARE @DashboardMenuItemId uniqueidentifier;
            DECLARE @SatisBelgeleriMenuItemId uniqueidentifier;
            DECLARE @AlisBelgeleriMenuItemId uniqueidentifier;
            DECLARE @KdvIstisnaTanimlariMenuItemId uniqueidentifier;
            DECLARE @KdvHareketRaporuMenuItemId uniqueidentifier;
            DECLARE @KdvOzetRaporuMenuItemId uniqueidentifier;
            DECLARE @KdvBeyannameHazirlikKontrolMenuItemId uniqueidentifier;

            SELECT TOP (1) @DashboardMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/dashboard' AND [IsDeleted] = 0;
            SELECT TOP (1) @SatisBelgeleriMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/satis-belgeleri' AND [IsDeleted] = 0;
            SELECT TOP (1) @AlisBelgeleriMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/alis-belgeleri' AND [IsDeleted] = 0;
            SELECT TOP (1) @KdvIstisnaTanimlariMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/kdv-istisna-tanimlari' AND [IsDeleted] = 0;
            SELECT TOP (1) @KdvHareketRaporuMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/kdv-hareket-raporu' AND [IsDeleted] = 0;
            SELECT TOP (1) @KdvOzetRaporuMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/kdv-ozet-raporu' AND [IsDeleted] = 0;
            SELECT TOP (1) @KdvBeyannameHazirlikKontrolMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/kdv-beyanname-hazirlik-kontrol' AND [IsDeleted] = 0;

            DECLARE @MenuRoleId uniqueidentifier;

            -- Dashboard
            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeDashboardYonetimi' AND [Name] = N'Menu' AND [IsDeleted] = 0 ORDER BY [CreatedAt];
            IF @DashboardMenuItemId IS NOT NULL AND @MenuRoleId IS NOT NULL
            BEGIN
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                WHERE mir.[MenuItemId] = @DashboardMenuItemId
                  AND r.[Domain] = N'MuhasebeFisYonetimi'
                  AND r.[Name] = N'View';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @DashboardMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @DashboardMenuItemId, @MenuRoleId, 0, @Now, @Now);
            END;

            -- Satış Belgeleri
            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeSatisBelgeleriYonetimi' AND [Name] = N'Menu' AND [IsDeleted] = 0 ORDER BY [CreatedAt];
            IF @SatisBelgeleriMenuItemId IS NOT NULL AND @MenuRoleId IS NOT NULL
            BEGIN
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                WHERE mir.[MenuItemId] = @SatisBelgeleriMenuItemId
                  AND r.[Domain] = N'MuhasebeFisYonetimi'
                  AND r.[Name] = N'View';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @SatisBelgeleriMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @SatisBelgeleriMenuItemId, @MenuRoleId, 0, @Now, @Now);
            END;

            -- Alış Belgeleri
            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeAlisBelgeleriYonetimi' AND [Name] = N'Menu' AND [IsDeleted] = 0 ORDER BY [CreatedAt];
            IF @AlisBelgeleriMenuItemId IS NOT NULL AND @MenuRoleId IS NOT NULL
            BEGIN
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                WHERE mir.[MenuItemId] = @AlisBelgeleriMenuItemId
                  AND r.[Domain] = N'MuhasebeFisYonetimi'
                  AND r.[Name] = N'View';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @AlisBelgeleriMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @AlisBelgeleriMenuItemId, @MenuRoleId, 0, @Now, @Now);
            END;

            -- KDV İstisna Tanımları
            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeKdvIstisnaTanimlariYonetimi' AND [Name] = N'Menu' AND [IsDeleted] = 0 ORDER BY [CreatedAt];
            IF @KdvIstisnaTanimlariMenuItemId IS NOT NULL AND @MenuRoleId IS NOT NULL
            BEGIN
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                WHERE mir.[MenuItemId] = @KdvIstisnaTanimlariMenuItemId
                  AND r.[Domain] = N'MuhasebeFisYonetimi'
                  AND r.[Name] = N'View';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KdvIstisnaTanimlariMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @KdvIstisnaTanimlariMenuItemId, @MenuRoleId, 0, @Now, @Now);
            END;

            -- KDV Hareket Raporu
            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeKdvHareketRaporuYonetimi' AND [Name] = N'Menu' AND [IsDeleted] = 0 ORDER BY [CreatedAt];
            IF @KdvHareketRaporuMenuItemId IS NOT NULL AND @MenuRoleId IS NOT NULL
            BEGIN
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                WHERE mir.[MenuItemId] = @KdvHareketRaporuMenuItemId
                  AND r.[Domain] = N'MuhasebeFisYonetimi'
                  AND r.[Name] = N'View';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KdvHareketRaporuMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @KdvHareketRaporuMenuItemId, @MenuRoleId, 0, @Now, @Now);
            END;

            -- KDV Özet Raporu
            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeKdvOzetRaporuYonetimi' AND [Name] = N'Menu' AND [IsDeleted] = 0 ORDER BY [CreatedAt];
            IF @KdvOzetRaporuMenuItemId IS NOT NULL AND @MenuRoleId IS NOT NULL
            BEGIN
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                WHERE mir.[MenuItemId] = @KdvOzetRaporuMenuItemId
                  AND r.[Domain] = N'MuhasebeFisYonetimi'
                  AND r.[Name] = N'View';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KdvOzetRaporuMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @KdvOzetRaporuMenuItemId, @MenuRoleId, 0, @Now, @Now);
            END;

            -- KDV Beyanname Hazırlık Kontrolü
            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeKdvBeyannameHazirlikKontrolYonetimi' AND [Name] = N'Menu' AND [IsDeleted] = 0 ORDER BY [CreatedAt];
            IF @KdvBeyannameHazirlikKontrolMenuItemId IS NOT NULL AND @MenuRoleId IS NOT NULL
            BEGIN
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                WHERE mir.[MenuItemId] = @KdvBeyannameHazirlikKontrolMenuItemId
                  AND r.[Domain] = N'MuhasebeFisYonetimi'
                  AND r.[Name] = N'View';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KdvBeyannameHazirlikKontrolMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @KdvBeyannameHazirlikKontrolMenuItemId, @MenuRoleId, 0, @Now, @Now);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @DashboardMenuItemId uniqueidentifier;
            DECLARE @SatisBelgeleriMenuItemId uniqueidentifier;
            DECLARE @AlisBelgeleriMenuItemId uniqueidentifier;
            DECLARE @KdvIstisnaTanimlariMenuItemId uniqueidentifier;
            DECLARE @KdvHareketRaporuMenuItemId uniqueidentifier;
            DECLARE @KdvOzetRaporuMenuItemId uniqueidentifier;
            DECLARE @KdvBeyannameHazirlikKontrolMenuItemId uniqueidentifier;

            SELECT TOP (1) @DashboardMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/dashboard' AND [IsDeleted] = 0;
            SELECT TOP (1) @SatisBelgeleriMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/satis-belgeleri' AND [IsDeleted] = 0;
            SELECT TOP (1) @AlisBelgeleriMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/alis-belgeleri' AND [IsDeleted] = 0;
            SELECT TOP (1) @KdvIstisnaTanimlariMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/kdv-istisna-tanimlari' AND [IsDeleted] = 0;
            SELECT TOP (1) @KdvHareketRaporuMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/kdv-hareket-raporu' AND [IsDeleted] = 0;
            SELECT TOP (1) @KdvOzetRaporuMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/kdv-ozet-raporu' AND [IsDeleted] = 0;
            SELECT TOP (1) @KdvBeyannameHazirlikKontrolMenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/kdv-beyanname-hazirlik-kontrol' AND [IsDeleted] = 0;

            DECLARE @RoleIds TABLE ([RoleId] uniqueidentifier NOT NULL);
            INSERT INTO @RoleIds ([RoleId])
            SELECT [Id]
            FROM [TODBase].[Roles]
            WHERE ([Domain] = N'MuhasebeDashboardYonetimi' AND [Name] IN (N'Menu', N'View'))
               OR ([Domain] = N'MuhasebeSatisBelgeleriYonetimi' AND [Name] IN (N'Menu', N'View', N'Manage'))
               OR ([Domain] = N'MuhasebeAlisBelgeleriYonetimi' AND [Name] IN (N'Menu', N'View', N'Manage'))
               OR ([Domain] = N'MuhasebeKdvIstisnaTanimlariYonetimi' AND [Name] IN (N'Menu', N'View', N'Manage'))
               OR ([Domain] = N'MuhasebeKdvHareketRaporuYonetimi' AND [Name] IN (N'Menu', N'View'))
               OR ([Domain] = N'MuhasebeKdvOzetRaporuYonetimi' AND [Name] IN (N'Menu', N'View'))
               OR ([Domain] = N'MuhasebeKdvBeyannameHazirlikKontrolYonetimi' AND [Name] IN (N'Menu', N'View'));

            DELETE mir
            FROM [TODBase].[MenuItemRoles] mir
            INNER JOIN @RoleIds rr ON rr.[RoleId] = mir.[RoleId];

            DELETE ugr
            FROM [TODBase].[UserGroupRoles] ugr
            INNER JOIN @RoleIds rr ON rr.[RoleId] = ugr.[RoleId];

            DELETE FROM [TODBase].[Roles]
            WHERE [Id] IN (SELECT [RoleId] FROM @RoleIds);

            -- Restore the previous MuhasebeFisYonetimi.View mapping for the affected menu items.
            DECLARE @LegacyViewRoleId uniqueidentifier = (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'MuhasebeFisYonetimi' AND [Name] = N'View' AND [IsDeleted] = 0
                ORDER BY [CreatedAt]
            );

            IF @LegacyViewRoleId IS NOT NULL
            BEGIN
                IF @DashboardMenuItemId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @DashboardMenuItemId AND [RoleId] = @LegacyViewRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @DashboardMenuItemId, @LegacyViewRoleId, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

                IF @SatisBelgeleriMenuItemId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @SatisBelgeleriMenuItemId AND [RoleId] = @LegacyViewRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @SatisBelgeleriMenuItemId, @LegacyViewRoleId, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

                IF @AlisBelgeleriMenuItemId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @AlisBelgeleriMenuItemId AND [RoleId] = @LegacyViewRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AlisBelgeleriMenuItemId, @LegacyViewRoleId, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

                IF @KdvIstisnaTanimlariMenuItemId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KdvIstisnaTanimlariMenuItemId AND [RoleId] = @LegacyViewRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @KdvIstisnaTanimlariMenuItemId, @LegacyViewRoleId, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

                IF @KdvHareketRaporuMenuItemId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KdvHareketRaporuMenuItemId AND [RoleId] = @LegacyViewRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @KdvHareketRaporuMenuItemId, @LegacyViewRoleId, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

                IF @KdvOzetRaporuMenuItemId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KdvOzetRaporuMenuItemId AND [RoleId] = @LegacyViewRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @KdvOzetRaporuMenuItemId, @LegacyViewRoleId, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

                IF @KdvBeyannameHazirlikKontrolMenuItemId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KdvBeyannameHazirlikKontrolMenuItemId AND [RoleId] = @LegacyViewRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @KdvBeyannameHazirlikKontrolMenuItemId, @LegacyViewRoleId, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
            END;
            """);
    }
}
