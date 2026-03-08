using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddOdaKullanimBlokYonetimi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OdaKullanimBloklari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    OdaId = table.Column<int>(type: "int", nullable: false),
                    BlokTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_OdaKullanimBloklari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OdaKullanimBloklari_Odalar_OdaId",
                        column: x => x.OdaId,
                        principalSchema: "dbo",
                        principalTable: "Odalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdaKullanimBloklari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OdaKullanimBloklari_OdaId",
                schema: "dbo",
                table: "OdaKullanimBloklari",
                column: "OdaId",
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OdaKullanimBloklari_TesisId_OdaId_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "OdaKullanimBloklari",
                columns: new[] { "TesisId", "OdaId", "BaslangicTarihi", "BitisTarihi" },
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';

                DECLARE @MenuRoleId uniqueidentifier = '2f3e25a7-0dc0-468f-b4f6-351421f25221';
                DECLARE @ViewRoleId uniqueidentifier = '88785a7a-f61a-4ec7-b337-d0e94e858d22';
                DECLARE @ManageRoleId uniqueidentifier = 'dff41b9e-f666-418b-a419-d3f9af2fe223';
                DECLARE @MenuItemId uniqueidentifier = '357a8f58-0ebe-4d8c-88eb-8f457f8c3124';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @MenuRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuRoleId, N'Menu', N'OdaKullanimBlokYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ViewRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ViewRoleId, N'View', N'OdaKullanimBlokYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ManageRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ManageRoleId, N'Manage', N'OdaKullanimBlokYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('7f9f2173-0ec8-44f8-8b6f-114397f95b31', @AdminGroupId, @MenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('b9739b37-84fd-484b-8f2d-6a172090f732', @AdminGroupId, @ViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('09a7f04f-bc18-437f-ad5a-5f03fd357633', @AdminGroupId, @ManageRoleId, 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('a9945436-3d27-4527-a5a5-2f08bf0e6d34', @TesisManagerGroupId, @MenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('f90ea178-cf0d-4668-9362-0409fd076335', @TesisManagerGroupId, @ViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('161442cb-2f75-4adb-a806-dd6fdb628236', @TesisManagerGroupId, @ManageRoleId, 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MenuItemId)
                        INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (@MenuItemId, N'Oda Bakim/Ariza', N'fa-solid fa-triangle-exclamation', N'oda-bakim-ariza', NULL, @MainMenuId, 24, 0, @Now, @Now);
                END

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES ('5d3df06d-36c8-4d37-b151-25e93dc62a37', @MenuItemId, @MenuRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [TODBase].[MenuItemRoles]
                WHERE [Id] IN ('5d3df06d-36c8-4d37-b151-25e93dc62a37');

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Id] IN ('357a8f58-0ebe-4d8c-88eb-8f457f8c3124');

                DELETE FROM [TODBase].[UserGroupRoles]
                WHERE [Id] IN (
                    '7f9f2173-0ec8-44f8-8b6f-114397f95b31',
                    'b9739b37-84fd-484b-8f2d-6a172090f732',
                    '09a7f04f-bc18-437f-ad5a-5f03fd357633',
                    'a9945436-3d27-4527-a5a5-2f08bf0e6d34',
                    'f90ea178-cf0d-4668-9362-0409fd076335',
                    '161442cb-2f75-4adb-a806-dd6fdb628236'
                );

                DELETE FROM [TODBase].[Roles]
                WHERE [Id] IN (
                    '2f3e25a7-0dc0-468f-b4f6-351421f25221',
                    '88785a7a-f61a-4ec7-b337-d0e94e858d22',
                    'dff41b9e-f666-418b-a419-d3f9af2fe223'
                );
                """);

            migrationBuilder.DropTable(
                name: "OdaKullanimBloklari",
                schema: "dbo");
        }
    }
}
