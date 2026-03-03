using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingAndDiscountManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IndirimKurallari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IndirimTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Deger = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KapsamTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: true),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Oncelik = table.Column<int>(type: "int", nullable: false),
                    BirlesebilirMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_IndirimKurallari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndirimKurallari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KonaklamaTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
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
                    table.PrimaryKey("PK_KonaklamaTipleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MisafirTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
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
                    table.PrimaryKey("PK_MisafirTipleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IndirimKuraliKonaklamaTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IndirimKuraliId = table.Column<int>(type: "int", nullable: false),
                    KonaklamaTipiId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_IndirimKuraliKonaklamaTipleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndirimKuraliKonaklamaTipleri_IndirimKurallari_IndirimKuraliId",
                        column: x => x.IndirimKuraliId,
                        principalSchema: "dbo",
                        principalTable: "IndirimKurallari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IndirimKuraliKonaklamaTipleri_KonaklamaTipleri_KonaklamaTipiId",
                        column: x => x.KonaklamaTipiId,
                        principalSchema: "dbo",
                        principalTable: "KonaklamaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IndirimKuraliMisafirTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IndirimKuraliId = table.Column<int>(type: "int", nullable: false),
                    MisafirTipiId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_IndirimKuraliMisafirTipleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndirimKuraliMisafirTipleri_IndirimKurallari_IndirimKuraliId",
                        column: x => x.IndirimKuraliId,
                        principalSchema: "dbo",
                        principalTable: "IndirimKurallari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IndirimKuraliMisafirTipleri_MisafirTipleri_MisafirTipiId",
                        column: x => x.MisafirTipiId,
                        principalSchema: "dbo",
                        principalTable: "MisafirTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OdaFiyatlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisOdaTipiId = table.Column<int>(type: "int", nullable: false),
                    KonaklamaTipiId = table.Column<int>(type: "int", nullable: false),
                    MisafirTipiId = table.Column<int>(type: "int", nullable: false),
                    KisiSayisi = table.Column<int>(type: "int", nullable: false),
                    Fiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_OdaFiyatlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OdaFiyatlari_KonaklamaTipleri_KonaklamaTipiId",
                        column: x => x.KonaklamaTipiId,
                        principalSchema: "dbo",
                        principalTable: "KonaklamaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdaFiyatlari_MisafirTipleri_MisafirTipiId",
                        column: x => x.MisafirTipiId,
                        principalSchema: "dbo",
                        principalTable: "MisafirTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdaFiyatlari_TesisOdaTipleri_TesisOdaTipiId",
                        column: x => x.TesisOdaTipiId,
                        principalSchema: "dbo",
                        principalTable: "TesisOdaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndirimKuraliKonaklamaTipleri_IndirimKuraliId_KonaklamaTipiId",
                schema: "dbo",
                table: "IndirimKuraliKonaklamaTipleri",
                columns: new[] { "IndirimKuraliId", "KonaklamaTipiId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IndirimKuraliKonaklamaTipleri_KonaklamaTipiId",
                schema: "dbo",
                table: "IndirimKuraliKonaklamaTipleri",
                column: "KonaklamaTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_IndirimKuraliMisafirTipleri_IndirimKuraliId_MisafirTipiId",
                schema: "dbo",
                table: "IndirimKuraliMisafirTipleri",
                columns: new[] { "IndirimKuraliId", "MisafirTipiId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IndirimKuraliMisafirTipleri_MisafirTipiId",
                schema: "dbo",
                table: "IndirimKuraliMisafirTipleri",
                column: "MisafirTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_IndirimKurallari_Kod",
                schema: "dbo",
                table: "IndirimKurallari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IndirimKurallari_TesisId",
                schema: "dbo",
                table: "IndirimKurallari",
                column: "TesisId");

            migrationBuilder.CreateIndex(
                name: "IX_KonaklamaTipleri_Ad",
                schema: "dbo",
                table: "KonaklamaTipleri",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KonaklamaTipleri_Kod",
                schema: "dbo",
                table: "KonaklamaTipleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MisafirTipleri_Ad",
                schema: "dbo",
                table: "MisafirTipleri",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MisafirTipleri_Kod",
                schema: "dbo",
                table: "MisafirTipleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OdaFiyatlari_KonaklamaTipiId",
                schema: "dbo",
                table: "OdaFiyatlari",
                column: "KonaklamaTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_OdaFiyatlari_MisafirTipiId",
                schema: "dbo",
                table: "OdaFiyatlari",
                column: "MisafirTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_OdaFiyatlari_TesisOdaTipiId_KonaklamaTipiId_MisafirTipiId_KisiSayisi_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "OdaFiyatlari",
                columns: new[] { "TesisOdaTipiId", "KonaklamaTipiId", "MisafirTipiId", "KisiSayisi", "BaslangicTarihi", "BitisTarihi" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                -- Roles
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'OdaFiyatYonetimi' AND [Name] = N'Menu')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('8d0f91f6-3218-4b89-8ec2-188ca4df7540', N'Menu', N'OdaFiyatYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'OdaFiyatYonetimi' AND [Name] = N'View')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('3452ebf0-e31f-4549-a55d-f2f68a8bd025', N'View', N'OdaFiyatYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'OdaFiyatYonetimi' AND [Name] = N'Manage')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('7f2975ab-09ad-4626-9ddb-950e242f7bda', N'Manage', N'OdaFiyatYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KonaklamaTipiYonetimi' AND [Name] = N'Menu')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('f85ddb6d-685d-4c4e-91f4-1616c8eb6a14', N'Menu', N'KonaklamaTipiYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KonaklamaTipiYonetimi' AND [Name] = N'View')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('3ff66c4e-a486-48db-97f8-35521d2f7755', N'View', N'KonaklamaTipiYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KonaklamaTipiYonetimi' AND [Name] = N'Manage')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('6b5c3b2b-77cf-4c79-a855-b4ea8e20f791', N'Manage', N'KonaklamaTipiYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'MisafirTipiYonetimi' AND [Name] = N'Menu')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('ffb4903f-7b3f-41c5-b0ac-b7f4ea4a2393', N'Menu', N'MisafirTipiYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'MisafirTipiYonetimi' AND [Name] = N'View')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('f7b2732d-26a5-4fd8-a374-6c40d0a4f86f', N'View', N'MisafirTipiYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'MisafirTipiYonetimi' AND [Name] = N'Manage')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('95e5b66c-df4b-49cf-9963-cfd9022b7172', N'Manage', N'MisafirTipiYonetimi', 0, @Now, @Now);

                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';

                -- Admin group role grants
                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = '8d0f91f6-3218-4b89-8ec2-188ca4df7540')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('f6109ff6-0f5f-4ebb-b557-3234f40ea318', @AdminGroupId, '8d0f91f6-3218-4b89-8ec2-188ca4df7540', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = '3452ebf0-e31f-4549-a55d-f2f68a8bd025')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('77e2b0de-ead6-4848-ad95-ac1ca8f52ca3', @AdminGroupId, '3452ebf0-e31f-4549-a55d-f2f68a8bd025', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = '7f2975ab-09ad-4626-9ddb-950e242f7bda')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('232f6c76-2fb8-4e6f-8b90-c75a28d35f58', @AdminGroupId, '7f2975ab-09ad-4626-9ddb-950e242f7bda', 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = 'f85ddb6d-685d-4c4e-91f4-1616c8eb6a14')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('e8f20389-67d2-4a95-a593-1560e003d359', @AdminGroupId, 'f85ddb6d-685d-4c4e-91f4-1616c8eb6a14', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = '3ff66c4e-a486-48db-97f8-35521d2f7755')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('f4d24552-4c6d-4a5d-b42a-8073d9da00ce', @AdminGroupId, '3ff66c4e-a486-48db-97f8-35521d2f7755', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = '6b5c3b2b-77cf-4c79-a855-b4ea8e20f791')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('7138e4b3-c422-493f-8ec0-5258f6f2e4a6', @AdminGroupId, '6b5c3b2b-77cf-4c79-a855-b4ea8e20f791', 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = 'ffb4903f-7b3f-41c5-b0ac-b7f4ea4a2393')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('7e4f3bc1-9df4-4874-a6be-dca5b26f8357', @AdminGroupId, 'ffb4903f-7b3f-41c5-b0ac-b7f4ea4a2393', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = 'f7b2732d-26a5-4fd8-a374-6c40d0a4f86f')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('d8136f90-527f-46c9-b5ee-4b071dd95d45', @AdminGroupId, 'f7b2732d-26a5-4fd8-a374-6c40d0a4f86f', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = '95e5b66c-df4b-49cf-9963-cfd9022b7172')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('d7745f34-ec5d-482f-a899-2bf0cb751fdc', @AdminGroupId, '95e5b66c-df4b-49cf-9963-cfd9022b7172', 0, @Now, @Now);
                END

                -- Tesis manager group gets only OdaFiyatYonetimi
                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = '8d0f91f6-3218-4b89-8ec2-188ca4df7540')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4a90c53d-89db-42dd-b4bc-8846a54ee96f', @TesisManagerGroupId, '8d0f91f6-3218-4b89-8ec2-188ca4df7540', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = '3452ebf0-e31f-4549-a55d-f2f68a8bd025')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4f74715f-ebd8-408d-b5d6-ef2690f4d656', @TesisManagerGroupId, '3452ebf0-e31f-4549-a55d-f2f68a8bd025', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = '7f2975ab-09ad-4626-9ddb-950e242f7bda')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('f6fdddb5-f4d5-496f-8af0-60d188f95af5', @TesisManagerGroupId, '7f2975ab-09ad-4626-9ddb-950e242f7bda', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = '3ff66c4e-a486-48db-97f8-35521d2f7755')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('1317a5f9-bbfd-4755-96ec-df72b7d8a5fd', @TesisManagerGroupId, '3ff66c4e-a486-48db-97f8-35521d2f7755', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = 'f7b2732d-26a5-4fd8-a374-6c40d0a4f86f')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('2596ab42-d7c7-4736-a6c6-0fd2f3018ef9', @TesisManagerGroupId, 'f7b2732d-26a5-4fd8-a374-6c40d0a4f86f', 0, @Now, @Now);
                END

                -- Menu items
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = 'fb2d3f66-c4f4-4a88-a271-869f7f560f0d')
                        INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('fb2d3f66-c4f4-4a88-a271-869f7f560f0d', N'Oda Fiyatlari', N'fa-solid fa-money-bill-wave', N'oda-fiyatlari', NULL, @MainMenuId, 17, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = '8dad95a9-f0f9-4341-a2f7-6d02009de8ec')
                        INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('8dad95a9-f0f9-4341-a2f7-6d02009de8ec', N'Konaklama Tipleri', N'fa-solid fa-bed', N'konaklama-tipleri', NULL, @MainMenuId, 18, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = 'c5f3b680-8903-4392-b4e2-2dd245ff4778')
                        INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('c5f3b680-8903-4392-b4e2-2dd245ff4778', N'Misafir Tipleri', N'fa-solid fa-user-group', N'misafir-tipleri', NULL, @MainMenuId, 19, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = 'f6a88f07-1536-4de2-8a89-9a5b84a2a4f0')
                        INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('f6a88f07-1536-4de2-8a89-9a5b84a2a4f0', N'Indirim Kurallari', N'fa-solid fa-tags', N'indirim-kurallari', NULL, @MainMenuId, 20, 0, @Now, @Now);
                END

                -- Menu role mappings
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = 'fb2d3f66-c4f4-4a88-a271-869f7f560f0d' AND [RoleId] = '8d0f91f6-3218-4b89-8ec2-188ca4df7540')
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('67a87f3c-c168-4e6b-a8ff-0ee41708915c', 'fb2d3f66-c4f4-4a88-a271-869f7f560f0d', '8d0f91f6-3218-4b89-8ec2-188ca4df7540', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = '8dad95a9-f0f9-4341-a2f7-6d02009de8ec' AND [RoleId] = 'f85ddb6d-685d-4c4e-91f4-1616c8eb6a14')
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('226d0533-22fd-4b65-96ec-80a127beea0c', '8dad95a9-f0f9-4341-a2f7-6d02009de8ec', 'f85ddb6d-685d-4c4e-91f4-1616c8eb6a14', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = 'c5f3b680-8903-4392-b4e2-2dd245ff4778' AND [RoleId] = 'ffb4903f-7b3f-41c5-b0ac-b7f4ea4a2393')
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('047276ad-f022-4178-b626-f2ca8fe4864d', 'c5f3b680-8903-4392-b4e2-2dd245ff4778', 'ffb4903f-7b3f-41c5-b0ac-b7f4ea4a2393', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = 'f6a88f07-1536-4de2-8a89-9a5b84a2a4f0' AND [RoleId] = '8d0f91f6-3218-4b89-8ec2-188ca4df7540')
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('3d21435f-874f-4b8d-b0d5-9a57e8836edb', 'f6a88f07-1536-4de2-8a89-9a5b84a2a4f0', '8d0f91f6-3218-4b89-8ec2-188ca4df7540', 0, @Now, @Now);

                -- Master data: KonaklamaTipleri
                IF NOT EXISTS (SELECT 1 FROM [dbo].[KonaklamaTipleri] WHERE [Kod] = N'SADECE_ODA')
                    INSERT INTO [dbo].[KonaklamaTipleri] ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (N'SADECE_ODA', N'Sadece Oda', 1, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [dbo].[KonaklamaTipleri] WHERE [Kod] = N'ODA_KAHVALTI')
                    INSERT INTO [dbo].[KonaklamaTipleri] ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (N'ODA_KAHVALTI', N'Oda Kahvalti', 1, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [dbo].[KonaklamaTipleri] WHERE [Kod] = N'YARIM_PANSIYON')
                    INSERT INTO [dbo].[KonaklamaTipleri] ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (N'YARIM_PANSIYON', N'Yarim Pansiyon', 1, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [dbo].[KonaklamaTipleri] WHERE [Kod] = N'ALKOLSUZ_HER_SEY_DAHIL')
                    INSERT INTO [dbo].[KonaklamaTipleri] ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (N'ALKOLSUZ_HER_SEY_DAHIL', N'Alkolsuz Her Sey Dahil', 1, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [dbo].[KonaklamaTipleri] WHERE [Kod] = N'ULTRA_HER_SEY_DAHIL')
                    INSERT INTO [dbo].[KonaklamaTipleri] ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (N'ULTRA_HER_SEY_DAHIL', N'Ultra Her Sey Dahil', 1, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [dbo].[KonaklamaTipleri] WHERE [Kod] = N'HER_SEY_DAHIL')
                    INSERT INTO [dbo].[KonaklamaTipleri] ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (N'HER_SEY_DAHIL', N'Her Sey Dahil', 1, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [dbo].[KonaklamaTipleri] WHERE [Kod] = N'TAM_PANSIYON')
                    INSERT INTO [dbo].[KonaklamaTipleri] ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (N'TAM_PANSIYON', N'Tam Pansiyon', 1, 0, @Now, @Now);

                -- Master data: MisafirTipleri
                IF NOT EXISTS (SELECT 1 FROM [dbo].[MisafirTipleri] WHERE [Kod] = N'MISAFIR')
                    INSERT INTO [dbo].[MisafirTipleri] ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (N'MISAFIR', N'Misafir', 1, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [dbo].[MisafirTipleri] WHERE [Kod] = N'KAMU_PERSONELI')
                    INSERT INTO [dbo].[MisafirTipleri] ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (N'KAMU_PERSONELI', N'Kamu Personeli', 1, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [dbo].[MisafirTipleri] WHERE [Kod] = N'KURUM_PERSONELI')
                    INSERT INTO [dbo].[MisafirTipleri] ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (N'KURUM_PERSONELI', N'Kurum Personeli', 1, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [TODBase].[MenuItemRoles] WHERE [Id] IN (
                    '67a87f3c-c168-4e6b-a8ff-0ee41708915c',
                    '226d0533-22fd-4b65-96ec-80a127beea0c',
                    '047276ad-f022-4178-b626-f2ca8fe4864d',
                    '3d21435f-874f-4b8d-b0d5-9a57e8836edb'
                );

                DELETE FROM [TODBase].[MenuItems] WHERE [Id] IN (
                    'fb2d3f66-c4f4-4a88-a271-869f7f560f0d',
                    '8dad95a9-f0f9-4341-a2f7-6d02009de8ec',
                    'c5f3b680-8903-4392-b4e2-2dd245ff4778',
                    'f6a88f07-1536-4de2-8a89-9a5b84a2a4f0'
                );

                DELETE FROM [TODBase].[UserGroupRoles] WHERE [Id] IN (
                    'f6109ff6-0f5f-4ebb-b557-3234f40ea318',
                    '77e2b0de-ead6-4848-ad95-ac1ca8f52ca3',
                    '232f6c76-2fb8-4e6f-8b90-c75a28d35f58',
                    'e8f20389-67d2-4a95-a593-1560e003d359',
                    'f4d24552-4c6d-4a5d-b42a-8073d9da00ce',
                    '7138e4b3-c422-493f-8ec0-5258f6f2e4a6',
                    '7e4f3bc1-9df4-4874-a6be-dca5b26f8357',
                    'd8136f90-527f-46c9-b5ee-4b071dd95d45',
                    'd7745f34-ec5d-482f-a899-2bf0cb751fdc',
                    '4a90c53d-89db-42dd-b4bc-8846a54ee96f',
                    '4f74715f-ebd8-408d-b5d6-ef2690f4d656',
                    'f6fdddb5-f4d5-496f-8af0-60d188f95af5',
                    '1317a5f9-bbfd-4755-96ec-df72b7d8a5fd',
                    '2596ab42-d7c7-4736-a6c6-0fd2f3018ef9'
                );

                DELETE FROM [TODBase].[Roles] WHERE [Id] IN (
                    '8d0f91f6-3218-4b89-8ec2-188ca4df7540',
                    '3452ebf0-e31f-4549-a55d-f2f68a8bd025',
                    '7f2975ab-09ad-4626-9ddb-950e242f7bda',
                    'f85ddb6d-685d-4c4e-91f4-1616c8eb6a14',
                    '3ff66c4e-a486-48db-97f8-35521d2f7755',
                    '6b5c3b2b-77cf-4c79-a855-b4ea8e20f791',
                    'ffb4903f-7b3f-41c5-b0ac-b7f4ea4a2393',
                    'f7b2732d-26a5-4fd8-a374-6c40d0a4f86f',
                    '95e5b66c-df4b-49cf-9963-cfd9022b7172'
                );
                """);

            migrationBuilder.DropTable(
                name: "IndirimKuraliKonaklamaTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "IndirimKuraliMisafirTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OdaFiyatlari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "IndirimKurallari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KonaklamaTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MisafirTipleri",
                schema: "dbo");
        }
    }
}
