using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260619194000_RemoveKampBasvuruPermissionsFromReceptionist")]
public partial class RemoveKampBasvuruPermissionsFromReceptionist : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @ResepsiyonistGroupId uniqueidentifier = (
                SELECT TOP (1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'ResepsiyonistGrubu' AND [IsDeleted] = 0
                ORDER BY [CreatedAt]
            );

            IF @ResepsiyonistGroupId IS NOT NULL
            BEGIN
                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE ugr.[UserGroupId] = @ResepsiyonistGroupId
                  AND ugr.[IsDeleted] = 0
                  AND (
                        (r.[Domain] = N'KampBasvuru' AND r.[Name] = N'Menu')
                     OR (r.[Domain] = N'KampBasvuruYonetimi' AND r.[Name] IN (N'Menu', N'View'))
                  );
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @ResepsiyonistGroupId uniqueidentifier = (
                SELECT TOP (1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'ResepsiyonistGrubu' AND [IsDeleted] = 0
                ORDER BY [CreatedAt]
            );

            IF @ResepsiyonistGroupId IS NOT NULL
            BEGIN
                DECLARE @Roles TABLE
                (
                    [Domain] nvarchar(128) NOT NULL,
                    [Name] nvarchar(64) NOT NULL
                );

                INSERT INTO @Roles ([Domain], [Name])
                VALUES
                (N'KampBasvuru', N'Menu'),
                (N'KampBasvuruYonetimi', N'Menu'),
                (N'KampBasvuruYonetimi', N'View');

                INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                SELECT NEWID(), @ResepsiyonistGroupId, r.[Id], 0, @Now, @Now
                FROM @Roles rr
                INNER JOIN [TODBase].[Roles] r
                    ON r.[Domain] = rr.[Domain]
                   AND r.[Name] = rr.[Name]
                WHERE r.[IsDeleted] = 0
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [TODBase].[UserGroupRoles] ugr
                      WHERE ugr.[UserGroupId] = @ResepsiyonistGroupId
                        AND ugr.[RoleId] = r.[Id]
                        AND ugr.[IsDeleted] = 0
                  );
            END;
            """);
    }
}
