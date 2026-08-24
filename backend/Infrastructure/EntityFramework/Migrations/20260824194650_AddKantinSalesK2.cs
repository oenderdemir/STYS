using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKantinSalesK2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KantinSatislar",
                schema: "kantin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    KantinId = table.Column<int>(type: "int", nullable: false),
                    SatisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MatrahToplami = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KdvToplami = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    KesinlesmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_KantinSatislar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KantinSatislar_Kantinler_KantinId",
                        column: x => x.KantinId,
                        principalSchema: "kantin",
                        principalTable: "Kantinler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KantinSatisOdemeleri",
                schema: "kantin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KantinSatisId = table.Column<int>(type: "int", nullable: false),
                    OdemeYontemi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KasaBankaHesapId = table.Column<int>(type: "int", nullable: true),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HesapKodSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HesapAdSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_KantinSatisOdemeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KantinSatisOdemeleri_KantinSatislar_KantinSatisId",
                        column: x => x.KantinSatisId,
                        principalSchema: "kantin",
                        principalTable: "KantinSatislar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KantinSatisOdemeleri_KasaBankaHesaplari_KasaBankaHesapId",
                        column: x => x.KasaBankaHesapId,
                        principalSchema: "muhasebe",
                        principalTable: "KasaBankaHesaplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KantinSatisSatirlari",
                schema: "kantin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KantinSatisId = table.Column<int>(type: "int", nullable: false),
                    KantinUrunId = table.Column<int>(type: "int", nullable: false),
                    TasinirKartId = table.Column<int>(type: "int", nullable: false),
                    Miktar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BirimSatisFiyati = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KdvOrani = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Matrah = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KdvTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StokLotId = table.Column<int>(type: "int", nullable: true),
                    StokSeriId = table.Column<int>(type: "int", nullable: true),
                    StokHareketId = table.Column<int>(type: "int", nullable: true),
                    Barkod = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StokKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UrunAdi = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Birim = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TakipTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LotNo = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SonKullanmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SeriNo = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_KantinSatisSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KantinSatisSatirlari_KantinSatislar_KantinSatisId",
                        column: x => x.KantinSatisId,
                        principalSchema: "kantin",
                        principalTable: "KantinSatislar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KantinSatisSatirlari_KantinUrunleri_KantinUrunId",
                        column: x => x.KantinUrunId,
                        principalSchema: "kantin",
                        principalTable: "KantinUrunleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KantinSatisSatirlari_StokHareketleri_StokHareketId",
                        column: x => x.StokHareketId,
                        principalSchema: "muhasebe",
                        principalTable: "StokHareketleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KantinSatisSatirlari_StokLotlar_StokLotId",
                        column: x => x.StokLotId,
                        principalSchema: "muhasebe",
                        principalTable: "StokLotlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KantinSatisSatirlari_StokSeriler_StokSeriId",
                        column: x => x.StokSeriId,
                        principalSchema: "muhasebe",
                        principalTable: "StokSeriler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KantinSatisSatirlari_TasinirKartlar_TasinirKartId",
                        column: x => x.TasinirKartId,
                        principalSchema: "muhasebe",
                        principalTable: "TasinirKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatislar_KantinId",
                schema: "kantin",
                table: "KantinSatislar",
                column: "KantinId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatislar_TesisId",
                schema: "kantin",
                table: "KantinSatislar",
                column: "TesisId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisOdemeleri_KantinSatisId",
                schema: "kantin",
                table: "KantinSatisOdemeleri",
                column: "KantinSatisId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisOdemeleri_KasaBankaHesapId",
                schema: "kantin",
                table: "KantinSatisOdemeleri",
                column: "KasaBankaHesapId",
                filter: "[IsDeleted] = 0 AND [KasaBankaHesapId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisSatirlari_KantinSatisId",
                schema: "kantin",
                table: "KantinSatisSatirlari",
                column: "KantinSatisId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisSatirlari_KantinSatisId_KantinUrunId",
                schema: "kantin",
                table: "KantinSatisSatirlari",
                columns: new[] { "KantinSatisId", "KantinUrunId" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisSatirlari_KantinUrunId",
                schema: "kantin",
                table: "KantinSatisSatirlari",
                column: "KantinUrunId");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisSatirlari_StokHareketId",
                schema: "kantin",
                table: "KantinSatisSatirlari",
                column: "StokHareketId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [StokHareketId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisSatirlari_StokLotId",
                schema: "kantin",
                table: "KantinSatisSatirlari",
                column: "StokLotId",
                filter: "[IsDeleted] = 0 AND [StokLotId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisSatirlari_StokSeriId",
                schema: "kantin",
                table: "KantinSatisSatirlari",
                column: "StokSeriId",
                filter: "[IsDeleted] = 0 AND [StokSeriId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisSatirlari_TasinirKartId",
                schema: "kantin",
                table: "KantinSatisSatirlari",
                column: "TasinirKartId");

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @KantinSatisMenuRoleId uniqueidentifier;
                DECLARE @KantinSatisViewRoleId uniqueidentifier;
                DECLARE @KantinSatisCreateRoleId uniqueidentifier;
                DECLARE @KantinRootMenuId uniqueidentifier;
                DECLARE @HizliSatisMenuId uniqueidentifier;

                SELECT TOP (1) @KantinSatisMenuRoleId = [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'KantinSatisYonetimi' AND [Name] = N'Menu';

                IF @KantinSatisMenuRoleId IS NULL
                BEGIN
                    SET @KantinSatisMenuRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KantinSatisMenuRoleId, N'Menu', N'KantinSatisYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @KantinSatisViewRoleId = [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'KantinSatisYonetimi' AND [Name] = N'View';

                IF @KantinSatisViewRoleId IS NULL
                BEGIN
                    SET @KantinSatisViewRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KantinSatisViewRoleId, N'View', N'KantinSatisYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @KantinSatisCreateRoleId = [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'KantinSatisYonetimi' AND [Name] = N'Create';

                IF @KantinSatisCreateRoleId IS NULL
                BEGIN
                    SET @KantinSatisCreateRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KantinSatisCreateRoleId, N'Create', N'KantinSatisYonetimi', 0, @Now, @Now);
                END;

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @KantinSatisMenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (NEWID(), @AdminGroupId, @KantinSatisMenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @KantinSatisViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (NEWID(), @AdminGroupId, @KantinSatisViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @KantinSatisCreateRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (NEWID(), @AdminGroupId, @KantinSatisCreateRoleId, 0, @Now, @Now);
                END;

                SELECT TOP (1) @KantinRootMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Kantin Yönetimi'
                  AND [ParentId] IS NULL;

                IF @KantinRootMenuId IS NULL
                BEGIN
                    SET @KantinRootMenuId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KantinRootMenuId, N'Kantin Yönetimi', N'pi pi-shop', N'', NULL, NULL, 6, 0, @Now, @Now);
                END;

                SELECT TOP (1) @HizliSatisMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'kantin-satis';

                IF @HizliSatisMenuId IS NULL
                BEGIN
                    SET @HizliSatisMenuId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@HizliSatisMenuId, N'Hızlı Satış', N'pi pi-bolt', N'kantin-satis', NULL, @KantinRootMenuId, 1, 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Hızlı Satış',
                        [Icon] = N'pi pi-bolt',
                        [ParentId] = @KantinRootMenuId,
                        [MenuOrder] = 1,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @HizliSatisMenuId;
                END;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KantinRootMenuId AND [RoleId] = @KantinSatisMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @KantinRootMenuId, @KantinSatisMenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @HizliSatisMenuId AND [RoleId] = @KantinSatisMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @HizliSatisMenuId, @KantinSatisMenuRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KantinSatisOdemeleri",
                schema: "kantin");

            migrationBuilder.DropTable(
                name: "KantinSatisSatirlari",
                schema: "kantin");

            migrationBuilder.DropTable(
                name: "KantinSatislar",
                schema: "kantin");

            migrationBuilder.Sql(
                """
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                WHERE mi.[Route] = N'kantin-satis';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] = N'kantin-satis';

                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE r.[Domain] = N'KantinSatisYonetimi';

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] = N'KantinSatisYonetimi';
                """);
        }
    }
}
