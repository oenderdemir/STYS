using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260620150000_AddOdaRezervasyonTakvimi")]
public partial class AddOdaRezervasyonTakvimi : Migration
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

            -- Permission rolleri: OdaRezervasyonTakvimiYonetimi.Menu ve .View
            DECLARE @RequiredRoles TABLE ([Domain] nvarchar(128) NOT NULL, [Name] nvarchar(64) NOT NULL);
            INSERT INTO @RequiredRoles ([Domain], [Name]) VALUES
                (N'OdaRezervasyonTakvimiYonetimi', N'Menu'),
                (N'OdaRezervasyonTakvimiYonetimi', N'View');

            INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), rr.[Name], rr.[Domain], 0, @Now, @Now
            FROM @RequiredRoles rr
            WHERE NOT EXISTS (
                SELECT 1 FROM [TODBase].[Roles] r
                WHERE r.[Domain] = rr.[Domain] AND r.[Name] = rr.[Name]
            );

            DECLARE @MenuRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'OdaRezervasyonTakvimiYonetimi' AND [Name] = N'Menu'
            );
            DECLARE @ViewRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'OdaRezervasyonTakvimiYonetimi' AND [Name] = N'View'
            );

            -- Admin ve TesisYoneticisi gruplarına Menu + View ata
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

            -- Rezervasyon Yönetimi menü grubu (parent folder) - varsa bul yoksa oluştur
            DECLARE @RezervasyonMenuGrupId uniqueidentifier;
            SELECT TOP (1) @RezervasyonMenuGrupId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'' AND [Label] = N'Rezervasyon Yönetimi' AND [IsDeleted] = 0;

            IF @RezervasyonMenuGrupId IS NULL
            BEGIN
                SET @RezervasyonMenuGrupId = NEWID();
                DECLARE @AnaMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RezervasyonMenuGrupId, N'Rezervasyon Yönetimi', N'fa-solid fa-calendar-check', N'', NULL, @AnaMenuId, 1, 0, @Now, @Now);

                -- Mevcut "Rezervasyon Yönetimi" leaf item'ı bu grubun altına taşı
                UPDATE [TODBase].[MenuItems]
                SET [ParentId] = @RezervasyonMenuGrupId,
                    [Label] = N'Rezervasyonlar',
                    [MenuOrder] = 0,
                    [UpdatedAt] = @Now
                WHERE [Id] = '0991feca-8df8-4e06-a253-941efe84a818' AND [IsDeleted] = 0;
            END;

            -- Grup menü item'a RezervasyonYonetimi.Menu rolünü bağla (görünürlük için)
            DECLARE @RezMenuRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'RezervasyonYonetimi' AND [Name] = N'Menu'
            );
            IF @RezMenuRoleId IS NOT NULL
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM [TODBase].[MenuItemRoles]
                    WHERE [MenuItemId] = @RezervasyonMenuGrupId AND [RoleId] = @RezMenuRoleId AND [IsDeleted] = 0
                )
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @RezervasyonMenuGrupId, @RezMenuRoleId, 0, @Now, @Now);
            END;

            -- Grup menü item'a OdaRezervasyonTakvimiYonetimi.Menu rolünü de bağla
            IF NOT EXISTS (
                SELECT 1 FROM [TODBase].[MenuItemRoles]
                WHERE [MenuItemId] = @RezervasyonMenuGrupId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0
            )
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @RezervasyonMenuGrupId, @MenuRoleId, 0, @Now, @Now);

            -- Oda Rezervasyon Takvimi menu item
            DECLARE @OdaTakvimiMenuItemId uniqueidentifier;
            SELECT TOP (1) @OdaTakvimiMenuItemId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'oda-rezervasyon-takvimi' AND [IsDeleted] = 0;

            IF @OdaTakvimiMenuItemId IS NULL
            BEGIN
                SET @OdaTakvimiMenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@OdaTakvimiMenuItemId, N'Oda Rezervasyon Takvimi', N'pi pi-calendar', N'oda-rezervasyon-takvimi', NULL, @RezervasyonMenuGrupId, 1, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Oda Rezervasyon Takvimi',
                    [Icon] = N'pi pi-calendar',
                    [ParentId] = @RezervasyonMenuGrupId,
                    [MenuOrder] = 1,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now
                WHERE [Id] = @OdaTakvimiMenuItemId;
            END;

            -- Menü görünürlük rolü: OdaRezervasyonTakvimiYonetimi.Menu
            IF NOT EXISTS (
                SELECT 1 FROM [TODBase].[MenuItemRoles]
                WHERE [MenuItemId] = @OdaTakvimiMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0
            )
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @OdaTakvimiMenuItemId, @MenuRoleId, 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 1, [DeletedAt] = SYSUTCDATETIME(), [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Route] = N'oda-rezervasyon-takvimi' AND [IsDeleted] = 0;

            DECLARE @MenuRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'OdaRezervasyonTakvimiYonetimi' AND [Name] = N'Menu'
            );
            DECLARE @ViewRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'OdaRezervasyonTakvimiYonetimi' AND [Name] = N'View'
            );

            UPDATE [TODBase].[UserGroupRoles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [RoleId] IN (@MenuRoleId, @ViewRoleId) AND [IsDeleted] = 0;

            UPDATE [TODBase].[Roles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Domain] = N'OdaRezervasyonTakvimiYonetimi' AND [IsDeleted] = 0;
            """);
    }
}
