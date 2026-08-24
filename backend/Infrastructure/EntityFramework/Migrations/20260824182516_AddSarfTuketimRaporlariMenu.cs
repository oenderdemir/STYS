using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddSarfTuketimRaporlariMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @SarfMenuRoleId uniqueidentifier;
                DECLARE @MuhasebeRootId uniqueidentifier;
                DECLARE @StokDepoYonetimiId uniqueidentifier;
                DECLARE @RaporMenuId uniqueidentifier;

                SELECT TOP (1) @SarfMenuRoleId = [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'SarfYonetimi' AND [Name] = N'Menu';

                IF @SarfMenuRoleId IS NULL
                BEGIN
                    SET @SarfMenuRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@SarfMenuRoleId, N'Menu', N'SarfYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @MuhasebeRootId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

                SELECT TOP (1) @StokDepoYonetimiId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Stok & Depo Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;

                SELECT TOP (1) @RaporMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/sarf-raporlari';

                IF @RaporMenuId IS NULL
                BEGIN
                    SET @RaporMenuId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@RaporMenuId, N'Sarf / Tüketim Raporları', N'pi pi-chart-bar', N'muhasebe/sarf-raporlari', @StokDepoYonetimiId, 6, 0, @Now, @Now);
                END;
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Sarf / Tüketim Raporları',
                        [Icon] = N'pi pi-chart-bar',
                        [ParentId] = @StokDepoYonetimiId,
                        [MenuOrder] = 6,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @RaporMenuId;
                END;

                IF @StokDepoYonetimiId IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @StokDepoYonetimiId AND [RoleId] = @SarfMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @StokDepoYonetimiId, @SarfMenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RaporMenuId AND [RoleId] = @SarfMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @RaporMenuId, @SarfMenuRoleId, 0, @Now, @Now);
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
                WHERE mi.[Route] = N'muhasebe/sarf-raporlari';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/sarf-raporlari';
                """);
        }
    }
}
