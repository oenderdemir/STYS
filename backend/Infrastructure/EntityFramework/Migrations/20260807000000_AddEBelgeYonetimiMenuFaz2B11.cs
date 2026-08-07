using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

/// <summary>
/// Faz 2B.11 görev md.4 - "E-Belge Yönetimi" için DB-driven menu kaydı. Mevcut
/// `StructurePermissions.MuhasebeSatisBelgeleriYonetimi` rol/izin ailesi (Menu/View/Manage)
/// ZATEN `20260619123000_AddMuhasebeSpecificMenuPermissions` migration'ında OLUŞTURULMUŞTUR - bu
/// migration YENİ bir rol/izin İCAT ETMEZ, yalnız mevcut `.Menu` rolünü YENİ menu item'a bağlar
/// (`AddSatisBelgeleriMenu`/`AddTicariBelgeYonetimiMenu` İLE AYNI, kanıtlanmış idempotent desen).
/// </summary>
public partial class AddEBelgeYonetimiMenuFaz2B11 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @MuhasebeRootId uniqueidentifier;
            SELECT TOP (1) @MuhasebeRootId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            IF @MuhasebeRootId IS NULL
            BEGIN
                SET @MuhasebeRootId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id],[Label],[Icon],[Route],[ParentId],[MenuOrder],[IsDeleted],[CreatedAt],[UpdatedAt])
                VALUES (@MuhasebeRootId, N'Muhasebe', N'pi pi-wallet', N'', NULL, 6, 0, @Now, @Now);
            END;

            DECLARE @MenuItemId uniqueidentifier;
            SELECT TOP (1) @MenuItemId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/e-belge-yonetimi' AND [IsDeleted] = 0;

            IF @MenuItemId IS NULL
            BEGIN
                SET @MenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id],[Label],[Icon],[Route],[ParentId],[MenuOrder],[IsDeleted],[CreatedAt],[UpdatedAt])
                VALUES (@MenuItemId, N'E-Belge Yönetimi', N'pi pi-file-edit', N'muhasebe/e-belge-yonetimi', @MuhasebeRootId, 13, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'E-Belge Yönetimi', [Icon] = N'pi pi-file-edit', [ParentId] = @MuhasebeRootId,
                    [MenuOrder] = 13, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @MenuItemId;
            END;

            -- Faz 2B.11 görev md.4 - YENİ bir rol/izin OLUŞTURULMAZ; mevcut
            -- `MuhasebeSatisBelgeleriYonetimi.Menu` rolü (20260619123000'de zaten var) YENİDEN KULLANILIR.
            DECLARE @MenuRoleId uniqueidentifier;
            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles]
            WHERE [Domain] = N'MuhasebeSatisBelgeleriYonetimi' AND [Name] = N'Menu' AND [IsDeleted] = 0
            ORDER BY [CreatedAt];

            IF @MenuItemId IS NOT NULL AND @MenuRoleId IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id],[MenuItemId],[RoleId],[IsDeleted],[CreatedAt],[UpdatedAt])
                    VALUES (NEWID(), @MenuItemId, @MenuRoleId, 0, @Now, @Now);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @MenuItemId uniqueidentifier;
            SELECT TOP (1) @MenuItemId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/e-belge-yonetimi' AND [IsDeleted] = 0;

            IF @MenuItemId IS NOT NULL
            BEGIN
                UPDATE [TODBase].[MenuItemRoles]
                SET [IsDeleted] = 1, [DeletedAt] = @Now, [UpdatedAt] = @Now
                WHERE [MenuItemId] = @MenuItemId;

                UPDATE [TODBase].[MenuItems]
                SET [IsDeleted] = 1, [DeletedAt] = @Now, [UpdatedAt] = @Now
                WHERE [Id] = @MenuItemId;
            END;
            """);
    }
}
