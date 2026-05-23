using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260525000000_AddKdvBeyannameHazirlikKontrolMenu")]
public partial class AddKdvBeyannameHazirlikKontrolMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            -- Mevcut MuhasebeFisYonetimi.View rolünü al (zaten var)
            DECLARE @ViewRoleId uniqueidentifier;
            SELECT TOP (1) @ViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeFisYonetimi' AND [Name] = N'View';

            -- Muhasebe root MenuItem (zaten olmalı)
            DECLARE @MuhasebeRootId uniqueidentifier;
            SELECT TOP (1) @MuhasebeRootId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            IF @MuhasebeRootId IS NULL
            BEGIN
                SET @MuhasebeRootId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MuhasebeRootId, N'Muhasebe', N'pi pi-wallet', N'', NULL, 6, 0, @Now, @Now);
            END;

            -- KDV Beyanname Hazırlık Kontrolü MenuItem
            DECLARE @MenuItemId uniqueidentifier;
            SELECT TOP (1) @MenuItemId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/kdv-beyanname-hazirlik-kontrol' AND [IsDeleted] = 0;

            IF @MenuItemId IS NULL
            BEGIN
                SET @MenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MenuItemId, N'KDV Beyanname Hazırlık Kontrolü', N'pi pi-check-square', N'muhasebe/kdv-beyanname-hazirlik-kontrol', @MuhasebeRootId, 11, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'KDV Beyanname Hazırlık Kontrolü',
                    [Icon] = N'pi pi-check-square',
                    [ParentId] = @MuhasebeRootId,
                    [MenuOrder] = 11,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now
                WHERE [Id] = @MenuItemId;
            END;

            -- MenuItemRoles: sadece View rolü (MuhasebeFisYonetimi.View)
            IF @ViewRoleId IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @ViewRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @MenuItemId, @ViewRoleId, 0, @Now, @Now);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE mir
            FROM [TODBase].[MenuItemRoles] mir
            INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
            WHERE mi.[Route] = N'muhasebe/kdv-beyanname-hazirlik-kontrol';

            DELETE FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/kdv-beyanname-hazirlik-kontrol';
            """);
    }
}
