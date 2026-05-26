using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260526000001_AddAlisBelgeleriMenu")]
public partial class AddAlisBelgeleriMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            -- TODO: This currently reuses the MuhasebeFisYonetimi.View role.
            -- When a more semantic menu permission is introduced, remap this seed accordingly.
            DECLARE @ViewRoleId uniqueidentifier;
            SELECT TOP (1) @ViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeFisYonetimi' AND [Name] = N'View';

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

            DECLARE @MenuItemId uniqueidentifier;
            SELECT TOP (1) @MenuItemId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/alis-belgeleri' AND [IsDeleted] = 0;

            IF @MenuItemId IS NULL
            BEGIN
                SET @MenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MenuItemId, N'Alış Belgeleri', N'pi pi-file', N'muhasebe/alis-belgeleri', @MuhasebeRootId, 13, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Alış Belgeleri',
                    [Icon] = N'pi pi-file',
                    [ParentId] = @MuhasebeRootId,
                    [MenuOrder] = 13,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now
                WHERE [Id] = @MenuItemId;
            END;

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
            WHERE mi.[Route] = N'muhasebe/alis-belgeleri';

            DELETE FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/alis-belgeleri';
            """);
    }
}
