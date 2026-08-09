using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    public partial class SeedPosYonetimiMenu : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';

DECLARE @ParentMenuId uniqueidentifier;
SELECT TOP 1 @ParentMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Sistem' AND [IsDeleted] = 0;
IF @ParentMenuId IS NULL
    SELECT TOP 1 @ParentMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Ana Menu' AND [IsDeleted] = 0;

-- Roles
DECLARE @MenuRole uniqueidentifier = 'D1D2D3D4-E5F6-7890-ABCD-EF5678901234';
DECLARE @ViewRole uniqueidentifier = 'E1E2E3E4-F5A6-7890-BCDE-F67890123456';
DECLARE @ManageRole uniqueidentifier = 'F1F2F3F4-A5B6-7890-CDEF-789012345678';

IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @MenuRole)
    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
    VALUES (@MenuRole, N'Menu', N'PosYonetimi', 0, @Now, @Now);
IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ViewRole)
    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
    VALUES (@ViewRole, N'View', N'PosYonetimi', 0, @Now, @Now);
IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ManageRole)
    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
    VALUES (@ManageRole, N'Manage', N'PosYonetimi', 0, @Now, @Now);

-- AdminGroup role assignments
IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRole)
        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
        VALUES ('A1A2A3A4-B5C6-D7E8-F9AB-CDEF01234567', @AdminGroupId, @MenuRole, 0, @Now, @Now);
    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRole)
        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
        VALUES ('B1B2B3B4-C5D6-E7F8-A9BC-DEF012345678', @AdminGroupId, @ViewRole, 0, @Now, @Now);
    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRole)
        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
        VALUES ('C1C2C3C4-D5E6-F7A8-B9CD-EF0123456789', @AdminGroupId, @ManageRole, 0, @Now, @Now);
END

-- MenuItem
DECLARE @MenuItem uniqueidentifier = '00000000-0000-0000-0000-100000000001';
IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'POS Yonetimi')
    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
    VALUES (@MenuItem, N'POS Yonetimi', N'fa-solid fa-credit-card', N'pos-yonetimi', NULL, @ParentMenuId, 98, 0, @Now, @Now);

-- MenuItemRole
IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItem AND [RoleId] = @MenuRole)
    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
    VALUES ('00000000-0000-0000-0000-200000000001', @MenuItem, @MenuRole, 0, @Now, @Now);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [TODBase].[MenuItemRoles] WHERE [Id] = '00000000-0000-0000-0000-200000000001';
DELETE FROM [TODBase].[MenuItems] WHERE [Id] = '00000000-0000-0000-0000-100000000001';
DELETE FROM [TODBase].[UserGroupRoles] WHERE [Id] IN ('C1C2C3C4-D5E6-F7A8-B9CD-EF0123456789', 'B1B2B3B4-C5D6-E7F8-A9BC-DEF012345678', 'A1A2A3A4-B5C6-D7E8-F9AB-CDEF01234567');
DELETE FROM [TODBase].[Roles] WHERE [Id] IN ('F1F2F3F4-A5B6-7890-CDEF-789012345678', 'E1E2E3E4-F5A6-7890-BCDE-F67890123456', 'D1D2D3D4-E5F6-7890-ABCD-EF5678901234');
");
        }
    }
}
