using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260524150000_ReorganizeMuhasebeMenuGroups")]
public partial class ReorganizeMuhasebeMenuGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            -- ============================================================
            -- 1. Find Muhasebe root
            -- ============================================================
            DECLARE @MuhasebeRootId uniqueidentifier;
            SELECT TOP (1) @MuhasebeRootId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            IF @MuhasebeRootId IS NULL
            BEGIN
                SET @MuhasebeRootId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MuhasebeRootId, N'Muhasebe', N'pi pi-wallet', N'', NULL, NULL, 6, 0, @Now, @Now);
            END;

            -- ============================================================
            -- 2. Create 6 group headings under Muhasebe root
            -- ============================================================
            DECLARE @CariYonetimiId uniqueidentifier = NEWID();
            DECLARE @FinansYonetimiId uniqueidentifier = NEWID();
            DECLARE @MuhasebeYonetimiId uniqueidentifier = NEWID();
            DECLARE @StokDepoYonetimiId uniqueidentifier = NEWID();
            DECLARE @TasinirDemirbasYonetimiId uniqueidentifier = NEWID();
            DECLARE @VergiKdvYonetimiId uniqueidentifier = NEWID();

            -- Cari Yönetimi
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Cari Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@CariYonetimiId, N'Cari Yönetimi', N'fa-solid fa-users', N'', NULL, @MuhasebeRootId, 0, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                SELECT TOP (1) @CariYonetimiId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Cari Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;
                UPDATE [TODBase].[MenuItems] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @CariYonetimiId;
            END;

            -- Finans Yönetimi
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Finans Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@FinansYonetimiId, N'Finans Yönetimi', N'fa-solid fa-coins', N'', NULL, @MuhasebeRootId, 1, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                SELECT TOP (1) @FinansYonetimiId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Finans Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;
                UPDATE [TODBase].[MenuItems] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @FinansYonetimiId;
            END;

            -- Muhasebe Yönetimi
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Muhasebe Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MuhasebeYonetimiId, N'Muhasebe Yönetimi', N'fa-solid fa-calculator', N'', NULL, @MuhasebeRootId, 2, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                SELECT TOP (1) @MuhasebeYonetimiId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Muhasebe Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;
                UPDATE [TODBase].[MenuItems] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @MuhasebeYonetimiId;
            END;

            -- Stok & Depo Yönetimi
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Stok & Depo Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@StokDepoYonetimiId, N'Stok & Depo Yönetimi', N'fa-solid fa-boxes-stacked', N'', NULL, @MuhasebeRootId, 3, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                SELECT TOP (1) @StokDepoYonetimiId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Stok & Depo Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;
                UPDATE [TODBase].[MenuItems] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @StokDepoYonetimiId;
            END;

            -- Taşınır / Demirbaş Yönetimi
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Taşınır / Demirbaş Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@TasinirDemirbasYonetimiId, N'Taşınır / Demirbaş Yönetimi', N'fa-solid fa-warehouse', N'', NULL, @MuhasebeRootId, 4, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                SELECT TOP (1) @TasinirDemirbasYonetimiId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Taşınır / Demirbaş Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;
                UPDATE [TODBase].[MenuItems] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @TasinirDemirbasYonetimiId;
            END;

            -- Vergi & KDV İşlemleri
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Vergi & KDV İşlemleri' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@VergiKdvYonetimiId, N'Vergi & KDV İşlemleri', N'fa-solid fa-file-invoice', N'', NULL, @MuhasebeRootId, 5, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                SELECT TOP (1) @VergiKdvYonetimiId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Vergi & KDV İşlemleri' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;
                UPDATE [TODBase].[MenuItems] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @VergiKdvYonetimiId;
            END;

            -- ============================================================
            -- 3. Copy MenuItemRoles from Muhasebe root to each group heading
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
            -- 4. Move existing menu items under their new group headings
            -- ============================================================

            -- === Cari Yönetimi (order 0) ===
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @CariYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/cari-kartlar' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @CariYonetimiId, [MenuOrder] = 1, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/cari-hareketler' AND [IsDeleted] = 0;

            -- === Finans Yönetimi (order 1) ===
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @FinansYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kasa-hareketleri' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @FinansYonetimiId, [MenuOrder] = 1, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/banka-hareketleri' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @FinansYonetimiId, [MenuOrder] = 2, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kasa-banka-hesaplari' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @FinansYonetimiId, [MenuOrder] = 3, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/tahsilat-odeme-belgeleri' AND [IsDeleted] = 0;

            -- === Muhasebe Yönetimi (order 2) ===
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/hesap-plani' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 1, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/hesaplar' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 2, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/fisler' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 3, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/hizli-mizan' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 4, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/yevmiye-defteri' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 5, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/muavin-defter' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 6, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/donemler' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 7, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/dashboard' AND [IsDeleted] = 0;

            -- === Stok & Depo Yönetimi (order 3) ===
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @StokDepoYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/depolar' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @StokDepoYonetimiId, [MenuOrder] = 1, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/paket-turleri' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @StokDepoYonetimiId, [MenuOrder] = 2, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/stok-hareketleri' AND [IsDeleted] = 0;

            -- === Taşınır / Demirbaş Yönetimi (order 4) ===
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @TasinirDemirbasYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/tasinir-kodlari' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @TasinirDemirbasYonetimiId, [MenuOrder] = 1, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/tasinir-kartlari' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @TasinirDemirbasYonetimiId, [MenuOrder] = 2, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/tasinir-fis-taslagi' AND [IsDeleted] = 0;

            -- === Vergi & KDV İşlemleri (order 5) ===
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @VergiKdvYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kdv-istisna-tanimlari' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @VergiKdvYonetimiId, [MenuOrder] = 1, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kdv-hareket-raporu' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @VergiKdvYonetimiId, [MenuOrder] = 2, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kdv-ozet-raporu' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @VergiKdvYonetimiId, [MenuOrder] = 3, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kdv-beyanname-hazirlik-kontrol' AND [IsDeleted] = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            -- ============================================================
            -- 1. Find Muhasebe root
            -- ============================================================
            DECLARE @MuhasebeRootId uniqueidentifier;
            SELECT TOP (1) @MuhasebeRootId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            -- ============================================================
            -- 2. Move all existing items back to Muhasebe root
            --    (ParentId only — order values are left as-is since they
            --     are group-relative and would conflict under one parent)
            -- ============================================================

            -- Cari Yönetimi items
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/cari-kartlar' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/cari-hareketler' AND [IsDeleted] = 0;

            -- Finans Yönetimi items
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kasa-hareketleri' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/banka-hareketleri' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kasa-banka-hesaplari' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/tahsilat-odeme-belgeleri' AND [IsDeleted] = 0;

            -- Muhasebe Yönetimi items
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/hesap-plani' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/hesaplar' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/fisler' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/hizli-mizan' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/yevmiye-defteri' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/muavin-defter' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/donemler' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/dashboard' AND [IsDeleted] = 0;

            -- Stok & Depo Yönetimi items
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/depolar' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/paket-turleri' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/stok-hareketleri' AND [IsDeleted] = 0;

            -- Taşınır / Demirbaş Yönetimi items
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/tasinir-kodlari' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/tasinir-kartlari' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/tasinir-fis-taslagi' AND [IsDeleted] = 0;

            -- Vergi & KDV İşlemleri items
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kdv-istisna-tanimlari' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kdv-hareket-raporu' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kdv-ozet-raporu' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kdv-beyanname-hazirlik-kontrol' AND [IsDeleted] = 0;

            -- ============================================================
            -- 3. Find and remove the 6 group headings created in Up()
            -- ============================================================
            DECLARE @CariYonetimiId uniqueidentifier;
            DECLARE @FinansYonetimiId uniqueidentifier;
            DECLARE @MuhasebeYonetimiId uniqueidentifier;
            DECLARE @StokDepoYonetimiId uniqueidentifier;
            DECLARE @TasinirDemirbasYonetimiId uniqueidentifier;
            DECLARE @VergiKdvYonetimiId uniqueidentifier;

            SELECT TOP (1) @CariYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Cari Yönetimi' AND [ParentId] = @MuhasebeRootId;

            SELECT TOP (1) @FinansYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Finans Yönetimi' AND [ParentId] = @MuhasebeRootId;

            SELECT TOP (1) @MuhasebeYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Muhasebe Yönetimi' AND [ParentId] = @MuhasebeRootId;

            SELECT TOP (1) @StokDepoYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Stok & Depo Yönetimi' AND [ParentId] = @MuhasebeRootId;

            SELECT TOP (1) @TasinirDemirbasYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Taşınır / Demirbaş Yönetimi' AND [ParentId] = @MuhasebeRootId;

            SELECT TOP (1) @VergiKdvYonetimiId = [Id] FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Vergi & KDV İşlemleri' AND [ParentId] = @MuhasebeRootId;

            -- 3a. Delete MenuItemRoles for group headings
            DELETE FROM [TODBase].[MenuItemRoles]
            WHERE [MenuItemId] IN (@CariYonetimiId, @FinansYonetimiId, @MuhasebeYonetimiId, @StokDepoYonetimiId, @TasinirDemirbasYonetimiId, @VergiKdvYonetimiId);

            -- 3b. Delete group heading MenuItems
            DELETE FROM [TODBase].[MenuItems]
            WHERE [Id] IN (@CariYonetimiId, @FinansYonetimiId, @MuhasebeYonetimiId, @StokDepoYonetimiId, @TasinirDemirbasYonetimiId, @VergiKdvYonetimiId);
            """);
    }
}
