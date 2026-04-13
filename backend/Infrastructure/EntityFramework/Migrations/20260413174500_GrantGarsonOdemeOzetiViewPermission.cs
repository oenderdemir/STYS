using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260413174500_GrantGarsonOdemeOzetiViewPermission")]
public partial class GrantGarsonOdemeOzetiViewPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @RestoranOdemeViewRoleId uniqueidentifier =
            (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'RestoranOdemeYonetimi'
                  AND [Name] = N'View'
                  AND [IsDeleted] = 0
            );

            IF @RestoranOdemeViewRoleId IS NULL
            BEGIN
                SET @RestoranOdemeViewRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@RestoranOdemeViewRoleId, N'View', N'RestoranOdemeYonetimi', 0, @Now, @Now);
            END;

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT NEWID(), ug.[Id], @RestoranOdemeViewRoleId, 0, @Now, @Now, N'migration_garson_odeme_ozeti', N'migration_garson_odeme_ozeti'
            FROM [TODBase].[UserGroups] ug
            WHERE ug.[Name] = N'GarsonGrubu'
              AND ug.[IsDeleted] = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [TODBase].[UserGroupRoles] ugr
                  WHERE ugr.[UserGroupId] = ug.[Id]
                    AND ugr.[RoleId] = @RestoranOdemeViewRoleId
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // no-op
    }
}

