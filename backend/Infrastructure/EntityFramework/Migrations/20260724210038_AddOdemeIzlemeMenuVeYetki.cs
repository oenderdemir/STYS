using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddOdemeIzlemeMenuVeYetki : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
                DECLARE @MuhasebeciGroupId uniqueidentifier = (
                    SELECT TOP (1) [Id] FROM [TODBase].[UserGroups]
                    WHERE [Name] IN (N'MuhasebeciGrubu', N'muhasebe-yoneticileri-grubu') AND [IsDeleted] = 0
                    ORDER BY [CreatedAt]
                );

                -- Permission rolleri: OdemeIzlemeYonetimi.Menu / .View (salt-okunur ekran - Manage yok)
                DECLARE @RequiredRoles TABLE ([Domain] nvarchar(128) NOT NULL, [Name] nvarchar(64) NOT NULL);
                INSERT INTO @RequiredRoles ([Domain], [Name]) VALUES
                    (N'OdemeIzlemeYonetimi', N'Menu'),
                    (N'OdemeIzlemeYonetimi', N'View');

                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), rr.[Name], rr.[Domain], 0, @Now, @Now
                FROM @RequiredRoles rr
                WHERE NOT EXISTS (
                    SELECT 1 FROM [TODBase].[Roles] r
                    WHERE r.[Domain] = rr.[Domain] AND r.[Name] = rr.[Name]
                );

                DECLARE @MenuRoleId uniqueidentifier = (
                    SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                    WHERE [Domain] = N'OdemeIzlemeYonetimi' AND [Name] = N'Menu'
                );
                DECLARE @ViewRoleId uniqueidentifier = (
                    SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                    WHERE [Domain] = N'OdemeIzlemeYonetimi' AND [Name] = N'View'
                );

                -- Admin ve TesisYoneticisi gruplarina Menu+View; Muhasebeci grubuna da ayni yetkiler
                -- (bu ekran hassas cari/IBAN detayi gosterdigi icin genis kullanici gruplarina
                -- ACILMAZ - yalnizca finans/muhasebe yetkisine sahip gruplar).
                DECLARE @Groups TABLE ([GroupId] uniqueidentifier NOT NULL);
                INSERT INTO @Groups ([GroupId])
                SELECT [Id] FROM (VALUES (@AdminGroupId), (@TesisManagerGroupId)) AS v([Id])
                WHERE [Id] IS NOT NULL
                  AND EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = v.[Id] AND [IsDeleted] = 0);

                IF @MuhasebeciGroupId IS NOT NULL
                    INSERT INTO @Groups ([GroupId]) VALUES (@MuhasebeciGroupId);

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

                -- Muhasebe kok menusu
                DECLARE @MuhasebeRootId uniqueidentifier;
                SELECT TOP (1) @MuhasebeRootId = [Id] FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

                -- Odeme Izleme menu item
                DECLARE @IzlemeMenuItemId uniqueidentifier;
                SELECT TOP (1) @IzlemeMenuItemId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/odeme-izleme' AND [IsDeleted] = 0;

                IF @IzlemeMenuItemId IS NULL
                BEGIN
                    SET @IzlemeMenuItemId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@IzlemeMenuItemId, N'Ödeme İzleme', N'pi pi-search-plus', N'muhasebe/odeme-izleme', NULL, @MuhasebeRootId, 5, 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Ödeme İzleme',
                        [Icon] = N'pi pi-search-plus',
                        [ParentId] = @MuhasebeRootId,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @IzlemeMenuItemId;
                END;

                IF NOT EXISTS (
                    SELECT 1 FROM [TODBase].[MenuItemRoles]
                    WHERE [MenuItemId] = @IzlemeMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0
                )
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @IzlemeMenuItemId, @MenuRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                UPDATE [TODBase].[MenuItems]
                SET [IsDeleted] = 1, [DeletedAt] = SYSUTCDATETIME(), [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Route] = N'muhasebe/odeme-izleme' AND [IsDeleted] = 0;

                DECLARE @MenuRoleId uniqueidentifier = (
                    SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                    WHERE [Domain] = N'OdemeIzlemeYonetimi' AND [Name] = N'Menu'
                );
                DECLARE @ViewRoleId uniqueidentifier = (
                    SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                    WHERE [Domain] = N'OdemeIzlemeYonetimi' AND [Name] = N'View'
                );

                UPDATE [TODBase].[UserGroupRoles]
                SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
                WHERE [RoleId] IN (@MenuRoleId, @ViewRoleId) AND [IsDeleted] = 0;

                UPDATE [TODBase].[Roles]
                SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Domain] = N'OdemeIzlemeYonetimi' AND [IsDeleted] = 0;
                """);
        }
    }
}
