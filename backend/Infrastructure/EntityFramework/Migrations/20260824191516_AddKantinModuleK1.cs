using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKantinModuleK1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "kantin");

            migrationBuilder.CreateTable(
                name: "Kantinler",
                schema: "kantin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    DepoId = table.Column<int>(type: "int", nullable: false),
                    VarsayilanNakitKasaId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_Kantinler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kantinler_Depolar_DepoId",
                        column: x => x.DepoId,
                        principalSchema: "muhasebe",
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Kantinler_KasaBankaHesaplari_VarsayilanNakitKasaId",
                        column: x => x.VarsayilanNakitKasaId,
                        principalSchema: "muhasebe",
                        principalTable: "KasaBankaHesaplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Kantinler_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KantinUrunleri",
                schema: "kantin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KantinId = table.Column<int>(type: "int", nullable: false),
                    TasinirKartId = table.Column<int>(type: "int", nullable: false),
                    SiraNo = table.Column<int>(type: "int", nullable: true),
                    Barkod = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SatisFiyati = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_KantinUrunleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KantinUrunleri_Kantinler_KantinId",
                        column: x => x.KantinId,
                        principalSchema: "kantin",
                        principalTable: "Kantinler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KantinUrunleri_TasinirKartlar_TasinirKartId",
                        column: x => x.TasinirKartId,
                        principalSchema: "muhasebe",
                        principalTable: "TasinirKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kantinler_DepoId",
                schema: "kantin",
                table: "Kantinler",
                column: "DepoId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Kantinler_TesisId_Kod",
                schema: "kantin",
                table: "Kantinler",
                columns: new[] { "TesisId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Kantinler_VarsayilanNakitKasaId",
                schema: "kantin",
                table: "Kantinler",
                column: "VarsayilanNakitKasaId",
                filter: "[IsDeleted] = 0 AND [VarsayilanNakitKasaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KantinUrunleri_KantinId_Barkod",
                schema: "kantin",
                table: "KantinUrunleri",
                columns: new[] { "KantinId", "Barkod" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Barkod] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KantinUrunleri_KantinId_TasinirKartId",
                schema: "kantin",
                table: "KantinUrunleri",
                columns: new[] { "KantinId", "TasinirKartId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KantinUrunleri_TasinirKartId",
                schema: "kantin",
                table: "KantinUrunleri",
                column: "TasinirKartId");

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @KantinMenuRoleId uniqueidentifier;
                DECLARE @KantinViewRoleId uniqueidentifier;
                DECLARE @KantinManageRoleId uniqueidentifier;
                DECLARE @KantinRootMenuId uniqueidentifier;
                DECLARE @KantinlerMenuId uniqueidentifier;

                SELECT TOP (1) @KantinMenuRoleId = [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'KantinYonetimi' AND [Name] = N'Menu';

                IF @KantinMenuRoleId IS NULL
                BEGIN
                    SET @KantinMenuRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KantinMenuRoleId, N'Menu', N'KantinYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @KantinViewRoleId = [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'KantinYonetimi' AND [Name] = N'View';

                IF @KantinViewRoleId IS NULL
                BEGIN
                    SET @KantinViewRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KantinViewRoleId, N'View', N'KantinYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @KantinManageRoleId = [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'KantinYonetimi' AND [Name] = N'Manage';

                IF @KantinManageRoleId IS NULL
                BEGIN
                    SET @KantinManageRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KantinManageRoleId, N'Manage', N'KantinYonetimi', 0, @Now, @Now);
                END;

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @KantinMenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (NEWID(), @AdminGroupId, @KantinMenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @KantinViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (NEWID(), @AdminGroupId, @KantinViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @KantinManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (NEWID(), @AdminGroupId, @KantinManageRoleId, 0, @Now, @Now);
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
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Kantin Yönetimi',
                        [Icon] = N'pi pi-shop',
                        [Route] = N'',
                        [ParentId] = NULL,
                        [MenuOrder] = 6,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @KantinRootMenuId;
                END;

                SELECT TOP (1) @KantinlerMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'kantinler';

                IF @KantinlerMenuId IS NULL
                BEGIN
                    SET @KantinlerMenuId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KantinlerMenuId, N'Kantinler', N'pi pi-list', N'kantinler', NULL, @KantinRootMenuId, 0, 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Kantinler',
                        [Icon] = N'pi pi-list',
                        [ParentId] = @KantinRootMenuId,
                        [MenuOrder] = 0,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @KantinlerMenuId;
                END;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KantinRootMenuId AND [RoleId] = @KantinMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @KantinRootMenuId, @KantinMenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KantinlerMenuId AND [RoleId] = @KantinMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @KantinlerMenuId, @KantinMenuRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KantinUrunleri",
                schema: "kantin");

            migrationBuilder.DropTable(
                name: "Kantinler",
                schema: "kantin");

            migrationBuilder.Sql(
                """
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                WHERE mi.[Route] = N'kantinler'
                   OR (mi.[Label] = N'Kantin Yönetimi' AND mi.[ParentId] IS NULL);

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] = N'kantinler';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Kantin Yönetimi'
                  AND [ParentId] IS NULL
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] child WHERE child.[ParentId] = [TODBase].[MenuItems].[Id] AND child.[IsDeleted] = 0);

                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE r.[Domain] = N'KantinYonetimi';

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] = N'KantinYonetimi';
                """);
        }
    }
}
