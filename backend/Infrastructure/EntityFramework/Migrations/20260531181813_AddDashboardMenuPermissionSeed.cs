using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardMenuPermissionSeed : Migration
    {
        /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @DashboardMenuId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2010';
            DECLARE @DashboardRoleId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'Dashboard' AND [Name] = N'Menu' AND [IsDeleted] = 0 ORDER BY [CreatedAt]);

            IF @DashboardRoleId IS NULL
            BEGIN
                SET @DashboardRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@DashboardRoleId, N'Menu', N'Dashboard', 0, @Now, @Now);
            END;

            IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @DashboardMenuId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @DashboardMenuId AND [RoleId] = @DashboardRoleId AND [IsDeleted] = 0)
                BEGIN
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @DashboardMenuId, @DashboardRoleId, 0, @Now, @Now);
                END;
            END;

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(),
                   ug.[Id],
                   @DashboardRoleId,
                   0,
                   @Now,
                   @Now
            FROM [TODBase].[UserGroups] ug
            WHERE ug.[IsDeleted] = 0
              AND ug.[Name] NOT IN (N'MuhasebeciGrubu', N'RestoranYoneticiGrubu', N'muhasebe-yoneticileri-grubu')
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [TODBase].[UserGroupRoles] ugr
                  WHERE ugr.[UserGroupId] = ug.[Id]
                    AND ugr.[RoleId] = @DashboardRoleId
                    AND ugr.[IsDeleted] = 0
              );
            """);
    }

        /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @DashboardMenuId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2010';
            DECLARE @DashboardRoleId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'Dashboard' AND [Name] = N'Menu' AND [IsDeleted] = 0 ORDER BY [CreatedAt]);

            IF @DashboardRoleId IS NOT NULL
            BEGIN
                DELETE FROM [TODBase].[MenuItemRoles]
                WHERE [MenuItemId] = @DashboardMenuId
                  AND [RoleId] = @DashboardRoleId;

                DELETE FROM [TODBase].[UserGroupRoles]
                WHERE [RoleId] = @DashboardRoleId;

                DELETE FROM [TODBase].[Roles]
                WHERE [Id] = @DashboardRoleId;
            END;
            """);
    }
}
}
