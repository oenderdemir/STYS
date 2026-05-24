using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260524160000_FixMuhasebeMenuGroupAssignments")]
public partial class FixMuhasebeMenuGroupAssignments : Migration
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

            -- ============================================================
            -- 2. Find existing group headings (created by Phase 64)
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
            -- 3. Re-assign all items to corrected groups
            -- ============================================================

            -- === Cari Yönetimi ===
            --   Cari Kartlar (0), Cari Hareketler (1), Hesaplar (2), Tahsilat/Ödeme Belgeleri (3)
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @CariYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/cari-kartlar' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @CariYonetimiId, [MenuOrder] = 1, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/cari-hareketler' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @CariYonetimiId, [MenuOrder] = 2, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/hesaplar' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @CariYonetimiId, [MenuOrder] = 3, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/tahsilat-odeme-belgeleri' AND [IsDeleted] = 0;

            -- === Finans Yönetimi ===
            --   Kasa Hareketleri (0), Banka Hareketleri (1), Finansal Hesaplar (2), Muhasebe Özet (3), Hızlı Mizan (4)
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @FinansYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kasa-hareketleri' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @FinansYonetimiId, [MenuOrder] = 1, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/banka-hareketleri' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @FinansYonetimiId, [MenuOrder] = 2, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kasa-banka-hesaplari' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @FinansYonetimiId, [MenuOrder] = 3, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/dashboard' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @FinansYonetimiId, [MenuOrder] = 4, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/hizli-mizan' AND [IsDeleted] = 0;

            -- === Muhasebe Yönetimi ===
            --   Muhasebe Fişleri (0), Muhasebe Hesap Planı (1), Yevmiye Defteri (2), Muavin Defter (3), Muhasebe Dönemleri (4)
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/fisler' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 1, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/hesap-plani' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 2, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/yevmiye-defteri' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 3, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/muavin-defter' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeYonetimiId, [MenuOrder] = 4, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/donemler' AND [IsDeleted] = 0;

            -- === Stok & Depo Yönetimi ===
            --   Depolar (0), Stok Hareketleri (1), Paket Türleri (2)
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @StokDepoYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/depolar' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @StokDepoYonetimiId, [MenuOrder] = 1, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/stok-hareketleri' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @StokDepoYonetimiId, [MenuOrder] = 2, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/paket-turleri' AND [IsDeleted] = 0;

            -- === Taşınır / Demirbaş Yönetimi (unchanged from Phase 64) ===
            --   Taşınır Kodları (0), Taşınır Kartları (1), Taşınır Fiş Taslağı (2)
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @TasinirDemirbasYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/tasinir-kodlari' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @TasinirDemirbasYonetimiId, [MenuOrder] = 1, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/tasinir-kartlari' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @TasinirDemirbasYonetimiId, [MenuOrder] = 2, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/tasinir-fis-taslagi' AND [IsDeleted] = 0;

            -- === Vergi & KDV İşlemleri ===
            --   KDV Hareket Raporu (0), KDV Özet Raporu (1), KDV İstisna Tanımları (2), KDV Beyanname Hazırlık Kontrolü (3)
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @VergiKdvYonetimiId, [MenuOrder] = 0, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kdv-hareket-raporu' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @VergiKdvYonetimiId, [MenuOrder] = 1, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kdv-ozet-raporu' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @VergiKdvYonetimiId, [MenuOrder] = 2, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kdv-istisna-tanimlari' AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @VergiKdvYonetimiId, [MenuOrder] = 3, [UpdatedAt] = @Now
            WHERE [Route] = N'muhasebe/kdv-beyanname-hazirlik-kontrol' AND [IsDeleted] = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            -- Find Muhasebe root
            DECLARE @MuhasebeRootId uniqueidentifier;
            SELECT TOP (1) @MuhasebeRootId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            -- Move all items back to Muhasebe root
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MuhasebeRootId, [UpdatedAt] = @Now
            WHERE [Route] IN (
                N'muhasebe/cari-kartlar',
                N'muhasebe/cari-hareketler',
                N'muhasebe/hesaplar',
                N'muhasebe/tahsilat-odeme-belgeleri',
                N'muhasebe/kasa-hareketleri',
                N'muhasebe/banka-hareketleri',
                N'muhasebe/kasa-banka-hesaplari',
                N'muhasebe/dashboard',
                N'muhasebe/hizli-mizan',
                N'muhasebe/fisler',
                N'muhasebe/hesap-plani',
                N'muhasebe/yevmiye-defteri',
                N'muhasebe/muavin-defter',
                N'muhasebe/donemler',
                N'muhasebe/depolar',
                N'muhasebe/stok-hareketleri',
                N'muhasebe/paket-turleri',
                N'muhasebe/tasinir-kodlari',
                N'muhasebe/tasinir-kartlari',
                N'muhasebe/tasinir-fis-taslagi',
                N'muhasebe/kdv-hareket-raporu',
                N'muhasebe/kdv-ozet-raporu',
                N'muhasebe/kdv-istisna-tanimlari',
                N'muhasebe/kdv-beyanname-hazirlik-kontrol'
            )
            AND [IsDeleted] = 0;
            """);
    }
}
