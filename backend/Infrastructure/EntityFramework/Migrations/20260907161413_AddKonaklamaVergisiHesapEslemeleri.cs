using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKonaklamaVergisiHesapEslemeleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KonaklamaVergisiHesapEslemeleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: true),
                    VergiHesapId = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_KonaklamaVergisiHesapEslemeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KonaklamaVergisiHesapEslemeleri_MuhasebeHesapPlanlari_VergiHesapId",
                        column: x => x.VergiHesapId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeHesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KonaklamaVergisiHesapEslemeleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KonaklamaVergisiHesapEslemeleri_AktifMi",
                schema: "muhasebe",
                table: "KonaklamaVergisiHesapEslemeleri",
                column: "AktifMi",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1 AND [TesisId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KonaklamaVergisiHesapEslemeleri_TesisId",
                schema: "muhasebe",
                table: "KonaklamaVergisiHesapEslemeleri",
                column: "TesisId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1 AND [TesisId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KonaklamaVergisiHesapEslemeleri_VergiHesapId",
                schema: "muhasebe",
                table: "KonaklamaVergisiHesapEslemeleri",
                column: "VergiHesapId");

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
                DECLARE @MuhasebeciGroupId uniqueidentifier;

                SELECT TOP (1) @MuhasebeciGroupId = [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'MuhasebeciGrubu' AND [IsDeleted] = 0;

                DECLARE @MenuRoleId uniqueidentifier;
                DECLARE @ViewRoleId uniqueidentifier;
                DECLARE @ManageRoleId uniqueidentifier;

                SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeKonaklamaVergisiHesapEslemeYonetimi' AND [Name] = N'Menu';
                IF @MenuRoleId IS NULL
                BEGIN
                    SET @MenuRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuRoleId, N'Menu', N'MuhasebeKonaklamaVergisiHesapEslemeYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @ViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeKonaklamaVergisiHesapEslemeYonetimi' AND [Name] = N'View';
                IF @ViewRoleId IS NULL
                BEGIN
                    SET @ViewRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ViewRoleId, N'View', N'MuhasebeKonaklamaVergisiHesapEslemeYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @ManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeKonaklamaVergisiHesapEslemeYonetimi' AND [Name] = N'Manage';
                IF @ManageRoleId IS NULL
                BEGIN
                    SET @ManageRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ManageRoleId, N'Manage', N'MuhasebeKonaklamaVergisiHesapEslemeYonetimi', 0, @Now, @Now);
                END;

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @MenuRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @ViewRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @ManageRoleId, 0, @Now, @Now);
                END;

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @MenuRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @ViewRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @ManageRoleId, 0, @Now, @Now);
                END;

                IF @MuhasebeciGroupId IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @MuhasebeciGroupId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeciGroupId, @MenuRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @MuhasebeciGroupId AND [RoleId] = @ViewRoleId AND [IsDeleted] = 0)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeciGroupId, @ViewRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @MuhasebeciGroupId AND [RoleId] = @ManageRoleId AND [IsDeleted] = 0)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeciGroupId, @ManageRoleId, 0, @Now, @Now);
                END;

                DECLARE @MuhasebeRootId uniqueidentifier;
                SELECT TOP (1) @MuhasebeRootId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

                IF @MuhasebeRootId IS NULL
                BEGIN
                    SET @MuhasebeRootId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MuhasebeRootId, N'Muhasebe', N'pi pi-wallet', N'', NULL, 6, 0, @Now, @Now);
                END;

                DECLARE @MuhasebeYonetimiId uniqueidentifier;
                SELECT TOP (1) @MuhasebeYonetimiId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Muhasebe Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;

                IF @MuhasebeYonetimiId IS NULL
                    SET @MuhasebeYonetimiId = @MuhasebeRootId;

                DECLARE @MenuItemId uniqueidentifier;
                SELECT TOP (1) @MenuItemId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/konaklama-vergisi-hesap-eslemeleri' AND [IsDeleted] = 0;

                IF @MenuItemId IS NULL
                BEGIN
                    SET @MenuItemId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuItemId, N'Konaklama Vergisi Hesabı', N'pi pi-percentage', N'muhasebe/konaklama-vergisi-hesap-eslemeleri', @MuhasebeYonetimiId, 24, 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Konaklama Vergisi Hesabı',
                        [Icon] = N'pi pi-percentage',
                        [ParentId] = @MuhasebeYonetimiId,
                        [MenuOrder] = 24,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @MenuItemId;
                END;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @MenuItemId, @MenuRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                WHERE mi.[Route] = N'muhasebe/konaklama-vergisi-hesap-eslemeleri';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/konaklama-vergisi-hesap-eslemeleri';
                """);

            migrationBuilder.DropTable(
                name: "KonaklamaVergisiHesapEslemeleri",
                schema: "muhasebe");
        }
    }
}
