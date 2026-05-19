using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260519210000_AddYevmiyeMuavinDefterMenu")]
public partial class AddYevmiyeMuavinDefterMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @MenuRoleId uniqueidentifier;

            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeFisYonetimi' AND [Name] = N'Menu' AND [IsDeleted] = 0;

            -- Muhasebe root bul veya olustur
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

            -- Yevmiye Defteri MenuItem
            DECLARE @YevmiyeDefteriId uniqueidentifier;

            SELECT TOP (1) @YevmiyeDefteriId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/yevmiye-defteri' AND [IsDeleted] = 0;

            IF @YevmiyeDefteriId IS NULL
            BEGIN
                SET @YevmiyeDefteriId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@YevmiyeDefteriId, N'Yevmiye Defteri', N'pi pi-book', N'muhasebe/yevmiye-defteri', @MuhasebeRootId, 15, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Yevmiye Defteri',
                    [Icon] = N'pi pi-book',
                    [ParentId] = @MuhasebeRootId,
                    [MenuOrder] = 15,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now
                WHERE [Id] = @YevmiyeDefteriId;
            END;

            -- Muavin Defter MenuItem
            DECLARE @MuavinDefterId uniqueidentifier;

            SELECT TOP (1) @MuavinDefterId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/muavin-defter' AND [IsDeleted] = 0;

            IF @MuavinDefterId IS NULL
            BEGIN
                SET @MuavinDefterId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MuavinDefterId, N'Muavin Defter', N'pi pi-list-check', N'muhasebe/muavin-defter', @MuhasebeRootId, 16, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Muavin Defter',
                    [Icon] = N'pi pi-list-check',
                    [ParentId] = @MuhasebeRootId,
                    [MenuOrder] = 16,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now
                WHERE [Id] = @MuavinDefterId;
            END;

            -- MenuItemRoles
            IF @MenuRoleId IS NOT NULL AND @MuhasebeRootId IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @MenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @YevmiyeDefteriId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @YevmiyeDefteriId, @MenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuavinDefterId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuavinDefterId, @MenuRoleId, 0, @Now, @Now);
            END;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            -- Sadece Yevmiye Defteri ve Muavin Defter MenuItem ve MenuItemRoles kayitlarini temizle.
            -- MuhasebeFisYonetimi rolleri diger muhasebe fis sayfalari tarafindan da
            -- kullanildigi icin Roller ve UserGroupRoles tablolarina dokunulmaz.

            DELETE mir
            FROM [TODBase].[MenuItemRoles] mir
            INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
            WHERE mi.[Route] IN (N'muhasebe/yevmiye-defteri', N'muhasebe/muavin-defter');

            DELETE FROM [TODBase].[MenuItems]
            WHERE [Route] IN (N'muhasebe/yevmiye-defteri', N'muhasebe/muavin-defter');
            """);
    }
}
