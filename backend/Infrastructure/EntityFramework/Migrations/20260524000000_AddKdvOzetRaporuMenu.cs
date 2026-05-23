using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260524000000_AddKdvOzetRaporuMenu")]
public partial class AddKdvOzetRaporuMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @MuhasebeciGroupId uniqueidentifier;

            SELECT TOP (1) @MuhasebeciGroupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'MuhasebeciGrubu' AND [IsDeleted] = 0;

            -- Mevcut MuhasebeFisYonetimi.View rolünü al (zaten var)
            DECLARE @ViewRoleId uniqueidentifier;
            SELECT TOP (1) @ViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeFisYonetimi' AND [Name] = N'View';

            -- Muhasebe root MenuItem (zaten olmalı, yoksa olustur)
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

            -- KDV Özet Raporu MenuItem
            DECLARE @MenuItemId uniqueidentifier;
            SELECT TOP (1) @MenuItemId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/kdv-ozet-raporu' AND [IsDeleted] = 0;

            IF @MenuItemId IS NULL
            BEGIN
                SET @MenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MenuItemId, N'KDV Özet Raporu', N'pi pi-chart-bar', N'muhasebe/kdv-ozet-raporu', @MuhasebeRootId, 10, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'KDV Özet Raporu',
                    [Icon] = N'pi pi-chart-bar',
                    [ParentId] = @MuhasebeRootId,
                    [MenuOrder] = 10,
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
            WHERE mi.[Route] = N'muhasebe/kdv-ozet-raporu';

            DELETE FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/kdv-ozet-raporu';
            """);
    }
}
