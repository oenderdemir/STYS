using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260413124000_AddUiUserRoleToRestaurantGroups")]
public partial class AddUiUserRoleToRestaurantGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @UiUserRoleId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'KullaniciTipi'
                  AND [Name] = N'UIUser'
            );

            IF @UiUserRoleId IS NULL
            BEGIN
                SET @UiUserRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@UiUserRoleId, N'UIUser', N'KullaniciTipi', 0, @Now, @Now);
            END;

            DECLARE @TargetGroups TABLE ([GroupId] uniqueidentifier NOT NULL);
            INSERT INTO @TargetGroups ([GroupId])
            SELECT [Id]
            FROM [TODBase].[UserGroups]
            WHERE [Name] IN (N'RestoranYoneticiGrubu', N'GarsonGrubu');

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT NEWID(), g.[GroupId], @UiUserRoleId, 0, @Now, @Now, N'migration_uiuser_fix', N'migration_uiuser_fix'
            FROM @TargetGroups g
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[UserGroupRoles] ugr
                WHERE ugr.[UserGroupId] = g.[GroupId]
                  AND ugr.[RoleId] = @UiUserRoleId
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @UiUserRoleId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'KullaniciTipi'
                  AND [Name] = N'UIUser'
            );

            IF @UiUserRoleId IS NOT NULL
            BEGIN
                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[UserGroups] ug ON ug.[Id] = ugr.[UserGroupId]
                WHERE ugr.[RoleId] = @UiUserRoleId
                  AND ug.[Name] IN (N'RestoranYoneticiGrubu', N'GarsonGrubu');
            END;
            """);
    }
}
