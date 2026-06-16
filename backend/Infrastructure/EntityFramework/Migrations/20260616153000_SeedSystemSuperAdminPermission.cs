using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260616153000_SeedSystemSuperAdminPermission")]
public partial class SeedSystemSuperAdminPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AuditTag nvarchar(128) = N'migration_seed_system_super_admin';
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @SuperAdminRoleId uniqueidentifier = (
                SELECT TOP(1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'System'
                  AND [Name] = N'SuperAdmin'
                ORDER BY [CreatedAt], [Id]
            );

            IF @SuperAdminRoleId IS NULL
            BEGIN
                SET @SuperAdminRoleId = NEWID();

                INSERT INTO [TODBase].[Roles]
                (
                    [Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy]
                )
                VALUES
                (
                    @SuperAdminRoleId, N'SuperAdmin', N'System', 0, @Now, @Now, NULL, @AuditTag, @AuditTag, NULL
                );
            END
            ELSE
            BEGIN
                UPDATE [TODBase].[Roles]
                SET [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now,
                    [UpdatedBy] = @AuditTag
                WHERE [Id] = @SuperAdminRoleId;
            END;

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId AND [IsDeleted] = 0)
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM [TODBase].[UserGroupRoles]
                    WHERE [UserGroupId] = @AdminGroupId
                      AND [RoleId] = @SuperAdminRoleId
                )
                BEGIN
                    UPDATE [TODBase].[UserGroupRoles]
                    SET [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now,
                        [UpdatedBy] = @AuditTag
                    WHERE [UserGroupId] = @AdminGroupId
                      AND [RoleId] = @SuperAdminRoleId;
                END
                ELSE
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles]
                    (
                        [Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy]
                    )
                    VALUES
                    (
                        NEWID(), @AdminGroupId, @SuperAdminRoleId, 0, @Now, @Now, NULL, @AuditTag, @AuditTag, NULL
                    );
                END
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @SuperAdminRoleId uniqueidentifier = (
                SELECT TOP(1) [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'System'
                  AND [Name] = N'SuperAdmin'
                ORDER BY [CreatedAt], [Id]
            );

            IF @SuperAdminRoleId IS NOT NULL
            BEGIN
                DELETE FROM [TODBase].[UserGroupRoles]
                WHERE [UserGroupId] = @AdminGroupId
                  AND [RoleId] = @SuperAdminRoleId;

                IF NOT EXISTS (
                    SELECT 1
                    FROM [TODBase].[UserGroupRoles]
                    WHERE [RoleId] = @SuperAdminRoleId
                )
                BEGIN
                    DELETE FROM [TODBase].[Roles]
                    WHERE [Id] = @SuperAdminRoleId;
                END
            END;
            """);
    }
}
