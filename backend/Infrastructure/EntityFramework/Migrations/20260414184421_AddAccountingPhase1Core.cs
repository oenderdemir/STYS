using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingPhase1Core : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "muhasebe");

            migrationBuilder.CreateTable(
                name: "CariKartlar",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CariTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CariKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UnvanAdSoyad = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    VergiNoTckn = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    VergiDairesi = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Telefon = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Eposta = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Adres = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Il = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Ilce = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    EFaturaMukellefiMi = table.Column<bool>(type: "bit", nullable: false),
                    EArsivKapsamindaMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_CariKartlar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankaHareketleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankaAdi = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HesapKoduIban = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HareketTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HareketTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    BelgeNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_BankaHareketleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankaHareketleri_CariKartlar_CariKartId",
                        column: x => x.CariKartId,
                        principalSchema: "muhasebe",
                        principalTable: "CariKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CariHareketler",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CariKartId = table.Column<int>(type: "int", nullable: false),
                    HareketTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BelgeTuru = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BelgeNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    BorcTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AlacakTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    VadeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Durum = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    KaynakModul = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    KaynakId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_CariHareketler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CariHareketler_CariKartlar_CariKartId",
                        column: x => x.CariKartId,
                        principalSchema: "muhasebe",
                        principalTable: "CariKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KasaHareketleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KasaKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HareketTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HareketTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    BelgeNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_KasaHareketleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KasaHareketleri_CariKartlar_CariKartId",
                        column: x => x.CariKartId,
                        principalSchema: "muhasebe",
                        principalTable: "CariKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TahsilatOdemeBelgeleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BelgeNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BelgeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BelgeTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CariKartId = table.Column<int>(type: "int", nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OdemeYontemi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_TahsilatOdemeBelgeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TahsilatOdemeBelgeleri_CariKartlar_CariKartId",
                        column: x => x.CariKartId,
                        principalSchema: "muhasebe",
                        principalTable: "CariKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankaHareketleri_BankaAdi_HesapKoduIban_HareketTarihi",
                schema: "muhasebe",
                table: "BankaHareketleri",
                columns: new[] { "BankaAdi", "HesapKoduIban", "HareketTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BankaHareketleri_BelgeNo",
                schema: "muhasebe",
                table: "BankaHareketleri",
                column: "BelgeNo",
                filter: "[IsDeleted] = 0 AND [BelgeNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankaHareketleri_CariKartId",
                schema: "muhasebe",
                table: "BankaHareketleri",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_CariHareketler_BelgeNo",
                schema: "muhasebe",
                table: "CariHareketler",
                column: "BelgeNo",
                filter: "[IsDeleted] = 0 AND [BelgeNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CariHareketler_CariKartId_HareketTarihi",
                schema: "muhasebe",
                table: "CariHareketler",
                columns: new[] { "CariKartId", "HareketTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_CariKodu",
                schema: "muhasebe",
                table: "CariKartlar",
                column: "CariKodu",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_CariTipi_UnvanAdSoyad",
                schema: "muhasebe",
                table: "CariKartlar",
                columns: new[] { "CariTipi", "UnvanAdSoyad" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketleri_BelgeNo",
                schema: "muhasebe",
                table: "KasaHareketleri",
                column: "BelgeNo",
                filter: "[IsDeleted] = 0 AND [BelgeNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketleri_CariKartId",
                schema: "muhasebe",
                table: "KasaHareketleri",
                column: "CariKartId");

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketleri_KasaKodu_HareketTarihi",
                schema: "muhasebe",
                table: "KasaHareketleri",
                columns: new[] { "KasaKodu", "HareketTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TahsilatOdemeBelgeleri_BelgeNo",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                column: "BelgeNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TahsilatOdemeBelgeleri_BelgeTarihi_BelgeTipi",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                columns: new[] { "BelgeTarihi", "BelgeTipi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TahsilatOdemeBelgeleri_CariKartId",
                schema: "muhasebe",
                table: "TahsilatOdemeBelgeleri",
                column: "CariKartId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';

                DECLARE @CariKartMenuRoleId uniqueidentifier;
                DECLARE @CariKartViewRoleId uniqueidentifier;
                DECLARE @CariKartManageRoleId uniqueidentifier;
                DECLARE @CariHareketMenuRoleId uniqueidentifier;
                DECLARE @CariHareketViewRoleId uniqueidentifier;
                DECLARE @CariHareketManageRoleId uniqueidentifier;
                DECLARE @KasaHareketMenuRoleId uniqueidentifier;
                DECLARE @KasaHareketViewRoleId uniqueidentifier;
                DECLARE @KasaHareketManageRoleId uniqueidentifier;
                DECLARE @BankaHareketMenuRoleId uniqueidentifier;
                DECLARE @BankaHareketViewRoleId uniqueidentifier;
                DECLARE @BankaHareketManageRoleId uniqueidentifier;
                DECLARE @TahsilatOdemeMenuRoleId uniqueidentifier;
                DECLARE @TahsilatOdemeViewRoleId uniqueidentifier;
                DECLARE @TahsilatOdemeManageRoleId uniqueidentifier;

                SELECT TOP (1) @CariKartMenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'CariKartYonetimi' AND [Name] = N'Menu';
                IF @CariKartMenuRoleId IS NULL BEGIN SET @CariKartMenuRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@CariKartMenuRoleId, N'Menu', N'CariKartYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @CariKartViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'CariKartYonetimi' AND [Name] = N'View';
                IF @CariKartViewRoleId IS NULL BEGIN SET @CariKartViewRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@CariKartViewRoleId, N'View', N'CariKartYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @CariKartManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'CariKartYonetimi' AND [Name] = N'Manage';
                IF @CariKartManageRoleId IS NULL BEGIN SET @CariKartManageRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@CariKartManageRoleId, N'Manage', N'CariKartYonetimi', 0, @Now, @Now); END;

                SELECT TOP (1) @CariHareketMenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'CariHareketYonetimi' AND [Name] = N'Menu';
                IF @CariHareketMenuRoleId IS NULL BEGIN SET @CariHareketMenuRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@CariHareketMenuRoleId, N'Menu', N'CariHareketYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @CariHareketViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'CariHareketYonetimi' AND [Name] = N'View';
                IF @CariHareketViewRoleId IS NULL BEGIN SET @CariHareketViewRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@CariHareketViewRoleId, N'View', N'CariHareketYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @CariHareketManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'CariHareketYonetimi' AND [Name] = N'Manage';
                IF @CariHareketManageRoleId IS NULL BEGIN SET @CariHareketManageRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@CariHareketManageRoleId, N'Manage', N'CariHareketYonetimi', 0, @Now, @Now); END;

                SELECT TOP (1) @KasaHareketMenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'KasaHareketYonetimi' AND [Name] = N'Menu';
                IF @KasaHareketMenuRoleId IS NULL BEGIN SET @KasaHareketMenuRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@KasaHareketMenuRoleId, N'Menu', N'KasaHareketYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @KasaHareketViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'KasaHareketYonetimi' AND [Name] = N'View';
                IF @KasaHareketViewRoleId IS NULL BEGIN SET @KasaHareketViewRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@KasaHareketViewRoleId, N'View', N'KasaHareketYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @KasaHareketManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'KasaHareketYonetimi' AND [Name] = N'Manage';
                IF @KasaHareketManageRoleId IS NULL BEGIN SET @KasaHareketManageRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@KasaHareketManageRoleId, N'Manage', N'KasaHareketYonetimi', 0, @Now, @Now); END;

                SELECT TOP (1) @BankaHareketMenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'BankaHareketYonetimi' AND [Name] = N'Menu';
                IF @BankaHareketMenuRoleId IS NULL BEGIN SET @BankaHareketMenuRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@BankaHareketMenuRoleId, N'Menu', N'BankaHareketYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @BankaHareketViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'BankaHareketYonetimi' AND [Name] = N'View';
                IF @BankaHareketViewRoleId IS NULL BEGIN SET @BankaHareketViewRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@BankaHareketViewRoleId, N'View', N'BankaHareketYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @BankaHareketManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'BankaHareketYonetimi' AND [Name] = N'Manage';
                IF @BankaHareketManageRoleId IS NULL BEGIN SET @BankaHareketManageRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@BankaHareketManageRoleId, N'Manage', N'BankaHareketYonetimi', 0, @Now, @Now); END;

                SELECT TOP (1) @TahsilatOdemeMenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'TahsilatOdemeBelgesiYonetimi' AND [Name] = N'Menu';
                IF @TahsilatOdemeMenuRoleId IS NULL BEGIN SET @TahsilatOdemeMenuRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TahsilatOdemeMenuRoleId, N'Menu', N'TahsilatOdemeBelgesiYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @TahsilatOdemeViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'TahsilatOdemeBelgesiYonetimi' AND [Name] = N'View';
                IF @TahsilatOdemeViewRoleId IS NULL BEGIN SET @TahsilatOdemeViewRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TahsilatOdemeViewRoleId, N'View', N'TahsilatOdemeBelgesiYonetimi', 0, @Now, @Now); END;
                SELECT TOP (1) @TahsilatOdemeManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'TahsilatOdemeBelgesiYonetimi' AND [Name] = N'Manage';
                IF @TahsilatOdemeManageRoleId IS NULL BEGIN SET @TahsilatOdemeManageRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TahsilatOdemeManageRoleId, N'Manage', N'TahsilatOdemeBelgesiYonetimi', 0, @Now, @Now); END;

                DECLARE @AllPhaseRoles TABLE (RoleId uniqueidentifier);
                INSERT INTO @AllPhaseRoles(RoleId)
                VALUES
                (@CariKartMenuRoleId), (@CariKartViewRoleId), (@CariKartManageRoleId),
                (@CariHareketMenuRoleId), (@CariHareketViewRoleId), (@CariHareketManageRoleId),
                (@KasaHareketMenuRoleId), (@KasaHareketViewRoleId), (@KasaHareketManageRoleId),
                (@BankaHareketMenuRoleId), (@BankaHareketViewRoleId), (@BankaHareketManageRoleId),
                (@TahsilatOdemeMenuRoleId), (@TahsilatOdemeViewRoleId), (@TahsilatOdemeManageRoleId);

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
                DECLARE @CariKartMenuId uniqueidentifier;
                DECLARE @CariHareketMenuId uniqueidentifier;
                DECLARE @KasaHareketMenuId uniqueidentifier;
                DECLARE @BankaHareketMenuId uniqueidentifier;
                DECLARE @TahsilatOdemeMenuId uniqueidentifier;

                SELECT TOP (1) @MuhasebeRootId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;
                IF @MuhasebeRootId IS NULL
                BEGIN
                    SET @MuhasebeRootId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MuhasebeRootId, N'Muhasebe', N'pi pi-wallet', N'', NULL, 6, 0, @Now, @Now);
                END;

                SELECT TOP (1) @CariKartMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/cari-kartlar' AND [IsDeleted] = 0;
                IF @CariKartMenuId IS NULL BEGIN SET @CariKartMenuId = NEWID(); INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@CariKartMenuId, N'Cari Kartlar', N'pi pi-id-card', N'muhasebe/cari-kartlar', @MuhasebeRootId, 0, 0, @Now, @Now); END
                ELSE BEGIN UPDATE [TODBase].[MenuItems] SET [Label] = N'Cari Kartlar', [Icon] = N'pi pi-id-card', [ParentId] = @MuhasebeRootId, [MenuOrder] = 0, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @CariKartMenuId; END;

                SELECT TOP (1) @CariHareketMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/cari-hareketler' AND [IsDeleted] = 0;
                IF @CariHareketMenuId IS NULL BEGIN SET @CariHareketMenuId = NEWID(); INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@CariHareketMenuId, N'Cari Hareketler', N'pi pi-list-check', N'muhasebe/cari-hareketler', @MuhasebeRootId, 1, 0, @Now, @Now); END
                ELSE BEGIN UPDATE [TODBase].[MenuItems] SET [Label] = N'Cari Hareketler', [Icon] = N'pi pi-list-check', [ParentId] = @MuhasebeRootId, [MenuOrder] = 1, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @CariHareketMenuId; END;

                SELECT TOP (1) @KasaHareketMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/kasa-hareketleri' AND [IsDeleted] = 0;
                IF @KasaHareketMenuId IS NULL BEGIN SET @KasaHareketMenuId = NEWID(); INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@KasaHareketMenuId, N'Kasa Hareketleri', N'pi pi-money-bill', N'muhasebe/kasa-hareketleri', @MuhasebeRootId, 2, 0, @Now, @Now); END
                ELSE BEGIN UPDATE [TODBase].[MenuItems] SET [Label] = N'Kasa Hareketleri', [Icon] = N'pi pi-money-bill', [ParentId] = @MuhasebeRootId, [MenuOrder] = 2, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @KasaHareketMenuId; END;

                SELECT TOP (1) @BankaHareketMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/banka-hareketleri' AND [IsDeleted] = 0;
                IF @BankaHareketMenuId IS NULL BEGIN SET @BankaHareketMenuId = NEWID(); INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@BankaHareketMenuId, N'Banka Hareketleri', N'pi pi-building-columns', N'muhasebe/banka-hareketleri', @MuhasebeRootId, 3, 0, @Now, @Now); END
                ELSE BEGIN UPDATE [TODBase].[MenuItems] SET [Label] = N'Banka Hareketleri', [Icon] = N'pi pi-building-columns', [ParentId] = @MuhasebeRootId, [MenuOrder] = 3, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @BankaHareketMenuId; END;

                SELECT TOP (1) @TahsilatOdemeMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/tahsilat-odeme-belgeleri' AND [IsDeleted] = 0;
                IF @TahsilatOdemeMenuId IS NULL BEGIN SET @TahsilatOdemeMenuId = NEWID(); INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@TahsilatOdemeMenuId, N'Tahsilat/Odeme Belgeleri', N'pi pi-receipt', N'muhasebe/tahsilat-odeme-belgeleri', @MuhasebeRootId, 4, 0, @Now, @Now); END
                ELSE BEGIN UPDATE [TODBase].[MenuItems] SET [Label] = N'Tahsilat/Odeme Belgeleri', [Icon] = N'pi pi-receipt', [ParentId] = @MuhasebeRootId, [MenuOrder] = 4, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @TahsilatOdemeMenuId; END;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @CariKartMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @CariKartMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @CariHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @CariHareketMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @KasaHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @KasaHareketMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @BankaHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @BankaHareketMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @TahsilatOdemeMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @TahsilatOdemeMenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @CariKartMenuId AND [RoleId] = @CariKartMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @CariKartMenuId, @CariKartMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @CariHareketMenuId AND [RoleId] = @CariHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @CariHareketMenuId, @CariHareketMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KasaHareketMenuId AND [RoleId] = @KasaHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @KasaHareketMenuId, @KasaHareketMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @BankaHareketMenuId AND [RoleId] = @BankaHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @BankaHareketMenuId, @BankaHareketMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @TahsilatOdemeMenuId AND [RoleId] = @TahsilatOdemeMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TahsilatOdemeMenuId, @TahsilatOdemeMenuRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankaHareketleri",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "CariHareketler",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "KasaHareketleri",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "TahsilatOdemeBelgeleri",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "CariKartlar",
                schema: "muhasebe");

            migrationBuilder.Sql(
                """
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                WHERE mi.[Route] IN (N'muhasebe/cari-kartlar', N'muhasebe/cari-hareketler', N'muhasebe/kasa-hareketleri', N'muhasebe/banka-hareketleri', N'muhasebe/tahsilat-odeme-belgeleri')
                   OR (mi.[Label] = N'Muhasebe' AND mi.[ParentId] IS NULL);

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] IN (N'muhasebe/cari-kartlar', N'muhasebe/cari-hareketler', N'muhasebe/kasa-hareketleri', N'muhasebe/banka-hareketleri', N'muhasebe/tahsilat-odeme-belgeleri');

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Muhasebe'
                  AND [ParentId] IS NULL
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] x WHERE x.[ParentId] = [TODBase].[MenuItems].[Id] AND x.[IsDeleted] = 0);

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] IN (N'CariKartYonetimi', N'CariHareketYonetimi', N'KasaHareketYonetimi', N'BankaHareketYonetimi', N'TahsilatOdemeBelgesiYonetimi');
                """);
        }
    }
}
