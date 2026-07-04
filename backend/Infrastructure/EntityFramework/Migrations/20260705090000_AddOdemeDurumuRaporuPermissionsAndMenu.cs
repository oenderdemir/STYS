using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260705090000_AddOdemeDurumuRaporuPermissionsAndMenu")]
public partial class AddOdemeDurumuRaporuPermissionsAndMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @ResepsiyonistGroupId uniqueidentifier = (
                SELECT TOP (1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'ResepsiyonistGrubu' AND [IsDeleted] = 0
                ORDER BY [CreatedAt]
            );

            -- Permission rolleri: OdemeDurumuRaporuYonetimi.Menu ve .View
            DECLARE @RequiredRoles TABLE ([Domain] nvarchar(128) NOT NULL, [Name] nvarchar(64) NOT NULL);
            INSERT INTO @RequiredRoles ([Domain], [Name]) VALUES
                (N'OdemeDurumuRaporuYonetimi', N'Menu'),
                (N'OdemeDurumuRaporuYonetimi', N'View');

            INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), rr.[Name], rr.[Domain], 0, @Now, @Now
            FROM @RequiredRoles rr
            WHERE NOT EXISTS (
                SELECT 1 FROM [TODBase].[Roles] r
                WHERE r.[Domain] = rr.[Domain] AND r.[Name] = rr.[Name]
            );

            DECLARE @MenuRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'OdemeDurumuRaporuYonetimi' AND [Name] = N'Menu'
            );
            DECLARE @ViewRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'OdemeDurumuRaporuYonetimi' AND [Name] = N'View'
            );

            -- Admin, TesisYoneticisi ve Resepsiyonist gruplarina Menu + View ata
            DECLARE @Groups TABLE ([GroupId] uniqueidentifier NOT NULL);
            INSERT INTO @Groups ([GroupId])
            SELECT [Id] FROM (VALUES (@AdminGroupId), (@TesisManagerGroupId)) AS v([Id])
            WHERE [Id] IS NOT NULL
              AND EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = v.[Id] AND [IsDeleted] = 0);

            IF @ResepsiyonistGroupId IS NOT NULL
                INSERT INTO @Groups ([GroupId]) VALUES (@ResepsiyonistGroupId);

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), g.[GroupId], r.[RoleId], 0, @Now, @Now
            FROM @Groups g
            CROSS JOIN (SELECT @MenuRoleId AS [RoleId] UNION ALL SELECT @ViewRoleId) r
            WHERE NOT EXISTS (
                SELECT 1 FROM [TODBase].[UserGroupRoles] ugr
                WHERE ugr.[UserGroupId] = g.[GroupId]
                  AND ugr.[RoleId] = r.[RoleId]
                  AND ugr.[IsDeleted] = 0
            );

            -- Raporlar menü grubu (parent folder) - varsa bul yoksa oluştur
            DECLARE @RaporlarMenuGrupId uniqueidentifier;
            SELECT TOP (1) @RaporlarMenuGrupId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'' AND [Label] = N'Raporlar' AND [IsDeleted] = 0;

            IF @RaporlarMenuGrupId IS NULL
            BEGIN
                SET @RaporlarMenuGrupId = NEWID();
                DECLARE @AnaMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RaporlarMenuGrupId, N'Raporlar', N'fa-solid fa-chart-column', N'', NULL, @AnaMenuId, 2, 0, @Now, @Now);
            END;

            -- Grup menü item'a OdemeDurumuRaporuYonetimi.Menu rolünü bağla (görünürlük için)
            IF NOT EXISTS (
                SELECT 1 FROM [TODBase].[MenuItemRoles]
                WHERE [MenuItemId] = @RaporlarMenuGrupId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0
            )
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @RaporlarMenuGrupId, @MenuRoleId, 0, @Now, @Now);

            -- Odeme Durumu / Borclu Rezervasyonlar Raporu menu item
            DECLARE @OdemeDurumuMenuItemId uniqueidentifier;
            SELECT TOP (1) @OdemeDurumuMenuItemId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'raporlar/odeme-durumu' AND [IsDeleted] = 0;

            IF @OdemeDurumuMenuItemId IS NULL
            BEGIN
                SET @OdemeDurumuMenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@OdemeDurumuMenuItemId, N'Odeme Durumu / Borclu Rezervasyonlar', N'pi pi-credit-card', N'raporlar/odeme-durumu', NULL, @RaporlarMenuGrupId, 3, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Odeme Durumu / Borclu Rezervasyonlar',
                    [Icon] = N'pi pi-credit-card',
                    [ParentId] = @RaporlarMenuGrupId,
                    [MenuOrder] = 3,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now
                WHERE [Id] = @OdemeDurumuMenuItemId;
            END;

            -- Menü görünürlük rolü: OdemeDurumuRaporuYonetimi.Menu
            IF NOT EXISTS (
                SELECT 1 FROM [TODBase].[MenuItemRoles]
                WHERE [MenuItemId] = @OdemeDurumuMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0
            )
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @OdemeDurumuMenuItemId, @MenuRoleId, 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 1, [DeletedAt] = SYSUTCDATETIME(), [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Route] = N'raporlar/odeme-durumu' AND [IsDeleted] = 0;

            DECLARE @MenuRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'OdemeDurumuRaporuYonetimi' AND [Name] = N'Menu'
            );
            DECLARE @ViewRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'OdemeDurumuRaporuYonetimi' AND [Name] = N'View'
            );

            UPDATE [TODBase].[UserGroupRoles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [RoleId] IN (@MenuRoleId, @ViewRoleId) AND [IsDeleted] = 0;

            UPDATE [TODBase].[Roles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Domain] = N'OdemeDurumuRaporuYonetimi' AND [IsDeleted] = 0;
            """);
    }
}
