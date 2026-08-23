using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLotExpiryWarningsMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @StokHareketMenuRoleId uniqueidentifier;
                DECLARE @MuhasebeRootId uniqueidentifier;
                DECLARE @StokDepoYonetimiId uniqueidentifier;
                DECLARE @SktUyarilariMenuId uniqueidentifier;

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

                SELECT TOP (1) @StokDepoYonetimiId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Stok & Depo Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;

                SELECT TOP (1) @SktUyarilariMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/stok-lotlari/skt-uyarilari';

                IF @SktUyarilariMenuId IS NULL
                BEGIN
                    SET @SktUyarilariMenuId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@SktUyarilariMenuId, N'SKT Uyarıları', N'pi pi-exclamation-triangle', N'muhasebe/stok-lotlari/skt-uyarilari', @StokDepoYonetimiId, 4, 0, @Now, @Now);
                END;
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'SKT Uyarıları',
                        [Icon] = N'pi pi-exclamation-triangle',
                        [ParentId] = @StokDepoYonetimiId,
                        [MenuOrder] = 4,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @SktUyarilariMenuId;
                END;

                IF @StokDepoYonetimiId IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @StokDepoYonetimiId AND [RoleId] = @StokHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @StokDepoYonetimiId, @StokHareketMenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @SktUyarilariMenuId AND [RoleId] = @StokHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @SktUyarilariMenuId, @StokHareketMenuRoleId, 0, @Now, @Now);
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
                WHERE mi.[Route] = N'muhasebe/stok-lotlari/skt-uyarilari';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/stok-lotlari/skt-uyarilari';
                """);
        }
    }
}
