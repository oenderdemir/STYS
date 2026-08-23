using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddCriticalStockWarnings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "KritikStokMiktari",
                schema: "muhasebe",
                table: "TasinirKartlar",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumStokMiktari",
                schema: "muhasebe",
                table: "TasinirKartlar",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TasinirKartlar_KritikStokMiktari_MinimumuAsamaz",
                schema: "muhasebe",
                table: "TasinirKartlar",
                sql: "[KritikStokMiktari] IS NULL OR [MinimumStokMiktari] IS NULL OR [KritikStokMiktari] <= [MinimumStokMiktari]");

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @StokHareketMenuRoleId uniqueidentifier;
                DECLARE @MuhasebeRootId uniqueidentifier;
                DECLARE @StokDepoYonetimiId uniqueidentifier;
                DECLARE @KritikStokMenuId uniqueidentifier;

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

                SELECT TOP (1) @KritikStokMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/stok-uyarilari';

                IF @KritikStokMenuId IS NULL
                BEGIN
                    SET @KritikStokMenuId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KritikStokMenuId, N'Kritik Stok Uyarıları', N'pi pi-exclamation-circle', N'muhasebe/stok-uyarilari', @StokDepoYonetimiId, 5, 0, @Now, @Now);
                END;
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Kritik Stok Uyarıları',
                        [Icon] = N'pi pi-exclamation-circle',
                        [ParentId] = @StokDepoYonetimiId,
                        [MenuOrder] = 5,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @KritikStokMenuId;
                END;

                IF @StokDepoYonetimiId IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @StokDepoYonetimiId AND [RoleId] = @StokHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @StokDepoYonetimiId, @StokHareketMenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KritikStokMenuId AND [RoleId] = @StokHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @KritikStokMenuId, @StokHareketMenuRoleId, 0, @Now, @Now);
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
                WHERE mi.[Route] = N'muhasebe/stok-uyarilari';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/stok-uyarilari';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_TasinirKartlar_KritikStokMiktari_MinimumuAsamaz",
                schema: "muhasebe",
                table: "TasinirKartlar");

            migrationBuilder.DropColumn(
                name: "KritikStokMiktari",
                schema: "muhasebe",
                table: "TasinirKartlar");

            migrationBuilder.DropColumn(
                name: "MinimumStokMiktari",
                schema: "muhasebe",
                table: "TasinirKartlar");
        }
    }
}

