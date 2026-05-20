using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260520100000_AddMuhasebeDonemleriMenu")]
public partial class AddMuhasebeDonemleriMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @MuhasebeciGroupId uniqueidentifier;

            SELECT TOP (1) @MuhasebeciGroupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'MuhasebeciGrubu' AND [IsDeleted] = 0;

            DECLARE @MenuRoleId uniqueidentifier;
            DECLARE @ViewRoleId uniqueidentifier;
            DECLARE @ManageRoleId uniqueidentifier;
            DECLARE @ClosePeriodRoleId uniqueidentifier;

            -- MuhasebeDonemYonetimi rolleri (varsa al, yoksa olustur)
            SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeDonemYonetimi' AND [Name] = N'Menu';
            IF @MenuRoleId IS NULL
            BEGIN
                SET @MenuRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MenuRoleId, N'Menu', N'MuhasebeDonemYonetimi', 0, @Now, @Now);
            END;

            SELECT TOP (1) @ViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeDonemYonetimi' AND [Name] = N'View';
            IF @ViewRoleId IS NULL
            BEGIN
                SET @ViewRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@ViewRoleId, N'View', N'MuhasebeDonemYonetimi', 0, @Now, @Now);
            END;

            SELECT TOP (1) @ManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeDonemYonetimi' AND [Name] = N'Manage';
            IF @ManageRoleId IS NULL
            BEGIN
                SET @ManageRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@ManageRoleId, N'Manage', N'MuhasebeDonemYonetimi', 0, @Now, @Now);
            END;

            SELECT TOP (1) @ClosePeriodRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeDonemYonetimi' AND [Name] = N'ClosePeriod';
            IF @ClosePeriodRoleId IS NULL
            BEGIN
                SET @ClosePeriodRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@ClosePeriodRoleId, N'ClosePeriod', N'MuhasebeDonemYonetimi', 0, @Now, @Now);
            END;

            -- Rolleri kullanici gruplarina ata (idempotent)
            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @MenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @ViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @ManageRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ClosePeriodRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @ClosePeriodRoleId, 0, @Now, @Now);
            END;

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @MenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ViewRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @ViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ManageRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @ManageRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ClosePeriodRoleId)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @ClosePeriodRoleId, 0, @Now, @Now);
            END;

            IF @MuhasebeciGroupId IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @MuhasebeciGroupId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeciGroupId, @MenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @MuhasebeciGroupId AND [RoleId] = @ViewRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeciGroupId, @ViewRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @MuhasebeciGroupId AND [RoleId] = @ManageRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeciGroupId, @ManageRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @MuhasebeciGroupId AND [RoleId] = @ClosePeriodRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeciGroupId, @ClosePeriodRoleId, 0, @Now, @Now);
            END;

            -- Muhasebe root MenuItem (varsa al, yoksa olustur)
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

            -- Muhasebe Dönemleri MenuItem
            DECLARE @MenuItemId uniqueidentifier;
            SELECT TOP (1) @MenuItemId = [Id]
            FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/donemler' AND [IsDeleted] = 0;

            IF @MenuItemId IS NULL
            BEGIN
                SET @MenuItemId = NEWID();
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MenuItemId, N'Muhasebe Dönemleri', N'pi pi-calendar-clock', N'muhasebe/donemler', @MuhasebeRootId, 17, 0, @Now, @Now);
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Muhasebe Dönemleri',
                    [Icon] = N'pi pi-calendar-clock',
                    [ParentId] = @MuhasebeRootId,
                    [MenuOrder] = 17,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now
                WHERE [Id] = @MenuItemId;
            END;

            -- MenuItemRoles: Muhasebe root ve Muhasebe Dönemleri icin Menu rolu atamasi
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @MenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @MenuRoleId, 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId)
                INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MenuItemId, @MenuRoleId, 0, @Now, @Now);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            -- Sadece Muhasebe Dönemleri MenuItem ve MenuItemRoles kayitlarini temizle.
            -- MuhasebeDonemYonetimi rolleri diger muhasebe donem sayfalari tarafindan da
            -- kullanilabilecegi icin Roller ve UserGroupRoles tablolarina dokunulmaz.

            DELETE mir
            FROM [TODBase].[MenuItemRoles] mir
            INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
            WHERE mi.[Route] = N'muhasebe/donemler';

            DELETE FROM [TODBase].[MenuItems]
            WHERE [Route] = N'muhasebe/donemler';
            """);
    }
}
