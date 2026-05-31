using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKampBasvuruMenuPermissionSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @MenuItemId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d171';
                DECLARE @RoleId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'KampBasvuru' AND [Name] = N'Menu' AND [IsDeleted] = 0 ORDER BY [CreatedAt]);

                IF @RoleId IS NULL
                BEGIN
                    SET @RoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@RoleId, N'Menu', N'KampBasvuru', 0, @Now, @Now);
                END;

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MenuItemId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @RoleId AND [IsDeleted] = 0)
                    BEGIN
                        INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (NEWID(), @MenuItemId, @RoleId, 0, @Now, @Now);
                    END;
                END;

                INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT NEWID(),
                       ug.[Id],
                       @RoleId,
                       0,
                       @Now,
                       @Now,
                       N'migration_kamp_basvuru_menu_permission',
                       N'migration_kamp_basvuru_menu_permission'
                FROM [TODBase].[UserGroups] ug
                WHERE ug.[IsDeleted] = 0
                  AND ug.[Name] NOT IN (N'MuhasebeciGrubu', N'RestoranYoneticiGrubu', N'muhasebe-yoneticileri-grubu')
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [TODBase].[UserGroupRoles] ugr
                      WHERE ugr.[UserGroupId] = ug.[Id]
                        AND ugr.[RoleId] = @RoleId
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

                DECLARE @MenuItemId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d171';
                DECLARE @RoleId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'KampBasvuru' AND [Name] = N'Menu' AND [IsDeleted] = 0 ORDER BY [CreatedAt]);

                IF @RoleId IS NOT NULL
                BEGIN
                    DELETE FROM [TODBase].[MenuItemRoles]
                    WHERE [MenuItemId] = @MenuItemId
                      AND [RoleId] = @RoleId;

                    DELETE FROM [TODBase].[UserGroupRoles]
                    WHERE [RoleId] = @RoleId;

                    DELETE FROM [TODBase].[Roles]
                    WHERE [Id] = @RoleId;
                END;
                """);
        }
    }
}
