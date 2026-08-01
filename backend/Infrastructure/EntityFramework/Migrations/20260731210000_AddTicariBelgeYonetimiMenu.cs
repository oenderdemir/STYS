using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260731210000_AddTicariBelgeYonetimiMenu")]
public partial class AddTicariBelgeYonetimiMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();

            -- TicariBelgeYonetimi.Menu: yalnızca menü görünürlüğü için (View/Manage önceki
            -- AddTicariBelgeYonetimiPermission migration'ında tanımlandı - o dosyaya DOKUNULMAZ).
            -- Bu turda da hiçbir kullanıcı/gruba OTOMATİK yetki atanmaz (bkz. görev J).
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'TicariBelgeYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), N'Menu', N'TicariBelgeYonetimi', 0, @Now, @Now);

            DECLARE @MenuRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'TicariBelgeYonetimi' AND [Name] = N'Menu'
            );

            -- "Satış Yönetimi" kök menüsü altına - operasyon personeline yönelik bu ekran
            -- "Muhasebe" kök menüsünün ALTINA KONULMAZ (ayrı yetki/ayrı ekran, bkz. görev K).
            DECLARE @SatisYonetimiRootId uniqueidentifier;
            SELECT TOP (1) @SatisYonetimiRootId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Satış Yönetimi' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            DECLARE @TicariBelgelerMenuItemId uniqueidentifier;
            SELECT TOP (1) @TicariBelgelerMenuItemId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'ticari-belgeler' AND [IsDeleted] = 0;

            IF @TicariBelgelerMenuItemId IS NULL
            BEGIN
                SET @TicariBelgelerMenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@TicariBelgelerMenuItemId, N'Ticari Belgeler', N'pi pi-file-edit', N'ticari-belgeler', NULL, @SatisYonetimiRootId, 1, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Ticari Belgeler',
                    [Icon] = N'pi pi-file-edit',
                    [ParentId] = @SatisYonetimiRootId,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now
                WHERE [Id] = @TicariBelgelerMenuItemId;
            END;

            IF NOT EXISTS (
                SELECT 1 FROM [TODBase].[MenuItemRoles]
                WHERE [MenuItemId] = @TicariBelgelerMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0
            )
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @TicariBelgelerMenuItemId, @MenuRoleId, 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 1, [DeletedAt] = SYSUTCDATETIME(), [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Route] = N'ticari-belgeler' AND [IsDeleted] = 0;

            DECLARE @MenuRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'TicariBelgeYonetimi' AND [Name] = N'Menu'
            );

            UPDATE [TODBase].[MenuItemRoles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [RoleId] = @MenuRoleId AND [IsDeleted] = 0;

            UPDATE [TODBase].[Roles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Domain] = N'TicariBelgeYonetimi' AND [Name] = N'Menu' AND [IsDeleted] = 0;
            """);
    }
}
