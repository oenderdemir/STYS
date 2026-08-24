using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddSarfFisleriAndStockPermissionSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SarfFisleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    DepoId = table.Column<int>(type: "int", nullable: false),
                    SarfTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsletmeAlaniId = table.Column<int>(type: "int", nullable: true),
                    Durum = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    OlusturanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SarfFisleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SarfFisleri_Depolar_DepoId",
                        column: x => x.DepoId,
                        principalSchema: "muhasebe",
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SarfFisleri_IsletmeAlanlari_IsletmeAlaniId",
                        column: x => x.IsletmeAlaniId,
                        principalSchema: "dbo",
                        principalTable: "IsletmeAlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SarfFisleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SarfFisiSatirlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SarfFisiId = table.Column<int>(type: "int", nullable: false),
                    TasinirKartId = table.Column<int>(type: "int", nullable: false),
                    StokLotId = table.Column<int>(type: "int", nullable: true),
                    StokSeriId = table.Column<int>(type: "int", nullable: true),
                    StokHareketId = table.Column<int>(type: "int", nullable: true),
                    TakipTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StokKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TasinirKartAd = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Birim = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LotNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SonKullanmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SeriNo = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Miktar = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SarfFisiSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SarfFisiSatirlari_SarfFisleri_SarfFisiId",
                        column: x => x.SarfFisiId,
                        principalSchema: "muhasebe",
                        principalTable: "SarfFisleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SarfFisiSatirlari_StokHareketleri_StokHareketId",
                        column: x => x.StokHareketId,
                        principalSchema: "muhasebe",
                        principalTable: "StokHareketleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SarfFisiSatirlari_StokLotlar_StokLotId",
                        column: x => x.StokLotId,
                        principalSchema: "muhasebe",
                        principalTable: "StokLotlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SarfFisiSatirlari_StokSeriler_StokSeriId",
                        column: x => x.StokSeriId,
                        principalSchema: "muhasebe",
                        principalTable: "StokSeriler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SarfFisiSatirlari_TasinirKartlar_TasinirKartId",
                        column: x => x.TasinirKartId,
                        principalSchema: "muhasebe",
                        principalTable: "TasinirKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SarfFisiSatirlari_SarfFisiId_TasinirKartId_StokLotId_StokSeriId",
                schema: "muhasebe",
                table: "SarfFisiSatirlari",
                columns: new[] { "SarfFisiId", "TasinirKartId", "StokLotId", "StokSeriId" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SarfFisiSatirlari_StokHareketId",
                schema: "muhasebe",
                table: "SarfFisiSatirlari",
                column: "StokHareketId",
                filter: "[IsDeleted] = 0 AND [StokHareketId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SarfFisiSatirlari_StokLotId",
                schema: "muhasebe",
                table: "SarfFisiSatirlari",
                column: "StokLotId");

            migrationBuilder.CreateIndex(
                name: "IX_SarfFisiSatirlari_StokSeriId",
                schema: "muhasebe",
                table: "SarfFisiSatirlari",
                column: "StokSeriId");

            migrationBuilder.CreateIndex(
                name: "IX_SarfFisiSatirlari_TasinirKartId",
                schema: "muhasebe",
                table: "SarfFisiSatirlari",
                column: "TasinirKartId");

            migrationBuilder.CreateIndex(
                name: "IX_SarfFisleri_DepoId",
                schema: "muhasebe",
                table: "SarfFisleri",
                column: "DepoId");

            migrationBuilder.CreateIndex(
                name: "IX_SarfFisleri_IsletmeAlaniId",
                schema: "muhasebe",
                table: "SarfFisleri",
                column: "IsletmeAlaniId");

            migrationBuilder.CreateIndex(
                name: "IX_SarfFisleri_TesisId_DepoId_SarfTarihi",
                schema: "muhasebe",
                table: "SarfFisleri",
                columns: new[] { "TesisId", "DepoId", "SarfTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @MuhasebeRootId uniqueidentifier;
                DECLARE @StokDepoYonetimiId uniqueidentifier;
                DECLARE @StokTalepleriMenuId uniqueidentifier;
                DECLARE @DepodanCikisMenuId uniqueidentifier;
                DECLARE @SarfFisleriMenuId uniqueidentifier;

                DECLARE @StokHareketMenuRoleId uniqueidentifier;
                DECLARE @StokTalepMenuRoleId uniqueidentifier;
                DECLARE @StokDepoCikisMenuRoleId uniqueidentifier;
                DECLARE @SarfMenuRoleId uniqueidentifier;

                SELECT TOP (1) @StokHareketMenuRoleId = [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'StokHareketYonetimi' AND [Name] = N'Menu';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'StokTalepYonetimi' AND [Name] = N'Menu')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'Menu', N'StokTalepYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'StokTalepYonetimi' AND [Name] = N'View')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'View', N'StokTalepYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'StokTalepYonetimi' AND [Name] = N'Create')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'Create', N'StokTalepYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'StokTalepYonetimi' AND [Name] = N'Approve')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'Approve', N'StokTalepYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'StokTalepYonetimi' AND [Name] = N'Deliver')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'Deliver', N'StokTalepYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'StokTalepYonetimi' AND [Name] = N'Cancel')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'Cancel', N'StokTalepYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'StokDepoCikisYonetimi' AND [Name] = N'Menu')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'Menu', N'StokDepoCikisYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'StokDepoCikisYonetimi' AND [Name] = N'View')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'View', N'StokDepoCikisYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'StokDepoCikisYonetimi' AND [Name] = N'Create')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'Create', N'StokDepoCikisYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'SarfYonetimi' AND [Name] = N'Menu')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'Menu', N'SarfYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'SarfYonetimi' AND [Name] = N'View')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'View', N'SarfYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'SarfYonetimi' AND [Name] = N'Create')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'Create', N'SarfYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'SarfYonetimi' AND [Name] = N'Finalize')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'Finalize', N'SarfYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'SarfYonetimi' AND [Name] = N'Cancel')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'Cancel', N'SarfYonetimi', 0, @Now, @Now);

                SELECT TOP (1) @StokTalepMenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'StokTalepYonetimi' AND [Name] = N'Menu';
                SELECT TOP (1) @StokDepoCikisMenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'StokDepoCikisYonetimi' AND [Name] = N'Menu';
                SELECT TOP (1) @SarfMenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'SarfYonetimi' AND [Name] = N'Menu';

                SELECT TOP (1) @MuhasebeRootId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

                SELECT TOP (1) @StokDepoYonetimiId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Stok & Depo Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;

                SELECT TOP (1) @StokTalepleriMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/stok-talepleri' AND [Label] = N'Stok Talepleri';

                IF @StokTalepleriMenuId IS NULL
                BEGIN
                    SET @StokTalepleriMenuId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@StokTalepleriMenuId, N'Stok Talepleri', N'pi pi-send', N'muhasebe/stok-talepleri', @StokDepoYonetimiId, 3, 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Icon] = N'pi pi-send',
                        [ParentId] = @StokDepoYonetimiId,
                        [MenuOrder] = 3,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @StokTalepleriMenuId;
                END;

                SELECT TOP (1) @DepodanCikisMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/stok-talepleri' AND [Label] = N'Depodan Çıkış';

                IF @DepodanCikisMenuId IS NULL
                BEGIN
                    SET @DepodanCikisMenuId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@DepodanCikisMenuId, N'Depodan Çıkış', N'pi pi-arrow-right-arrow-left', N'muhasebe/stok-talepleri', @StokDepoYonetimiId, 4, 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Icon] = N'pi pi-arrow-right-arrow-left',
                        [ParentId] = @StokDepoYonetimiId,
                        [MenuOrder] = 4,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @DepodanCikisMenuId;
                END;

                SELECT TOP (1) @SarfFisleriMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/sarf-fisleri';

                IF @SarfFisleriMenuId IS NULL
                BEGIN
                    SET @SarfFisleriMenuId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@SarfFisleriMenuId, N'Sarf Fişleri', N'pi pi-briefcase', N'muhasebe/sarf-fisleri', @StokDepoYonetimiId, 5, 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Sarf Fişleri',
                        [Icon] = N'pi pi-briefcase',
                        [ParentId] = @StokDepoYonetimiId,
                        [MenuOrder] = 5,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @SarfFisleriMenuId;
                END;

                IF @StokDepoYonetimiId IS NOT NULL AND @StokHareketMenuRoleId IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @StokDepoYonetimiId AND [RoleId] = @StokHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @StokDepoYonetimiId, @StokHareketMenuRoleId, 0, @Now, @Now);

                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                WHERE mir.[MenuItemId] = @StokTalepleriMenuId AND mir.[RoleId] = @StokHareketMenuRoleId;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @StokTalepleriMenuId AND [RoleId] = @StokTalepMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @StokTalepleriMenuId, @StokTalepMenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @DepodanCikisMenuId AND [RoleId] = @StokDepoCikisMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @DepodanCikisMenuId, @StokDepoCikisMenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @SarfFisleriMenuId AND [RoleId] = @SarfMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @SarfFisleriMenuId, @SarfMenuRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SarfFisiSatirlari",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "SarfFisleri",
                schema: "muhasebe");

            migrationBuilder.Sql(
                """
                DECLARE @StokTalepMenuId uniqueidentifier;
                DECLARE @DepodanCikisMenuId uniqueidentifier;
                DECLARE @SarfFisleriMenuId uniqueidentifier;

                SELECT TOP (1) @StokTalepMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/stok-talepleri' AND [Label] = N'Stok Talepleri';
                SELECT TOP (1) @DepodanCikisMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/stok-talepleri' AND [Label] = N'Depodan Çıkış';
                SELECT TOP (1) @SarfFisleriMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/sarf-fisleri';

                DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] IN (@StokTalepMenuId, @DepodanCikisMenuId, @SarfFisleriMenuId);
                DELETE FROM [TODBase].[MenuItems] WHERE [Id] IN (@StokTalepMenuId, @DepodanCikisMenuId, @SarfFisleriMenuId);
                """);
        }
    }
}
