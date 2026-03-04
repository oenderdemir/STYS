using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260304103000_AddIndirimKuraliPermissions")]
    public partial class AddIndirimKuraliPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
                DECLARE @IndirimKuraliMenuRoleId uniqueidentifier = '5f72067f-c12e-41ea-b1d8-e88506af57bb';
                DECLARE @IndirimKuraliViewRoleId uniqueidentifier = '5ca584a2-16f4-4d89-a18f-16dc7b5f2d8f';
                DECLARE @IndirimKuraliManageRoleId uniqueidentifier = 'f50d9bf4-e7ae-4f31-9d17-9dd8d2f15296';
                DECLARE @IndirimKuraliSystemManageRoleId uniqueidentifier = 'db7f1f9e-4a9c-4d12-82ba-ed415de4e598';
                DECLARE @IndirimKuraliMenuItemId uniqueidentifier = 'f6a88f07-1536-4de2-8a89-9a5b84a2a4f0';
                DECLARE @OdaFiyatiMenuRoleId uniqueidentifier = '8d0f91f6-3218-4b89-8ec2-188ca4df7540';

                -- Roles
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @IndirimKuraliMenuRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@IndirimKuraliMenuRoleId, N'Menu', N'IndirimKuraliYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @IndirimKuraliViewRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@IndirimKuraliViewRoleId, N'View', N'IndirimKuraliYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @IndirimKuraliManageRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@IndirimKuraliManageRoleId, N'Manage', N'IndirimKuraliYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @IndirimKuraliSystemManageRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@IndirimKuraliSystemManageRoleId, N'SistemIndirimKuraliOlusturabilir', N'IndirimKuraliYonetimi', 0, @Now, @Now);

                -- Group role grants: Admin
                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @IndirimKuraliMenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('8f8bcd3d-5792-4f8e-aec0-b83e76a6304f', @AdminGroupId, @IndirimKuraliMenuRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @IndirimKuraliViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('836f93b9-0f56-49d2-8e8b-0eb8a53cb0b7', @AdminGroupId, @IndirimKuraliViewRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @IndirimKuraliManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('951911cb-8d27-4934-9f1e-c05c1044d4ca', @AdminGroupId, @IndirimKuraliManageRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @IndirimKuraliSystemManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('207bd674-f8cc-4be5-8f5f-d8eb613fe94d', @AdminGroupId, @IndirimKuraliSystemManageRoleId, 0, @Now, @Now);
                END

                -- Group role grants: Tesis yoneticisi
                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @IndirimKuraliMenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('76835d1f-a2f5-49a7-ba11-cf638d19172d', @TesisManagerGroupId, @IndirimKuraliMenuRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @IndirimKuraliViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('c96ef10f-1763-412f-ac57-4add29d7cd52', @TesisManagerGroupId, @IndirimKuraliViewRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @IndirimKuraliManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('bf37c0c9-c8ff-4ad3-99af-3f6f02459f37', @TesisManagerGroupId, @IndirimKuraliManageRoleId, 0, @Now, @Now);
                END

                -- Menu mapping: Indirim Kurallari menusu artik IndirimKuraliYonetimi.Menu ile calisir
                DELETE FROM [TODBase].[MenuItemRoles]
                WHERE [MenuItemId] = @IndirimKuraliMenuItemId
                  AND [RoleId] = @OdaFiyatiMenuRoleId;

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @IndirimKuraliMenuItemId)
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @IndirimKuraliMenuItemId AND [RoleId] = @IndirimKuraliMenuRoleId)
                        INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('dbaf8bc8-9496-4f65-a783-0e9f026ec40d', @IndirimKuraliMenuItemId, @IndirimKuraliMenuRoleId, 0, @Now, @Now);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @IndirimKuraliMenuRoleId uniqueidentifier = '5f72067f-c12e-41ea-b1d8-e88506af57bb';
                DECLARE @IndirimKuraliViewRoleId uniqueidentifier = '5ca584a2-16f4-4d89-a18f-16dc7b5f2d8f';
                DECLARE @IndirimKuraliManageRoleId uniqueidentifier = 'f50d9bf4-e7ae-4f31-9d17-9dd8d2f15296';
                DECLARE @IndirimKuraliSystemManageRoleId uniqueidentifier = 'db7f1f9e-4a9c-4d12-82ba-ed415de4e598';
                DECLARE @IndirimKuraliMenuItemId uniqueidentifier = 'f6a88f07-1536-4de2-8a89-9a5b84a2a4f0';
                DECLARE @OdaFiyatiMenuRoleId uniqueidentifier = '8d0f91f6-3218-4b89-8ec2-188ca4df7540';

                DELETE FROM [TODBase].[MenuItemRoles]
                WHERE [Id] = 'dbaf8bc8-9496-4f65-a783-0e9f026ec40d';

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @IndirimKuraliMenuItemId)
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @IndirimKuraliMenuItemId AND [RoleId] = @OdaFiyatiMenuRoleId)
                        INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('3d21435f-874f-4b8d-b0d5-9a57e8836edb', @IndirimKuraliMenuItemId, @OdaFiyatiMenuRoleId, 0, @Now, @Now);

                DELETE FROM [TODBase].[UserGroupRoles] WHERE [Id] IN (
                    '8f8bcd3d-5792-4f8e-aec0-b83e76a6304f',
                    '836f93b9-0f56-49d2-8e8b-0eb8a53cb0b7',
                    '951911cb-8d27-4934-9f1e-c05c1044d4ca',
                    '207bd674-f8cc-4be5-8f5f-d8eb613fe94d',
                    '76835d1f-a2f5-49a7-ba11-cf638d19172d',
                    'c96ef10f-1763-412f-ac57-4add29d7cd52',
                    'bf37c0c9-c8ff-4ad3-99af-3f6f02459f37'
                );

                DELETE FROM [TODBase].[Roles] WHERE [Id] IN (
                    '5f72067f-c12e-41ea-b1d8-e88506af57bb',
                    '5ca584a2-16f4-4d89-a18f-16dc7b5f2d8f',
                    'f50d9bf4-e7ae-4f31-9d17-9dd8d2f15296',
                    'db7f1f9e-4a9c-4d12-82ba-ed415de4e598'
                );
                """);
        }
    }
}
