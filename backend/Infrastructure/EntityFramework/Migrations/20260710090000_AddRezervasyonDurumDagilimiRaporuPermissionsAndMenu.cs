using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260710090000_AddRezervasyonDurumDagilimiRaporuPermissionsAndMenu")]
public partial class AddRezervasyonDurumDagilimiRaporuPermissionsAndMenu : Migration
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

            -- Permission rolleri: RezervasyonDurumDagilimiRaporuYonetimi.Menu ve .View
            DECLARE @RequiredRoles TABLE ([Domain] nvarchar(128) NOT NULL, [Name] nvarchar(64) NOT NULL);
            INSERT INTO @RequiredRoles ([Domain], [Name]) VALUES
                (N'RezervasyonDurumDagilimiRaporuYonetimi', N'Menu'),
                (N'RezervasyonDurumDagilimiRaporuYonetimi', N'View');

            INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), rr.[Name], rr.[Domain], 0, @Now, @Now
            FROM @RequiredRoles rr
            WHERE NOT EXISTS (
                SELECT 1 FROM [TODBase].[Roles] r
                WHERE r.[Domain] = rr.[Domain] AND r.[Name] = rr.[Name]
            );

            DECLARE @MenuRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'RezervasyonDurumDagilimiRaporuYonetimi' AND [Name] = N'Menu'
            );
            DECLARE @ViewRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'RezervasyonDurumDagilimiRaporuYonetimi' AND [Name] = N'View'
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

            -- Grup menü item'a RezervasyonDurumDagilimiRaporuYonetimi.Menu rolünü bağla (görünürlük için)
            IF NOT EXISTS (
                SELECT 1 FROM [TODBase].[MenuItemRoles]
                WHERE [MenuItemId] = @RaporlarMenuGrupId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0
            )
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @RaporlarMenuGrupId, @MenuRoleId, 0, @Now, @Now);

            -- Rezervasyon Durum Dagilimi Raporu menu item
            DECLARE @RezervasyonDurumDagilimiMenuItemId uniqueidentifier;
            SELECT TOP (1) @RezervasyonDurumDagilimiMenuItemId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'raporlar/rezervasyon-durum-dagilimi' AND [IsDeleted] = 0;

            IF @RezervasyonDurumDagilimiMenuItemId IS NULL
            BEGIN
                SET @RezervasyonDurumDagilimiMenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RezervasyonDurumDagilimiMenuItemId, N'Rezervasyon Durum Dagilimi', N'pi pi-chart-pie', N'raporlar/rezervasyon-durum-dagilimi', NULL, @RaporlarMenuGrupId, 8, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Rezervasyon Durum Dagilimi',
                    [Icon] = N'pi pi-chart-pie',
                    [ParentId] = @RaporlarMenuGrupId,
                    [MenuOrder] = 8,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now
                WHERE [Id] = @RezervasyonDurumDagilimiMenuItemId;
            END;

            -- Menü görünürlük rolü: RezervasyonDurumDagilimiRaporuYonetimi.Menu
            IF NOT EXISTS (
                SELECT 1 FROM [TODBase].[MenuItemRoles]
                WHERE [MenuItemId] = @RezervasyonDurumDagilimiMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0
            )
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @RezervasyonDurumDagilimiMenuItemId, @MenuRoleId, 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 1, [DeletedAt] = SYSUTCDATETIME(), [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Route] = N'raporlar/rezervasyon-durum-dagilimi' AND [IsDeleted] = 0;

            DECLARE @MenuRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'RezervasyonDurumDagilimiRaporuYonetimi' AND [Name] = N'Menu'
            );
            DECLARE @ViewRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'RezervasyonDurumDagilimiRaporuYonetimi' AND [Name] = N'View'
            );

            UPDATE [TODBase].[UserGroupRoles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [RoleId] IN (@MenuRoleId, @ViewRoleId) AND [IsDeleted] = 0;

            UPDATE [TODBase].[Roles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Domain] = N'RezervasyonDurumDagilimiRaporuYonetimi' AND [IsDeleted] = 0;
            """);
    }
}
