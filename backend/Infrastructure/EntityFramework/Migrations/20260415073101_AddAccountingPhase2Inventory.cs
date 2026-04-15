using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingPhase2Inventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Depolar",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: true),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_Depolar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Depolar_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TasinirKodlar",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TamKod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Duzey1Kod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Duzey2Kod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Duzey3Kod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Duzey4Kod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Duzey5Kod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Ad = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DuzeyNo = table.Column<int>(type: "int", nullable: false),
                    UstKodId = table.Column<int>(type: "int", nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TasinirKodlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasinirKodlar_TasinirKodlar_UstKodId",
                        column: x => x.UstKodId,
                        principalSchema: "muhasebe",
                        principalTable: "TasinirKodlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TasinirKartlar",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasinirKodId = table.Column<int>(type: "int", nullable: false),
                    StokKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Birim = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MalzemeTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SarfMi = table.Column<bool>(type: "bit", nullable: false),
                    DemirbasMi = table.Column<bool>(type: "bit", nullable: false),
                    TakipliMi = table.Column<bool>(type: "bit", nullable: false),
                    KdvOrani = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TasinirKartlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasinirKartlar_TasinirKodlar_TasinirKodId",
                        column: x => x.TasinirKodId,
                        principalSchema: "muhasebe",
                        principalTable: "TasinirKodlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StokHareketleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepoId = table.Column<int>(type: "int", nullable: false),
                    TasinirKartId = table.Column<int>(type: "int", nullable: false),
                    HareketTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HareketTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Miktar = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BelgeNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BelgeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CariKartId = table.Column<int>(type: "int", nullable: true),
                    KaynakModul = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    KaynakId = table.Column<int>(type: "int", nullable: true),
                    Durum = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
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
                    table.PrimaryKey("PK_StokHareketleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StokHareketleri_CariKartlar_CariKartId",
                        column: x => x.CariKartId,
                        principalSchema: "muhasebe",
                        principalTable: "CariKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokHareketleri_Depolar_DepoId",
                        column: x => x.DepoId,
                        principalSchema: "muhasebe",
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokHareketleri_TasinirKartlar_TasinirKartId",
                        column: x => x.TasinirKartId,
                        principalSchema: "muhasebe",
                        principalTable: "TasinirKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Depolar_Kod",
                schema: "muhasebe",
                table: "Depolar",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Depolar_TesisId_Ad",
                schema: "muhasebe",
                table: "Depolar",
                columns: new[] { "TesisId", "Ad" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_BelgeNo",
                schema: "muhasebe",
                table: "StokHareketleri",
                column: "BelgeNo",
                filter: "[IsDeleted] = 0 AND [BelgeNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_CariKartId",
                schema: "muhasebe",
                table: "StokHareketleri",
                column: "CariKartId",
                filter: "[IsDeleted] = 0 AND [CariKartId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_DepoId_TasinirKartId_HareketTarihi",
                schema: "muhasebe",
                table: "StokHareketleri",
                columns: new[] { "DepoId", "TasinirKartId", "HareketTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_TasinirKartId",
                schema: "muhasebe",
                table: "StokHareketleri",
                column: "TasinirKartId");

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKartlar_StokKodu",
                schema: "muhasebe",
                table: "TasinirKartlar",
                column: "StokKodu",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKartlar_TasinirKodId_Ad",
                schema: "muhasebe",
                table: "TasinirKartlar",
                columns: new[] { "TasinirKodId", "Ad" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKodlar_DuzeyNo_Ad",
                schema: "muhasebe",
                table: "TasinirKodlar",
                columns: new[] { "DuzeyNo", "Ad" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKodlar_TamKod",
                schema: "muhasebe",
                table: "TasinirKodlar",
                column: "TamKod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKodlar_UstKodId",
                schema: "muhasebe",
                table: "TasinirKodlar",
                column: "UstKodId",
                filter: "[IsDeleted] = 0 AND [UstKodId] IS NOT NULL");

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';

                DECLARE @TasinirKodMenuRoleId uniqueidentifier;
                DECLARE @TasinirKodViewRoleId uniqueidentifier;
                DECLARE @TasinirKodManageRoleId uniqueidentifier;
                DECLARE @TasinirKartMenuRoleId uniqueidentifier;
                DECLARE @TasinirKartViewRoleId uniqueidentifier;
                DECLARE @TasinirKartManageRoleId uniqueidentifier;
                DECLARE @DepoMenuRoleId uniqueidentifier;
                DECLARE @DepoViewRoleId uniqueidentifier;
                DECLARE @DepoManageRoleId uniqueidentifier;
                DECLARE @StokHareketMenuRoleId uniqueidentifier;
                DECLARE @StokHareketViewRoleId uniqueidentifier;
                DECLARE @StokHareketManageRoleId uniqueidentifier;

                SELECT TOP (1) @TasinirKodMenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'TasinirKodYonetimi' AND [Name] = N'Menu';
                IF @TasinirKodMenuRoleId IS NULL BEGIN SET @TasinirKodMenuRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TasinirKodMenuRoleId, N'Menu', N'TasinirKodYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @TasinirKodViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'TasinirKodYonetimi' AND [Name] = N'View';
                IF @TasinirKodViewRoleId IS NULL BEGIN SET @TasinirKodViewRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TasinirKodViewRoleId, N'View', N'TasinirKodYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @TasinirKodManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'TasinirKodYonetimi' AND [Name] = N'Manage';
                IF @TasinirKodManageRoleId IS NULL BEGIN SET @TasinirKodManageRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TasinirKodManageRoleId, N'Manage', N'TasinirKodYonetimi', 0, @Now, @Now); END;

                SELECT TOP (1) @TasinirKartMenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'TasinirKartYonetimi' AND [Name] = N'Menu';
                IF @TasinirKartMenuRoleId IS NULL BEGIN SET @TasinirKartMenuRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TasinirKartMenuRoleId, N'Menu', N'TasinirKartYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @TasinirKartViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'TasinirKartYonetimi' AND [Name] = N'View';
                IF @TasinirKartViewRoleId IS NULL BEGIN SET @TasinirKartViewRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TasinirKartViewRoleId, N'View', N'TasinirKartYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @TasinirKartManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'TasinirKartYonetimi' AND [Name] = N'Manage';
                IF @TasinirKartManageRoleId IS NULL BEGIN SET @TasinirKartManageRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TasinirKartManageRoleId, N'Manage', N'TasinirKartYonetimi', 0, @Now, @Now); END;

                SELECT TOP (1) @DepoMenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'DepoYonetimi' AND [Name] = N'Menu';
                IF @DepoMenuRoleId IS NULL BEGIN SET @DepoMenuRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@DepoMenuRoleId, N'Menu', N'DepoYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @DepoViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'DepoYonetimi' AND [Name] = N'View';
                IF @DepoViewRoleId IS NULL BEGIN SET @DepoViewRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@DepoViewRoleId, N'View', N'DepoYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @DepoManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'DepoYonetimi' AND [Name] = N'Manage';
                IF @DepoManageRoleId IS NULL BEGIN SET @DepoManageRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@DepoManageRoleId, N'Manage', N'DepoYonetimi', 0, @Now, @Now); END;

                SELECT TOP (1) @StokHareketMenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'StokHareketYonetimi' AND [Name] = N'Menu';
                IF @StokHareketMenuRoleId IS NULL BEGIN SET @StokHareketMenuRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@StokHareketMenuRoleId, N'Menu', N'StokHareketYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @StokHareketViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'StokHareketYonetimi' AND [Name] = N'View';
                IF @StokHareketViewRoleId IS NULL BEGIN SET @StokHareketViewRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@StokHareketViewRoleId, N'View', N'StokHareketYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @StokHareketManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'StokHareketYonetimi' AND [Name] = N'Manage';
                IF @StokHareketManageRoleId IS NULL BEGIN SET @StokHareketManageRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@StokHareketManageRoleId, N'Manage', N'StokHareketYonetimi', 0, @Now, @Now); END;

                DECLARE @AllPhaseRoles TABLE (RoleId uniqueidentifier);
                INSERT INTO @AllPhaseRoles(RoleId)
                VALUES
                (@TasinirKodMenuRoleId), (@TasinirKodViewRoleId), (@TasinirKodManageRoleId),
                (@TasinirKartMenuRoleId), (@TasinirKartViewRoleId), (@TasinirKartManageRoleId),
                (@DepoMenuRoleId), (@DepoViewRoleId), (@DepoManageRoleId),
                (@StokHareketMenuRoleId), (@StokHareketViewRoleId), (@StokHareketManageRoleId);

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    SELECT NEWID(), @AdminGroupId, r.RoleId, 0, @Now, @Now
                    FROM @AllPhaseRoles r
                    WHERE NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] ugr WHERE ugr.[UserGroupId] = @AdminGroupId AND ugr.[RoleId] = r.RoleId);
                END;

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    SELECT NEWID(), @TesisManagerGroupId, r.RoleId, 0, @Now, @Now
                    FROM @AllPhaseRoles r
                    WHERE NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] ugr WHERE ugr.[UserGroupId] = @TesisManagerGroupId AND ugr.[RoleId] = r.RoleId);
                END;

                DECLARE @MuhasebeRootId uniqueidentifier;
                DECLARE @TasinirKodMenuId uniqueidentifier;
                DECLARE @TasinirKartMenuId uniqueidentifier;
                DECLARE @DepoMenuId uniqueidentifier;
                DECLARE @StokHareketMenuId uniqueidentifier;

                SELECT TOP (1) @MuhasebeRootId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;
                IF @MuhasebeRootId IS NULL
                BEGIN
                    SET @MuhasebeRootId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MuhasebeRootId, N'Muhasebe', N'pi pi-wallet', N'', NULL, 6, 0, @Now, @Now);
                END;

                SELECT TOP (1) @TasinirKodMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/tasinir-kodlari';
                IF @TasinirKodMenuId IS NULL BEGIN SET @TasinirKodMenuId = NEWID(); INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TasinirKodMenuId, N'Tasinir Kodlari', N'pi pi-sitemap', N'muhasebe/tasinir-kodlari', @MuhasebeRootId, 5, 0, @Now, @Now); END
                ELSE BEGIN UPDATE [TODBase].[MenuItems] SET [Label] = N'Tasinir Kodlari', [Icon] = N'pi pi-sitemap', [ParentId] = @MuhasebeRootId, [MenuOrder] = 5, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @TasinirKodMenuId; END;

                SELECT TOP (1) @TasinirKartMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/tasinir-kartlari';
                IF @TasinirKartMenuId IS NULL BEGIN SET @TasinirKartMenuId = NEWID(); INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TasinirKartMenuId, N'Tasinir Kartlari', N'pi pi-box', N'muhasebe/tasinir-kartlari', @MuhasebeRootId, 6, 0, @Now, @Now); END
                ELSE BEGIN UPDATE [TODBase].[MenuItems] SET [Label] = N'Tasinir Kartlari', [Icon] = N'pi pi-box', [ParentId] = @MuhasebeRootId, [MenuOrder] = 6, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @TasinirKartMenuId; END;

                SELECT TOP (1) @DepoMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/depolar';
                IF @DepoMenuId IS NULL BEGIN SET @DepoMenuId = NEWID(); INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@DepoMenuId, N'Depolar', N'pi pi-warehouse', N'muhasebe/depolar', @MuhasebeRootId, 7, 0, @Now, @Now); END
                ELSE BEGIN UPDATE [TODBase].[MenuItems] SET [Label] = N'Depolar', [Icon] = N'pi pi-warehouse', [ParentId] = @MuhasebeRootId, [MenuOrder] = 7, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @DepoMenuId; END;

                SELECT TOP (1) @StokHareketMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/stok-hareketleri';
                IF @StokHareketMenuId IS NULL BEGIN SET @StokHareketMenuId = NEWID(); INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@StokHareketMenuId, N'Stok Hareketleri', N'pi pi-sort-alt', N'muhasebe/stok-hareketleri', @MuhasebeRootId, 8, 0, @Now, @Now); END
                ELSE BEGIN UPDATE [TODBase].[MenuItems] SET [Label] = N'Stok Hareketleri', [Icon] = N'pi pi-sort-alt', [ParentId] = @MuhasebeRootId, [MenuOrder] = 8, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @StokHareketMenuId; END;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @TasinirKodMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @TasinirKodMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @TasinirKartMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @TasinirKartMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @DepoMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @DepoMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @StokHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @StokHareketMenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @TasinirKodMenuId AND [RoleId] = @TasinirKodMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TasinirKodMenuId, @TasinirKodMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @TasinirKartMenuId AND [RoleId] = @TasinirKartMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TasinirKartMenuId, @TasinirKartMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @DepoMenuId AND [RoleId] = @DepoMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @DepoMenuId, @DepoMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @StokHareketMenuId AND [RoleId] = @StokHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @StokHareketMenuId, @StokHareketMenuRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StokHareketleri",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "Depolar",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "TasinirKartlar",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "TasinirKodlar",
                schema: "muhasebe");

            migrationBuilder.Sql(
                """
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                WHERE mi.[Route] IN (N'muhasebe/tasinir-kodlari', N'muhasebe/tasinir-kartlari', N'muhasebe/depolar', N'muhasebe/stok-hareketleri');

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] IN (N'muhasebe/tasinir-kodlari', N'muhasebe/tasinir-kartlari', N'muhasebe/depolar', N'muhasebe/stok-hareketleri');

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] IN (N'TasinirKodYonetimi', N'TasinirKartYonetimi', N'DepoYonetimi', N'StokHareketYonetimi');
                """);
        }
    }
}
