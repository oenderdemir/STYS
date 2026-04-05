using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260403154500_AddKampPermissionsAndMenus")]
public partial class AddKampPermissionsAndMenus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @IsletmeRootId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2002';

            DECLARE @KampParentMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d101';
            DECLARE @KampProgramMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d111';
            DECLARE @KampDonemMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d121';
            DECLARE @KampAtamaMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d131';

            DECLARE @ProgramMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d211';
            DECLARE @ProgramViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d212';
            DECLARE @ProgramManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d213';

            DECLARE @DonemMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d221';
            DECLARE @DonemViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d222';
            DECLARE @DonemManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d223';

            DECLARE @AtamaMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d231';
            DECLARE @AtamaViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d232';
            DECLARE @AtamaManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d233';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampProgramiTanimYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@ProgramMenuRoleId, N'Menu', N'KampProgramiTanimYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampProgramiTanimYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@ProgramViewRoleId, N'View', N'KampProgramiTanimYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampProgramiTanimYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@ProgramManageRoleId, N'Manage', N'KampProgramiTanimYonetimi', 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampDonemiTanimYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@DonemMenuRoleId, N'Menu', N'KampDonemiTanimYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampDonemiTanimYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@DonemViewRoleId, N'View', N'KampDonemiTanimYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampDonemiTanimYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@DonemManageRoleId, N'Manage', N'KampDonemiTanimYonetimi', 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampDonemiTesisAtamaYonetimi' AND [Name] = N'Menu')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaMenuRoleId, N'Menu', N'KampDonemiTesisAtamaYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampDonemiTesisAtamaYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaViewRoleId, N'View', N'KampDonemiTesisAtamaYonetimi', 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'KampDonemiTesisAtamaYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AtamaManageRoleId, N'Manage', N'KampDonemiTesisAtamaYonetimi', 0, @Now, @Now);

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ProgramMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d311', @AdminGroupId, @ProgramMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ProgramViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d312', @AdminGroupId, @ProgramViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ProgramManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d313', @AdminGroupId, @ProgramManageRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @DonemMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d321', @AdminGroupId, @DonemMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @DonemViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d322', @AdminGroupId, @DonemViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @DonemManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d323', @AdminGroupId, @DonemManageRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AtamaMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d331', @AdminGroupId, @AtamaMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AtamaViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d332', @AdminGroupId, @AtamaViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AtamaManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d333', @AdminGroupId, @AtamaManageRoleId, 0, @Now, @Now);
            END

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @AtamaMenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d341', @TesisManagerGroupId, @AtamaMenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @AtamaViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d342', @TesisManagerGroupId, @AtamaViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @AtamaManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d343', @TesisManagerGroupId, @AtamaManageRoleId, 0, @Now, @Now);
            END

            IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @IsletmeRootId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampParentMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KampParentMenuId, N'Kamp Yönetimi', N'fa-solid fa-campground', N'', NULL, @IsletmeRootId, 9, 0, @Now, @Now);
                ELSE
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Kamp Yönetimi', [Icon] = N'fa-solid fa-campground', [Route] = N'', [ParentId] = @IsletmeRootId, [MenuOrder] = 9, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                    WHERE [Id] = @KampParentMenuId;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampProgramMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KampProgramMenuId, N'Programlar', N'fa-solid fa-layer-group', N'kamp-programlari', NULL, @KampParentMenuId, 0, 0, @Now, @Now);
                ELSE
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Programlar', [Icon] = N'fa-solid fa-layer-group', [Route] = N'kamp-programlari', [ParentId] = @KampParentMenuId, [MenuOrder] = 0, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                    WHERE [Id] = @KampProgramMenuId;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampDonemMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KampDonemMenuId, N'Dönemler', N'fa-solid fa-calendar-days', N'kamp-donemleri', NULL, @KampParentMenuId, 1, 0, @Now, @Now);
                ELSE
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Dönemler', [Icon] = N'fa-solid fa-calendar-days', [Route] = N'kamp-donemleri', [ParentId] = @KampParentMenuId, [MenuOrder] = 1, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                    WHERE [Id] = @KampDonemMenuId;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampAtamaMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KampAtamaMenuId, N'Tesis Atamaları', N'fa-solid fa-hotel', N'kamp-donemi-atamalari', NULL, @KampParentMenuId, 2, 0, @Now, @Now);
                ELSE
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Tesis Atamaları', [Icon] = N'fa-solid fa-hotel', [Route] = N'kamp-donemi-atamalari', [ParentId] = @KampParentMenuId, [MenuOrder] = 2, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                    WHERE [Id] = @KampAtamaMenuId;
            END

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KampProgramMenuId AND [RoleId] = @ProgramMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d411', @KampProgramMenuId, @ProgramMenuRoleId, 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KampDonemMenuId AND [RoleId] = @DonemMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d421', @KampDonemMenuId, @DonemMenuRoleId, 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KampAtamaMenuId AND [RoleId] = @AtamaMenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d431', @KampAtamaMenuId, @AtamaMenuRoleId, 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @KampParentMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d101';
            DECLARE @KampProgramMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d111';
            DECLARE @KampDonemMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d121';
            DECLARE @KampAtamaMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d131';

            DECLARE @ProgramMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d211';
            DECLARE @ProgramViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d212';
            DECLARE @ProgramManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d213';

            DECLARE @DonemMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d221';
            DECLARE @DonemViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d222';
            DECLARE @DonemManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d223';

            DECLARE @AtamaMenuRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d231';
            DECLARE @AtamaViewRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d232';
            DECLARE @AtamaManageRoleId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d233';

            DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] IN (@KampProgramMenuId, @KampDonemMenuId, @KampAtamaMenuId);
            DELETE FROM [TODBase].[MenuItems] WHERE [Id] IN (@KampProgramMenuId, @KampDonemMenuId, @KampAtamaMenuId, @KampParentMenuId);
            DELETE FROM [TODBase].[UserGroupRoles] WHERE [RoleId] IN (@ProgramMenuRoleId, @ProgramViewRoleId, @ProgramManageRoleId, @DonemMenuRoleId, @DonemViewRoleId, @DonemManageRoleId, @AtamaMenuRoleId, @AtamaViewRoleId, @AtamaManageRoleId);
            DELETE FROM [TODBase].[Roles] WHERE [Id] IN (@ProgramMenuRoleId, @ProgramViewRoleId, @ProgramManageRoleId, @DonemMenuRoleId, @DonemViewRoleId, @DonemManageRoleId, @AtamaMenuRoleId, @AtamaViewRoleId, @AtamaManageRoleId);
            """);
    }
}
