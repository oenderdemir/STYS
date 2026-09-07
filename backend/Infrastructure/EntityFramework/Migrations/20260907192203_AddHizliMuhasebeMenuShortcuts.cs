using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <summary>
    /// "Hızlı Muhasebe" kök menüsü + 8 kısayol MenuItem'ı ekler. Mevcut muhasebe MenuItem'ları
    /// YERLERİNDE KALIR (taşınmaz); aynı route'lara giden YENİ kısayollar oluşturulur. Route tekilliği
    /// varsayılmaz — kısayollar her zaman (ParentId = @HizliMuhasebeId AND Route = ...) ile çözülür.
    /// Yetki modeli genişletilmez: root role'ları aktif "Muhasebe" kökünden, child role'ları ise
    /// ilgili original MenuItem'ın (Route + original parent) rol kümesinden KOPYALANIR.
    /// </summary>
    public partial class AddHizliMuhasebeMenuShortcuts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                DECLARE @Now datetime2 = SYSUTCDATETIME();

                -- ============================================================
                -- 1. Aktif "Muhasebe" kök menüsünü bul (rol kopyası + sıra referansı).
                --    NOT: "Muhasebe" (kök, MenuOrder=60) ile "Muhasebe Yönetimi" (grup, MenuOrder=300)
                --    farklı kayıtlardır.
                -- ============================================================
                DECLARE @MuhasebeRootId uniqueidentifier;
                SELECT TOP (1) @MuhasebeRootId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

                -- ============================================================
                -- 2. "Hızlı Muhasebe" kök menüsü (Muhasebe kökünden hemen önce: MenuOrder = 55).
                -- ============================================================
                DECLARE @HizliMuhasebeId uniqueidentifier;
                SELECT TOP (1) @HizliMuhasebeId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Hızlı Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

                IF @HizliMuhasebeId IS NULL
                BEGIN
                    SET @HizliMuhasebeId = NEWID();
                    INSERT INTO [TODBase].[MenuItems]
                        ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@HizliMuhasebeId, N'Hızlı Muhasebe', N'pi pi-bolt', N'', NULL, NULL, 55, 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [IsDeleted] = 0, [DeletedAt] = NULL, [Icon] = N'pi pi-bolt', [MenuOrder] = 55, [UpdatedAt] = @Now
                    WHERE [Id] = @HizliMuhasebeId;
                END;

                -- ============================================================
                -- 3. Root rol kopyası: aktif "Muhasebe" kökünün rolleri → "Hızlı Muhasebe".
                -- ============================================================
                IF @MuhasebeRootId IS NOT NULL
                BEGIN
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    SELECT NEWID(), @HizliMuhasebeId, mir.[RoleId], 0, @Now, @Now
                    FROM [TODBase].[MenuItemRoles] mir
                    WHERE mir.[MenuItemId] = @MuhasebeRootId AND mir.[IsDeleted] = 0
                      AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] x WHERE x.[MenuItemId] = @HizliMuhasebeId AND x.[RoleId] = mir.[RoleId]);
                END;

                -- ============================================================
                -- 4. 8 kısayol (ParentId = Hızlı Muhasebe, MenuOrder = 0..7).
                --    Original parent, "Route + beklenen parent label" ile çözülür (Route ile DEĞİL).
                -- ============================================================
                DECLARE @Shortcuts TABLE
                (
                    [MenuOrder] int NOT NULL,
                    [Label] nvarchar(128) NOT NULL,
                    [Route] nvarchar(256) NOT NULL,
                    [OriginalParentLabel] nvarchar(128) NOT NULL
                );

                INSERT INTO @Shortcuts ([MenuOrder], [Label], [Route], [OriginalParentLabel])
                VALUES
                (0, N'1. Hesap Planı', N'muhasebe/hesap-plani', N'Muhasebe Yönetimi'),
                (1, N'2. Kasa / Banka Hesapları', N'muhasebe/kasa-banka-hesaplari', N'Finans Yönetimi'),
                (2, N'3. Konaklama Vergisi Hesabı', N'muhasebe/konaklama-vergisi-hesap-eslemeleri', N'Muhasebe'),
                (3, N'4. Satış Belgeleri', N'muhasebe/satis-belgeleri', N'Muhasebe'),
                (4, N'5. Tahsilat / Ödeme Belgeleri', N'muhasebe/tahsilat-odeme-belgeleri', N'Cari Yönetimi'),
                (5, N'6. Muhasebe Fişleri', N'muhasebe/fisler', N'Muhasebe Yönetimi'),
                (6, N'7. Cari Hareketler', N'muhasebe/cari-hareketler', N'Cari Yönetimi'),
                (7, N'8. Hızlı Mizan', N'muhasebe/hizli-mizan', N'Finans Yönetimi');

                DECLARE @Order int, @ShortcutLabel nvarchar(128), @ShortcutRoute nvarchar(256), @OriginalParentLabel nvarchar(128);
                DECLARE @OriginalParentId uniqueidentifier, @OriginalItemId uniqueidentifier, @OriginalIcon nvarchar(128), @ShortcutId uniqueidentifier;

                DECLARE shortcut_cursor CURSOR LOCAL FOR
                    SELECT [MenuOrder], [Label], [Route], [OriginalParentLabel] FROM @Shortcuts;

                OPEN shortcut_cursor;
                FETCH NEXT FROM shortcut_cursor INTO @Order, @ShortcutLabel, @ShortcutRoute, @OriginalParentLabel;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    -- Original parent (kök seviyesinde: grup başlığı VEYA "Muhasebe" kökü).
                    SET @OriginalParentId = NULL;
                    SELECT TOP (1) @OriginalParentId = [Id]
                    FROM [TODBase].[MenuItems]
                    WHERE [Label] = @OriginalParentLabel AND [ParentId] IS NULL AND [IsDeleted] = 0;

                    -- Original MenuItem (Route + original parent) — icon + rol kaynağı.
                    SET @OriginalItemId = NULL;
                    SET @OriginalIcon = NULL;
                    SELECT TOP (1) @OriginalItemId = [Id], @OriginalIcon = [Icon]
                    FROM [TODBase].[MenuItems]
                    WHERE [Route] = @ShortcutRoute AND [ParentId] = @OriginalParentId AND [IsDeleted] = 0;

                    -- Kısayolu oluştur / güncelle (ParentId + Route idempotency).
                    SET @ShortcutId = NULL;
                    SELECT TOP (1) @ShortcutId = [Id]
                    FROM [TODBase].[MenuItems]
                    WHERE [ParentId] = @HizliMuhasebeId AND [Route] = @ShortcutRoute AND [IsDeleted] = 0;

                    IF @ShortcutId IS NULL
                    BEGIN
                        SET @ShortcutId = NEWID();
                        INSERT INTO [TODBase].[MenuItems]
                            ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES
                            (@ShortcutId, @ShortcutLabel, ISNULL(@OriginalIcon, N'pi pi-file'), @ShortcutRoute, NULL, @HizliMuhasebeId, @Order, 0, @Now, @Now);
                    END
                    ELSE
                    BEGIN
                        UPDATE [TODBase].[MenuItems]
                        SET [Label] = @ShortcutLabel,
                            [Icon] = ISNULL(@OriginalIcon, [Icon]),
                            [MenuOrder] = @Order,
                            [IsDeleted] = 0,
                            [DeletedAt] = NULL,
                            [UpdatedAt] = @Now
                        WHERE [Id] = @ShortcutId;
                    END;

                    -- Kısayola original MenuItem'ın rollerini kopyala.
                    IF @OriginalItemId IS NOT NULL
                    BEGIN
                        INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        SELECT NEWID(), @ShortcutId, mir.[RoleId], 0, @Now, @Now
                        FROM [TODBase].[MenuItemRoles] mir
                        WHERE mir.[MenuItemId] = @OriginalItemId AND mir.[IsDeleted] = 0
                          AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] x WHERE x.[MenuItemId] = @ShortcutId AND x.[RoleId] = mir.[RoleId]);
                    END;

                    FETCH NEXT FROM shortcut_cursor INTO @Order, @ShortcutLabel, @ShortcutRoute, @OriginalParentLabel;
                END;

                CLOSE shortcut_cursor;
                DEALLOCATE shortcut_cursor;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @HizliMuhasebeId uniqueidentifier;
                SELECT TOP (1) @HizliMuhasebeId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Hızlı Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

                IF @HizliMuhasebeId IS NOT NULL
                BEGIN
                    -- 1. Kısayol MenuItemRoles kayıtlarını kaldır.
                    DELETE mir
                    FROM [TODBase].[MenuItemRoles] mir
                    INNER JOIN [TODBase].[MenuItems] m ON m.[Id] = mir.[MenuItemId]
                    WHERE m.[ParentId] = @HizliMuhasebeId;

                    -- 2. Kısayol MenuItem'larını soft-delete et.
                    UPDATE [TODBase].[MenuItems]
                    SET [IsDeleted] = 1, [DeletedAt] = @Now, [UpdatedAt] = @Now
                    WHERE [ParentId] = @HizliMuhasebeId AND [IsDeleted] = 0;

                    -- 3. Hızlı Muhasebe root MenuItemRoles kayıtlarını kaldır.
                    DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @HizliMuhasebeId;

                    -- 4. Hızlı Muhasebe root MenuItem'ını soft-delete et.
                    UPDATE [TODBase].[MenuItems]
                    SET [IsDeleted] = 1, [DeletedAt] = @Now, [UpdatedAt] = @Now
                    WHERE [Id] = @HizliMuhasebeId;
                END;
                """);
        }
    }
}
