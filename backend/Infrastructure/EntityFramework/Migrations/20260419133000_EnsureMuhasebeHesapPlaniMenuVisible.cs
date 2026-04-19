using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260419133000_EnsureMuhasebeHesapPlaniMenuVisible")]
public partial class EnsureMuhasebeHesapPlaniMenuVisible : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @AdminGroupId uniqueidentifier;
            DECLARE @TesisManagerGroupId uniqueidentifier;
            SELECT TOP (1) @AdminGroupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] IN (N'YoneticiGrubu', N'Yönetici Grubu') AND [IsDeleted] = 0;
            SELECT TOP (1) @TesisManagerGroupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'TesisYoneticiGrubu' AND [IsDeleted] = 0;

            DECLARE @MenuRoleId uniqueidentifier;
            DECLARE @ViewRoleId uniqueidentifier;
            DECLARE @ManageRoleId uniqueidentifier;

            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeHesapPlaniYonetimi' AND [Name] = N'Menu' ORDER BY [IsDeleted], [CreatedAt];
            IF @MenuRoleId IS NULL
            BEGIN
                SET @MenuRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@MenuRoleId, N'Menu', N'MuhasebeHesapPlaniYonetimi', 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[Roles] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @MenuRoleId;
            END;

            SELECT TOP (1) @ViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeHesapPlaniYonetimi' AND [Name] = N'View' ORDER BY [IsDeleted], [CreatedAt];
            IF @ViewRoleId IS NULL
            BEGIN
                SET @ViewRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@ViewRoleId, N'View', N'MuhasebeHesapPlaniYonetimi', 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[Roles] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @ViewRoleId;
            END;

            SELECT TOP (1) @ManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeHesapPlaniYonetimi' AND [Name] = N'Manage' ORDER BY [IsDeleted], [CreatedAt];
            IF @ManageRoleId IS NULL
            BEGIN
                SET @ManageRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@ManageRoleId, N'Manage', N'MuhasebeHesapPlaniYonetimi', 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[Roles] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @ManageRoleId;
            END;

            IF @AdminGroupId IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @MenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @ViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @ManageRoleId, 0, @Now, @Now);
            END;

            IF @TesisManagerGroupId IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @MenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ViewRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @ViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ManageRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @ManageRoleId, 0, @Now, @Now);
            END;

            DECLARE @MuhasebeRootId uniqueidentifier;
            DECLARE @MenuItemId uniqueidentifier;

            SELECT TOP (1) @MuhasebeRootId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL
            ORDER BY [IsDeleted], [CreatedAt];

            IF @MuhasebeRootId IS NULL
            BEGIN
                SET @MuhasebeRootId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MuhasebeRootId, N'Muhasebe', N'pi pi-wallet', N'', NULL, 6, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @MuhasebeRootId;
            END;

            SELECT TOP (1) @MenuItemId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/hesap-plani'
            ORDER BY [IsDeleted], [CreatedAt];

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
                    [Route] = N'muhasebe/hesap-plani',
                    [ParentId] = @MuhasebeRootId,
                    [MenuOrder] = 9,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now
                WHERE [Id] = @MenuItemId;
            END;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @MenuRoleId, 0, @Now, @Now);
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MenuItemId, @MenuRoleId, 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM [TODBase].[MenuItemRoles]
            WHERE [MenuItemId] IN (SELECT [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/hesap-plani');

            DELETE FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/hesap-plani';
            """);
    }
}
