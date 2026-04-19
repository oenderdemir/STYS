using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260419124500_AddMuhasebeHesapPlaniMenuAndPermissions")]
public partial class AddMuhasebeHesapPlaniMenuAndPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';

            DECLARE @MenuRoleId uniqueidentifier;
            DECLARE @ViewRoleId uniqueidentifier;
            DECLARE @ManageRoleId uniqueidentifier;

            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeHesapPlaniYonetimi' AND [Name] = N'Menu';
            IF @MenuRoleId IS NULL
            BEGIN
                SET @MenuRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MenuRoleId, N'Menu', N'MuhasebeHesapPlaniYonetimi', 0, @Now, @Now);
            END;

            SELECT TOP (1) @ViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeHesapPlaniYonetimi' AND [Name] = N'View';
            IF @ViewRoleId IS NULL
            BEGIN
                SET @ViewRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@ViewRoleId, N'View', N'MuhasebeHesapPlaniYonetimi', 0, @Now, @Now);
            END;

            SELECT TOP (1) @ManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeHesapPlaniYonetimi' AND [Name] = N'Manage';
            IF @ManageRoleId IS NULL
            BEGIN
                SET @ManageRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@ManageRoleId, N'Manage', N'MuhasebeHesapPlaniYonetimi', 0, @Now, @Now);
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

            DECLARE @MuhasebeRootId uniqueidentifier;
            DECLARE @MenuItemId uniqueidentifier;

            SELECT TOP (1) @MuhasebeRootId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

            IF @MuhasebeRootId IS NULL
            BEGIN
                SET @MuhasebeRootId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MuhasebeRootId, N'Muhasebe', N'pi pi-wallet', N'', NULL, 6, 0, @Now, @Now);
            END;

            SELECT TOP (1) @MenuItemId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/hesap-plani';

            IF @MenuItemId IS NULL
            BEGIN
                SET @MenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MenuItemId, N'Muhasebe Hesap Plani', N'pi pi-sitemap', N'muhasebe/hesap-plani', @MuhasebeRootId, 9, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Muhasebe Hesap Plani',
                    [Icon] = N'pi pi-sitemap',
                    [ParentId] = @MuhasebeRootId,
                    [MenuOrder] = 9,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now
                WHERE [Id] = @MenuItemId;
            END;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @MenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @MenuRoleId, 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MenuItemId, @MenuRoleId, 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @MenuRoleId uniqueidentifier;
            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeHesapPlaniYonetimi' AND [Name] = N'Menu';

            DELETE mir
            FROM [TODBase].[MenuItemRoles] mir
            INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
            WHERE mi.[Route] = N'muhasebe/hesap-plani'
               OR (mi.[Label] = N'Muhasebe' AND mi.[ParentId] IS NULL AND mir.[RoleId] = @MenuRoleId);

            DELETE FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/hesap-plani';

            DELETE FROM [TODBase].[Roles]
            WHERE [Domain] = N'MuhasebeHesapPlaniYonetimi';
            """);
    }
}
