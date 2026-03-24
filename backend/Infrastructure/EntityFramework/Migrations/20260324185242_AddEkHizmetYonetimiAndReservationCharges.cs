using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddEkHizmetYonetimiAndReservationCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EkHizmetTarifeleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BirimAdi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_EkHizmetTarifeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkHizmetTarifeleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RezervasyonEkHizmetler",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonId = table.Column<int>(type: "int", nullable: false),
                    RezervasyonKonaklayanId = table.Column<int>(type: "int", nullable: false),
                    EkHizmetTarifeId = table.Column<int>(type: "int", nullable: false),
                    RezervasyonSegmentId = table.Column<int>(type: "int", nullable: false),
                    OdaId = table.Column<int>(type: "int", nullable: false),
                    YatakNoSnapshot = table.Column<int>(type: "int", nullable: true),
                    TarifeAdiSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BirimAdiSnapshot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OdaNoSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BinaAdiSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HizmetTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Miktar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_RezervasyonEkHizmetler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonEkHizmetler_EkHizmetTarifeleri_EkHizmetTarifeId",
                        column: x => x.EkHizmetTarifeId,
                        principalSchema: "dbo",
                        principalTable: "EkHizmetTarifeleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonEkHizmetler_Odalar_OdaId",
                        column: x => x.OdaId,
                        principalSchema: "dbo",
                        principalTable: "Odalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonEkHizmetler_RezervasyonKonaklayanlar_RezervasyonKonaklayanId",
                        column: x => x.RezervasyonKonaklayanId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonKonaklayanlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonEkHizmetler_RezervasyonSegmentleri_RezervasyonSegmentId",
                        column: x => x.RezervasyonSegmentId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonSegmentleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonEkHizmetler_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EkHizmetTarifeleri_TesisId_Ad_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "EkHizmetTarifeleri",
                columns: new[] { "TesisId", "Ad", "BaslangicTarihi", "BitisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonEkHizmetler_EkHizmetTarifeId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                column: "EkHizmetTarifeId");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonEkHizmetler_OdaId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                column: "OdaId");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonEkHizmetler_RezervasyonId_HizmetTarihi",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                columns: new[] { "RezervasyonId", "HizmetTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonEkHizmetler_RezervasyonKonaklayanId_HizmetTarihi",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                columns: new[] { "RezervasyonKonaklayanId", "HizmetTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonEkHizmetler_RezervasyonSegmentId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                column: "RezervasyonSegmentId");

            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';

                DECLARE @MenuRoleId uniqueidentifier = '8ca6cb52-3baf-4d50-b22f-59df6dc22a21';
                DECLARE @ViewRoleId uniqueidentifier = 'a1f43e94-37bb-4de1-bcb2-7eb7f3693f22';
                DECLARE @ManageRoleId uniqueidentifier = '0bbd0f2a-ea5f-4f96-a50f-3d216bc99523';
                DECLARE @MenuItemId uniqueidentifier = 'd5a6b6de-d0db-4e78-82f0-7a98cf279924';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @MenuRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuRoleId, N'Menu', N'EkHizmetYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ViewRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ViewRoleId, N'View', N'EkHizmetYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ManageRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ManageRoleId, N'Manage', N'EkHizmetYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('7e2c172d-82c2-4b12-bb09-fce7a211f551', @AdminGroupId, @MenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('b10b1173-f613-4578-a89d-94f47f895552', @AdminGroupId, @ViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('00c8b61f-b596-4835-b3ca-13d0c1122553', @AdminGroupId, @ManageRoleId, 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('4b18ba99-060a-4537-af18-f3eff3f60554', @TesisManagerGroupId, @MenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('6f7f12c1-0af0-4a16-a723-2252c7e72555', @TesisManagerGroupId, @ViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('3e741747-5d39-4878-a2bb-42ab1efb9556', @TesisManagerGroupId, @ManageRoleId, 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MenuItemId)
                        INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (@MenuItemId, N'Ek Hizmetler', N'fa-solid fa-bell-concierge', N'ek-hizmetler', NULL, @MainMenuId, 24, 0, @Now, @Now);
                END

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES ('1755e445-1c60-4c22-a647-b24464716557', @MenuItemId, @MenuRoleId, 0, @Now, @Now);
                """);

            migrationBuilder.Sql("""
                SET IDENTITY_INSERT [dbo].[EkHizmetTarifeleri] ON;

                IF EXISTS (SELECT 1 FROM [dbo].[Tesisler] WHERE [Id] = 1)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1001)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1001, 1, N'Ayakkabi Boyama', N'Vale veya resepsiyon uzerinden teslim alinan ayakkabi boyama hizmeti.', N'Adet', 150.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);

                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1002)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1002, 1, N'Kurutemizleme', N'Gomlek, takim veya tekstil urunu icin kurutemizleme hizmeti.', N'Parca', 250.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);

                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1003)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1003, 1, N'Odaya Kahvalti', N'Sabah kahvaltisinin oda servisi ile iletilmesi.', N'Kisi', 350.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);

                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1004)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1004, 1, N'Mini Bar Sepeti', N'Odadaki mini bar tuketimi icin sabit sepet ucreti.', N'Kullanim', 420.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);
                END

                IF EXISTS (SELECT 1 FROM [dbo].[Tesisler] WHERE [Id] = 2)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1005)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1005, 2, N'Ayakkabi Boyama', N'Resepsiyon uzerinden ayni gun teslim ayakkabi boyama.', N'Adet', 175.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);

                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1006)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1006, 2, N'Kurutemizleme', N'24 saat icinde teslim kurutemizleme hizmeti.', N'Parca', 280.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);

                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1007)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1007, 2, N'Odaya Kahvalti', N'Kahvaltinin odaya servis edilmesi.', N'Kisi', 390.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);

                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1008)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1008, 2, N'Havalimani Transferi', N'Havalimani ile tesis arasi tek yon transfer hizmeti.', N'Sefer', 900.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);
                END

                IF EXISTS (SELECT 1 FROM [dbo].[Tesisler] WHERE [Id] = 3)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1009)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1009, 3, N'Utuleme', N'Gomlek veya kiyafet icin utuleme hizmeti.', N'Parca', 120.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);

                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1010)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1010, 3, N'Ek Havlu Seti', N'Oda icin ilave havlu ve buklet seti.', N'Set', 95.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);

                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1011)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1011, 3, N'Odaya Sicak Icecek', N'Cay veya kahvenin odaya servisi.', N'Kisi', 85.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);
                END

                IF EXISTS (SELECT 1 FROM [dbo].[Tesisler] WHERE [Id] = 4)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1012)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1012, 4, N'Sahil Transferi', N'Tesis ile sahil/etkinlik alani arasi transfer hizmeti.', N'Sefer', 650.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);

                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1013)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1013, 4, N'Gec Check-out', N'Standart cikis saatinden sonra oda kullanim uzatmasi.', N'Oda', 750.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);

                    IF NOT EXISTS (SELECT 1 FROM [dbo].[EkHizmetTarifeleri] WHERE [Id] = 1014)
                        INSERT INTO [dbo].[EkHizmetTarifeleri] ([Id], [TesisId], [Ad], [Aciklama], [BirimAdi], [BirimFiyat], [ParaBirimi], [BaslangicTarihi], [BitisTarihi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                        VALUES (1014, 4, N'Meyve Tabagi', N'Odaya meyve tabagi ikram servisi.', N'Servis', 220.00, N'TRY', '2026-01-01T00:00:00', '2026-12-31T23:59:59', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, N'migration_seed_ek_hizmet', N'migration_seed_ek_hizmet', NULL);
                END

                SET IDENTITY_INSERT [dbo].[EkHizmetTarifeleri] OFF;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [TODBase].[MenuItemRoles]
                WHERE [Id] IN ('1755e445-1c60-4c22-a647-b24464716557');

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Id] IN ('d5a6b6de-d0db-4e78-82f0-7a98cf279924');

                DELETE FROM [TODBase].[UserGroupRoles]
                WHERE [Id] IN (
                    '7e2c172d-82c2-4b12-bb09-fce7a211f551',
                    'b10b1173-f613-4578-a89d-94f47f895552',
                    '00c8b61f-b596-4835-b3ca-13d0c1122553',
                    '4b18ba99-060a-4537-af18-f3eff3f60554',
                    '6f7f12c1-0af0-4a16-a723-2252c7e72555',
                    '3e741747-5d39-4878-a2bb-42ab1efb9556'
                );

                DELETE FROM [TODBase].[Roles]
                WHERE [Id] IN (
                    '8ca6cb52-3baf-4d50-b22f-59df6dc22a21',
                    'a1f43e94-37bb-4de1-bcb2-7eb7f3693f22',
                    '0bbd0f2a-ea5f-4f96-a50f-3d216bc99523'
                );
                """);

            migrationBuilder.Sql("""
                DELETE FROM [dbo].[EkHizmetTarifeleri]
                WHERE [Id] IN (1001,1002,1003,1004,1005,1006,1007,1008,1009,1010,1011,1012,1013,1014);
                """);

            migrationBuilder.DropTable(
                name: "RezervasyonEkHizmetler",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EkHizmetTarifeleri",
                schema: "dbo");
        }
    }
}
