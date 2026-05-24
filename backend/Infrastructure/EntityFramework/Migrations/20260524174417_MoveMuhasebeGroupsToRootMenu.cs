using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

public partial class MoveMuhasebeGroupsToRootMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            -- ============================================================
            -- 1. Find old Muhasebe root (ParentId IS NULL)
            -- ============================================================
            DECLARE @MuhasebeRootId uniqueidentifier;
            SELECT TOP (1) @MuhasebeRootId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            -- ============================================================
            -- 2. Find the 6 group headings (currently children of Muhasebe root)
            -- ============================================================
            DECLARE @CariYonetimiId uniqueidentifier;
            DECLARE @FinansYonetimiId uniqueidentifier;
            DECLARE @MuhasebeYonetimiId uniqueidentifier;
            DECLARE @StokDepoYonetimiId uniqueidentifier;
            DECLARE @TasinirDemirbasYonetimiId uniqueidentifier;
            DECLARE @VergiKdvYonetimiId uniqueidentifier;

            SELECT TOP (1) @CariYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Cari Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;

            SELECT TOP (1) @FinansYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Finans Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;

            SELECT TOP (1) @MuhasebeYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Muhasebe Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;

            SELECT TOP (1) @StokDepoYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Stok & Depo Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;

            SELECT TOP (1) @TasinirDemirbasYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Taşınır / Demirbaş Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;

            SELECT TOP (1) @VergiKdvYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Vergi & KDV İşlemleri' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;

            -- ============================================================
            -- 3. Promote 6 group headings to root level
            --    (sub-items stay under their parent; only the parent moves to root)
            --    MenuOrder: 10, 20, 30, 40, 50, 60
            -- ============================================================
            UPDATE [TODBase].[MenuItems] SET [ParentId] = NULL, [MenuOrder] = 10, [UpdatedAt] = @Now WHERE [Id] = @CariYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = NULL, [MenuOrder] = 20, [UpdatedAt] = @Now WHERE [Id] = @FinansYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = NULL, [MenuOrder] = 30, [UpdatedAt] = @Now WHERE [Id] = @MuhasebeYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = NULL, [MenuOrder] = 40, [UpdatedAt] = @Now WHERE [Id] = @StokDepoYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = NULL, [MenuOrder] = 50, [UpdatedAt] = @Now WHERE [Id] = @TasinirDemirbasYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = NULL, [MenuOrder] = 60, [UpdatedAt] = @Now WHERE [Id] = @VergiKdvYonetimiId;

            -- ============================================================
            -- 4. Ensure all 6 new root items have MenuItemRoles
            --    (idempotent: skip if already assigned in Phase 64)
            -- ============================================================
            INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), @CariYonetimiId, mir.[RoleId], 0, @Now, @Now
            FROM [TODBase].[MenuItemRoles] mir
            WHERE mir.[MenuItemId] = @MuhasebeRootId AND mir.[IsDeleted] = 0
              AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] x WHERE x.[MenuItemId] = @CariYonetimiId AND x.[RoleId] = mir.[RoleId]);

            INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), @FinansYonetimiId, mir.[RoleId], 0, @Now, @Now
            FROM [TODBase].[MenuItemRoles] mir
            WHERE mir.[MenuItemId] = @MuhasebeRootId AND mir.[IsDeleted] = 0
              AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] x WHERE x.[MenuItemId] = @FinansYonetimiId AND x.[RoleId] = mir.[RoleId]);

            INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), @MuhasebeYonetimiId, mir.[RoleId], 0, @Now, @Now
            FROM [TODBase].[MenuItemRoles] mir
            WHERE mir.[MenuItemId] = @MuhasebeRootId AND mir.[IsDeleted] = 0
              AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] x WHERE x.[MenuItemId] = @MuhasebeYonetimiId AND x.[RoleId] = mir.[RoleId]);

            INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), @StokDepoYonetimiId, mir.[RoleId], 0, @Now, @Now
            FROM [TODBase].[MenuItemRoles] mir
            WHERE mir.[MenuItemId] = @MuhasebeRootId AND mir.[IsDeleted] = 0
              AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] x WHERE x.[MenuItemId] = @StokDepoYonetimiId AND x.[RoleId] = mir.[RoleId]);

            INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), @TasinirDemirbasYonetimiId, mir.[RoleId], 0, @Now, @Now
            FROM [TODBase].[MenuItemRoles] mir
            WHERE mir.[MenuItemId] = @MuhasebeRootId AND mir.[IsDeleted] = 0
              AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] x WHERE x.[MenuItemId] = @TasinirDemirbasYonetimiId AND x.[RoleId] = mir.[RoleId]);

            INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), @VergiKdvYonetimiId, mir.[RoleId], 0, @Now, @Now
            FROM [TODBase].[MenuItemRoles] mir
            WHERE mir.[MenuItemId] = @MuhasebeRootId AND mir.[IsDeleted] = 0
              AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] x WHERE x.[MenuItemId] = @VergiKdvYonetimiId AND x.[RoleId] = mir.[RoleId]);

            -- ============================================================
            -- 5. Remove MenuItemRoles from old Muhasebe root
            -- ============================================================
            DELETE FROM [TODBase].[MenuItemRoles]
            WHERE [MenuItemId] = @MuhasebeRootId;

            -- ============================================================
            -- 6. Soft-delete old Muhasebe root
            -- ============================================================
            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 1, [DeletedAt] = @Now, [UpdatedAt] = @Now
            WHERE [Id] = @MuhasebeRootId;

            -- ============================================================
            -- 7. Create "Satış Yönetimi" root menu item (MenuOrder = 70)
            -- ============================================================
            DECLARE @SatisYonetimiId uniqueidentifier = NEWID();

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Satış Yönetimi' AND [ParentId] IS NULL AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@SatisYonetimiId, N'Satış Yönetimi', N'fa-solid fa-cart-shopping', N'', NULL, NULL, 70, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                SELECT TOP (1) @SatisYonetimiId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Satış Yönetimi' AND [ParentId] IS NULL AND [IsDeleted] = 0;
                UPDATE [TODBase].[MenuItems] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @SatisYonetimiId;
            END;

            -- ============================================================
            -- 8. Copy MenuItemRoles from Cari Yönetimi (has same roles as old Muhasebe root)
            --    to the new Satış Yönetimi
            -- ============================================================
            INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), @SatisYonetimiId, mir.[RoleId], 0, @Now, @Now
            FROM [TODBase].[MenuItemRoles] mir
            WHERE mir.[MenuItemId] = @CariYonetimiId AND mir.[IsDeleted] = 0
              AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] x WHERE x.[MenuItemId] = @SatisYonetimiId AND x.[RoleId] = mir.[RoleId]);

            -- ============================================================
            -- 9. Move "Satış Belgeleri" under "Satış Yönetimi"
            -- ============================================================
            UPDATE [TODBase].[MenuItems]
            SET [ParentId] = @SatisYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/satis-belgeleri' AND [IsDeleted] = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            -- ============================================================
            -- 1. Find old Muhasebe root (now soft-deleted)
            -- ============================================================
            DECLARE @MuhasebeRootId uniqueidentifier;
            SELECT TOP (1) @MuhasebeRootId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 1;

            -- ============================================================
            -- 2. Un-soft-delete old Muhasebe root
            -- ============================================================
            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
            WHERE [Id] = @MuhasebeRootId;

            -- ============================================================
            -- 3. Find the 6 root-level group headings
            -- ============================================================
            DECLARE @CariYonetimiId uniqueidentifier;
            DECLARE @FinansYonetimiId uniqueidentifier;
            DECLARE @MuhasebeYonetimiId uniqueidentifier;
            DECLARE @StokDepoYonetimiId uniqueidentifier;
            DECLARE @TasinirDemirbasYonetimiId uniqueidentifier;
            DECLARE @VergiKdvYonetimiId uniqueidentifier;

            SELECT TOP (1) @CariYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Cari Yönetimi' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            SELECT TOP (1) @FinansYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Finans Yönetimi' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            SELECT TOP (1) @MuhasebeYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Muhasebe Yönetimi' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            SELECT TOP (1) @StokDepoYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Stok & Depo Yönetimi' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            SELECT TOP (1) @TasinirDemirbasYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Taşınır / Demirbaş Yönetimi' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            SELECT TOP (1) @VergiKdvYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Vergi & KDV İşlemleri' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            -- ============================================================
            -- 4. Restore MenuItemRoles for old Muhasebe root
            --    (copy from Cari Yönetimi which retained the same roles)
            -- ============================================================
            INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), @MuhasebeRootId, mir.[RoleId], 0, @Now, @Now
            FROM [TODBase].[MenuItemRoles] mir
            WHERE mir.[MenuItemId] = @CariYonetimiId AND mir.[IsDeleted] = 0
              AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] x WHERE x.[MenuItemId] = @MuhasebeRootId AND x.[RoleId] = mir.[RoleId]);

            -- ============================================================
            -- 5. Move 6 group headings back under Muhasebe root
            --    MenuOrder: 0, 1, 2, 3, 4, 5 (original order)
            -- ============================================================
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [MenuOrder] = 0, [UpdatedAt] = @Now WHERE [Id] = @CariYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [MenuOrder] = 1, [UpdatedAt] = @Now WHERE [Id] = @FinansYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [MenuOrder] = 2, [UpdatedAt] = @Now WHERE [Id] = @MuhasebeYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [MenuOrder] = 3, [UpdatedAt] = @Now WHERE [Id] = @StokDepoYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [MenuOrder] = 4, [UpdatedAt] = @Now WHERE [Id] = @TasinirDemirbasYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [MenuOrder] = 5, [UpdatedAt] = @Now WHERE [Id] = @VergiKdvYonetimiId;

            -- ============================================================
            -- 6. Move "Satış Belgeleri" back under old Muhasebe root
            -- ============================================================
            UPDATE [TODBase].[MenuItems]
            SET [ParentId] = @MuhasebeRootId, [MenuOrder] = 99, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/satis-belgeleri' AND [IsDeleted] = 0;

            -- ============================================================
            -- 7. Find and remove "Satış Yönetimi"
            -- ============================================================
            DECLARE @SatisYonetimiId uniqueidentifier;
            SELECT TOP (1) @SatisYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Satış Yönetimi' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            -- 7a. Delete MenuItemRoles for Satış Yönetimi
            DELETE FROM [TODBase].[MenuItemRoles]
            WHERE [MenuItemId] = @SatisYonetimiId;

            -- 7b. Soft-delete Satış Yönetimi menu item
            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 1, [DeletedAt] = @Now, [UpdatedAt] = @Now
            WHERE [Id] = @SatisYonetimiId;
            """);
    }
}
