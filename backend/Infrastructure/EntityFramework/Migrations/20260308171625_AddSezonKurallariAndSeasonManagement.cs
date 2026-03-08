using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddSezonKurallariAndSeasonManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SezonKurallari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MinimumGece = table.Column<int>(type: "int", nullable: false),
                    StopSaleMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_SezonKurallari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SezonKurallari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SezonKurallari_TesisId_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "SezonKurallari",
                columns: new[] { "TesisId", "BaslangicTarihi", "BitisTarihi" },
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SezonKurallari_TesisId_Kod",
                schema: "dbo",
                table: "SezonKurallari",
                columns: new[] { "TesisId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';

                DECLARE @SezonMenuRoleId uniqueidentifier = '7d4d4f18-31f8-4a8f-97a4-f47ecbe3c901';
                DECLARE @SezonViewRoleId uniqueidentifier = '22f8ab40-9ef8-4abf-a4f9-d9b6fa8f9502';
                DECLARE @SezonManageRoleId uniqueidentifier = '18ae6f0d-1659-445f-86af-cfdbd3c8a903';
                DECLARE @SezonMenuItemId uniqueidentifier = '02d8c86c-4c43-41ca-9421-a6f67b57de04';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @SezonMenuRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@SezonMenuRoleId, N'Menu', N'SezonYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @SezonViewRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@SezonViewRoleId, N'View', N'SezonYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @SezonManageRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@SezonManageRoleId, N'Manage', N'SezonYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @SezonMenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('8a60d655-c74a-4b49-a0c8-b03ebbe40511', @AdminGroupId, @SezonMenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @SezonViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('2c0c00f6-9daa-4e95-b8f5-c7195f48cd12', @AdminGroupId, @SezonViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @SezonManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('4100d0dd-27d8-4b08-b3ff-1b1cbdf84713', @AdminGroupId, @SezonManageRoleId, 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @SezonMenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('f8a6d81f-5e13-4d7a-8439-c63f1f434f14', @TesisManagerGroupId, @SezonMenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @SezonViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('99a04471-28dc-4017-9f96-1eafe6dc2615', @TesisManagerGroupId, @SezonViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @SezonManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('41a8a5cc-6fdd-4a87-88eb-9e9ee0ca0f16', @TesisManagerGroupId, @SezonManageRoleId, 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @SezonMenuItemId)
                        INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (@SezonMenuItemId, N'Sezon Kurallari', N'fa-solid fa-calendar-days', N'sezon-kurallari', NULL, @MainMenuId, 23, 0, @Now, @Now);
                END

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @SezonMenuItemId AND [RoleId] = @SezonMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES ('8a118162-65b6-4340-a951-b74d22ce9c17', @SezonMenuItemId, @SezonMenuRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [TODBase].[MenuItemRoles]
                WHERE [Id] IN ('8a118162-65b6-4340-a951-b74d22ce9c17');

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Id] IN ('02d8c86c-4c43-41ca-9421-a6f67b57de04');

                DELETE FROM [TODBase].[UserGroupRoles]
                WHERE [Id] IN (
                    '8a60d655-c74a-4b49-a0c8-b03ebbe40511',
                    '2c0c00f6-9daa-4e95-b8f5-c7195f48cd12',
                    '4100d0dd-27d8-4b08-b3ff-1b1cbdf84713',
                    'f8a6d81f-5e13-4d7a-8439-c63f1f434f14',
                    '99a04471-28dc-4017-9f96-1eafe6dc2615',
                    '41a8a5cc-6fdd-4a87-88eb-9e9ee0ca0f16'
                );

                DELETE FROM [TODBase].[Roles]
                WHERE [Id] IN (
                    '7d4d4f18-31f8-4a8f-97a4-f47ecbe3c901',
                    '22f8ab40-9ef8-4abf-a4f9-d9b6fa8f9502',
                    '18ae6f0d-1659-445f-86af-cfdbd3c8a903'
                );
                """);

            migrationBuilder.DropTable(
                name: "SezonKurallari",
                schema: "dbo");
        }
    }
}
