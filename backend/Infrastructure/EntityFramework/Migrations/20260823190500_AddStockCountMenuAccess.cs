using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddStockCountMenuAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @StokHareketMenuRoleId uniqueidentifier;
                DECLARE @MuhasebeRootId uniqueidentifier;
                DECLARE @StokSayimlariMenuId uniqueidentifier;

                SELECT TOP (1) @StokHareketMenuRoleId = [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'StokHareketYonetimi' AND [Name] = N'Menu';

                IF @StokHareketMenuRoleId IS NULL
                BEGIN
                    SET @StokHareketMenuRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@StokHareketMenuRoleId, N'Menu', N'StokHareketYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @MuhasebeRootId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

                IF @MuhasebeRootId IS NULL
                BEGIN
                    SET @MuhasebeRootId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MuhasebeRootId, N'Muhasebe', N'pi pi-wallet', N'', NULL, 6, 0, @Now, @Now);
                END;

                SELECT TOP (1) @StokSayimlariMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/stok-sayimlari';

                IF @StokSayimlariMenuId IS NULL
                BEGIN
                    SET @StokSayimlariMenuId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@StokSayimlariMenuId, N'Stok Sayımları', N'pi pi-verified', N'muhasebe/stok-sayimlari', @MuhasebeRootId, 9, 0, @Now, @Now);
                END;
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Stok Sayımları',
                        [Icon] = N'pi pi-verified',
                        [ParentId] = @MuhasebeRootId,
                        [MenuOrder] = 9,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @StokSayimlariMenuId;
                END;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @StokHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @MuhasebeRootId, @StokHareketMenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @StokSayimlariMenuId AND [RoleId] = @StokHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @StokSayimlariMenuId, @StokHareketMenuRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                WHERE mi.[Route] = N'muhasebe/stok-sayimlari';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/stok-sayimlari';
                """);
        }
    }
}
