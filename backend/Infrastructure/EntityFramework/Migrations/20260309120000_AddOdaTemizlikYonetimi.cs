using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260309120000_AddOdaTemizlikYonetimi")]
    public partial class AddOdaTemizlikYonetimi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TemizlikDurumu",
                schema: "dbo",
                table: "Odalar",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Hazir");

            migrationBuilder.Sql("""
                UPDATE [dbo].[Odalar]
                SET [TemizlikDurumu] = N'Hazir'
                WHERE [TemizlikDurumu] IS NULL OR LTRIM(RTRIM([TemizlikDurumu])) = N'';
                """);

            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
                DECLARE @TemizlikGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222205';

                DECLARE @MenuRoleId uniqueidentifier = '1d6f24ac-6c88-4aab-a4cf-e053fc4cbde9';
                DECLARE @ViewRoleId uniqueidentifier = '50528965-43b0-4662-aad6-a9d0b3315107';
                DECLARE @ManageRoleId uniqueidentifier = '0db2ca85-3c95-48f4-b8f6-553ec0d9a2fc';
                DECLARE @MenuItemId uniqueidentifier = '95c1f9f6-604f-4d82-8962-a8d682d99eef';
                DECLARE @UiUserRoleId uniqueidentifier;

                SELECT TOP 1 @UiUserRoleId = [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'KullaniciTipi' AND [Name] = N'UIUser';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @MenuRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuRoleId, N'Menu', N'OdaTemizlikYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ViewRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ViewRoleId, N'View', N'OdaTemizlikYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ManageRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ManageRoleId, N'Manage', N'OdaTemizlikYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TemizlikGroupId)
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@TemizlikGroupId, N'Destek Hizmetleri-Temizlik', 0, @Now, @Now);

                IF @UiUserRoleId IS NOT NULL
                   AND EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TemizlikGroupId)
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TemizlikGroupId AND [RoleId] = @UiUserRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES ('55ae0194-1097-43f8-bc67-449e9ee59649', @TemizlikGroupId, @UiUserRoleId, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('b58f3e48-40d4-4c2f-a4be-cbf4f3d8f1cb', @AdminGroupId, @MenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('93d30f66-6bc2-4ff3-9e8c-3367d85995ad', @AdminGroupId, @ViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('5da28ae7-6f1e-44dd-9bc0-fc8af84d4bc2', @AdminGroupId, @ManageRoleId, 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('355cf0a2-e0c9-4be8-9931-54003ad54ca6', @TesisManagerGroupId, @MenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('f6577ddd-c2f8-4249-b0e2-b57f8076f73a', @TesisManagerGroupId, @ViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('d8f6df3f-c76d-45f6-9dcf-0dc017e8cf0f', @TesisManagerGroupId, @ManageRoleId, 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TemizlikGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TemizlikGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('40fcd5ec-e4fb-4c12-9629-25f96db6de5d', @TemizlikGroupId, @MenuRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TemizlikGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('0f6d4bb4-8829-4b8c-9d7f-9de3328ddf86', @TemizlikGroupId, @ViewRoleId, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TemizlikGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('88b215ac-ef13-4ea8-8232-85bcf72ca6f7', @TemizlikGroupId, @ManageRoleId, 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MenuItemId)
                        INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (@MenuItemId, N'Oda Temizlik', N'fa-solid fa-broom', N'oda-temizlik-yonetimi', NULL, @MainMenuId, 25, 0, @Now, @Now);
                END

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES ('cbbf94bb-d779-4c86-a14f-713d3a3af537', @MenuItemId, @MenuRoleId, 0, @Now, @Now);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [TODBase].[MenuItemRoles]
                WHERE [Id] IN ('cbbf94bb-d779-4c86-a14f-713d3a3af537');

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Id] IN ('95c1f9f6-604f-4d82-8962-a8d682d99eef');

                DELETE FROM [TODBase].[UserGroupRoles]
                WHERE [Id] IN (
                    '55ae0194-1097-43f8-bc67-449e9ee59649',
                    'b58f3e48-40d4-4c2f-a4be-cbf4f3d8f1cb',
                    '93d30f66-6bc2-4ff3-9e8c-3367d85995ad',
                    '5da28ae7-6f1e-44dd-9bc0-fc8af84d4bc2',
                    '355cf0a2-e0c9-4be8-9931-54003ad54ca6',
                    'f6577ddd-c2f8-4249-b0e2-b57f8076f73a',
                    'd8f6df3f-c76d-45f6-9dcf-0dc017e8cf0f',
                    '40fcd5ec-e4fb-4c12-9629-25f96db6de5d',
                    '0f6d4bb4-8829-4b8c-9d7f-9de3328ddf86',
                    '88b215ac-ef13-4ea8-8232-85bcf72ca6f7'
                );

                DELETE FROM [TODBase].[UserGroups]
                WHERE [Id] = '22222222-2222-2222-2222-222222222205';

                DELETE FROM [TODBase].[Roles]
                WHERE [Id] IN (
                    '1d6f24ac-6c88-4aab-a4cf-e053fc4cbde9',
                    '50528965-43b0-4662-aad6-a9d0b3315107',
                    '0db2ca85-3c95-48f4-b8f6-553ec0d9a2fc'
                );
                """);

            migrationBuilder.DropColumn(
                name: "TemizlikDurumu",
                schema: "dbo",
                table: "Odalar");
        }
    }
}
