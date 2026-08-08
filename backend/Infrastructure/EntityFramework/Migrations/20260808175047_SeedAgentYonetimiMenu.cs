using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    public partial class SeedAgentYonetimiMenu : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';

DECLARE @MenuRole uniqueidentifier = '1AA2B3C4-D5E6-7890-ABCD-EF1234567890';
DECLARE @ViewRole uniqueidentifier = '2BB3C4D5-E6F7-8901-BCDE-F12345678901';
DECLARE @ManageRole uniqueidentifier = '3CC4D5E6-F7A8-9012-CDEF-123456789012';

-- Roles
IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @MenuRole)
    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
    VALUES (@MenuRole, N'Menu', N'AgentYonetimi', 0, @Now, @Now);
IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ViewRole)
    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
    VALUES (@ViewRole, N'View', N'AgentYonetimi', 0, @Now, @Now);
IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @ManageRole)
    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
    VALUES (@ManageRole, N'Manage', N'AgentYonetimi', 0, @Now, @Now);

-- AdminGroup assignments
IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRole)
        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
        VALUES ('5EE6F7A8-B9C0-1234-EFAB-345678901234', @AdminGroupId, @MenuRole, 0, @Now, @Now);
    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRole)
        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
        VALUES ('6FF7A8B9-C0D1-2345-FABC-456789012345', @AdminGroupId, @ViewRole, 0, @Now, @Now);
    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRole)
        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
        VALUES ('7AA8B9C0-D1E2-3456-ABCD-567890123456', @AdminGroupId, @ManageRole, 0, @Now, @Now);
END

-- MenuItem
DECLARE @AgentMenuItem uniqueidentifier = '4DD5E6F7-A8B9-0123-DEFA-234567890123';
IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @AgentMenuItem)
        INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
        VALUES (@AgentMenuItem, N'Agent Yonetimi', N'fa-solid fa-robot', N'agent-yonetimi', NULL, @MainMenuId, 99, 0, @Now, @Now);
END

-- MenuItemRole
IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @AgentMenuItem AND [RoleId] = @MenuRole)
    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
    VALUES ('8BB9C0D1-E2F3-4567-BCDE-678901234567', @AgentMenuItem, @MenuRole, 0, @Now, @Now);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [TODBase].[MenuItemRoles] WHERE [Id] = '8BB9C0D1-E2F3-4567-BCDE-678901234567';
DELETE FROM [TODBase].[MenuItems] WHERE [Id] = '4DD5E6F7-A8B9-0123-DEFA-234567890123';
DELETE FROM [TODBase].[UserGroupRoles] WHERE [Id] IN ('7AA8B9C0-D1E2-3456-ABCD-567890123456', '6FF7A8B9-C0D1-2345-FABC-456789012345', '5EE6F7A8-B9C0-1234-EFAB-345678901234');
DELETE FROM [TODBase].[Roles] WHERE [Id] IN ('3CC4D5E6-F7A8-9012-CDEF-123456789012', '2BB3C4D5-E6F7-8901-BCDE-F12345678901', '1AA2B3C4-D5E6-7890-ABCD-EF1234567890');
");
        }
    }
}
