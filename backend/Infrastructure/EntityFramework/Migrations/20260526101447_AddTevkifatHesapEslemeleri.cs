using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddTevkifatHesapEslemeleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TevkifatHesapEslemeleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: true),
                    IslemYonu = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TevkifatPay = table.Column<int>(type: "int", nullable: false),
                    TevkifatPayda = table.Column<int>(type: "int", nullable: false),
                    MuhasebeHesapPlaniId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_TevkifatHesapEslemeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TevkifatHesapEslemeleri_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId",
                        column: x => x.MuhasebeHesapPlaniId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeHesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TevkifatHesapEslemeleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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

                SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeTevkifatHesapEslemeYonetimi' AND [Name] = N'Menu';
                IF @MenuRoleId IS NULL
                BEGIN
                    SET @MenuRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuRoleId, N'Menu', N'MuhasebeTevkifatHesapEslemeYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @ViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeTevkifatHesapEslemeYonetimi' AND [Name] = N'View';
                IF @ViewRoleId IS NULL
                BEGIN
                    SET @ViewRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ViewRoleId, N'View', N'MuhasebeTevkifatHesapEslemeYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @ManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeTevkifatHesapEslemeYonetimi' AND [Name] = N'Manage';
                IF @ManageRoleId IS NULL
                BEGIN
                    SET @ManageRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ManageRoleId, N'Manage', N'MuhasebeTevkifatHesapEslemeYonetimi', 0, @Now, @Now);
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

                DECLARE @MenuItemId uniqueidentifier;
                SELECT TOP (1) @MenuItemId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/tevkifat-hesap-eslemeleri' AND [IsDeleted] = 0;

                IF @MenuItemId IS NULL
                BEGIN
                    SET @MenuItemId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuItemId, N'Tevkifat Hesap Eşlemeleri', N'pi pi-percentage', N'muhasebe/tevkifat-hesap-eslemeleri', @MuhasebeRootId, 19, 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Tevkifat Hesap Eşlemeleri',
                        [Icon] = N'pi pi-percentage',
                        [ParentId] = @MuhasebeRootId,
                        [MenuOrder] = 19,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @MenuItemId;
                END;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @MenuItemId, @MenuRoleId, 0, @Now, @Now);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TevkifatHesapEslemeleri_IslemYonu_TevkifatPay_TevkifatPayda",
                schema: "muhasebe",
                table: "TevkifatHesapEslemeleri",
                columns: new[] { "IslemYonu", "TevkifatPay", "TevkifatPayda" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1 AND [TesisId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TevkifatHesapEslemeleri_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "TevkifatHesapEslemeleri",
                column: "MuhasebeHesapPlaniId");

            migrationBuilder.CreateIndex(
                name: "IX_TevkifatHesapEslemeleri_TesisId",
                schema: "muhasebe",
                table: "TevkifatHesapEslemeleri",
                column: "TesisId");

            migrationBuilder.CreateIndex(
                name: "IX_TevkifatHesapEslemeleri_TesisId_IslemYonu_TevkifatPay_TevkifatPayda",
                schema: "muhasebe",
                table: "TevkifatHesapEslemeleri",
                columns: new[] { "TesisId", "IslemYonu", "TevkifatPay", "TevkifatPayda" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1 AND [TesisId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                WHERE mi.[Route] = N'muhasebe/tevkifat-hesap-eslemeleri';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/tevkifat-hesap-eslemeleri';
                """);

            migrationBuilder.DropTable(
                name: "TevkifatHesapEslemeleri",
                schema: "muhasebe");
        }
    }
}
