using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260616170000_SeedKurumYoneticisiGrubu")]
public partial class SeedKurumYoneticisiGrubu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AuditTag nvarchar(128) = N'migration_seed_kurum_yonetici_group';
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @KurumYoneticisiGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222205';

            IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @KurumYoneticisiGroupId)
            BEGIN
                UPDATE [TODBase].[UserGroups]
                SET [Name] = N'Kurum Yöneticisi Grubu',
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now,
                    [UpdatedBy] = @AuditTag
                WHERE [Id] = @KurumYoneticisiGroupId;
            END
            ELSE
            BEGIN
                INSERT INTO [TODBase].[UserGroups]
                (
                    [Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy]
                )
                VALUES
                (
                    @KurumYoneticisiGroupId, N'Kurum Yöneticisi Grubu', 0, @Now, @Now, NULL, @AuditTag, @AuditTag, NULL
                );
            END;

            DECLARE @SourceRoles TABLE
            (
                [RoleId] uniqueidentifier NOT NULL PRIMARY KEY
            );

            INSERT INTO @SourceRoles ([RoleId])
            SELECT DISTINCT ugr.[RoleId]
            FROM [TODBase].[UserGroupRoles] ugr
            INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
            WHERE ugr.[UserGroupId] = @AdminGroupId
              AND ugr.[IsDeleted] = 0
              AND NOT (
                    (r.[Domain] = N'System' AND r.[Name] = N'SuperAdmin')
                 OR (r.[Domain] = N'KullaniciTipi' AND r.[Name] = N'Admin')
                 OR (r.[Domain] = N'RoleManagement' AND r.[Name] = N'Manage')
                 OR (r.[Domain] = N'UserGroupManagement' AND r.[Name] = N'Manage')
                 OR (r.[Domain] = N'MenuManagement' AND r.[Name] = N'Manage')
                 OR (r.[Domain] = N'CountryManagement' AND r.[Name] = N'Manage')
              );

            UPDATE target
            SET [IsDeleted] = 0,
                [DeletedAt] = NULL,
                [UpdatedAt] = @Now,
                [UpdatedBy] = @AuditTag
            FROM [TODBase].[UserGroupRoles] target
            INNER JOIN @SourceRoles source ON source.[RoleId] = target.[RoleId]
            WHERE target.[UserGroupId] = @KurumYoneticisiGroupId;

            INSERT INTO [TODBase].[UserGroupRoles]
            (
                [Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy]
            )
            SELECT
                NEWID(),
                @KurumYoneticisiGroupId,
                source.[RoleId],
                0,
                @Now,
                @Now,
                NULL,
                @AuditTag,
                @AuditTag,
                NULL
            FROM @SourceRoles source
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[UserGroupRoles] target
                WHERE target.[UserGroupId] = @KurumYoneticisiGroupId
                  AND target.[RoleId] = source.[RoleId]
            );

            DECLARE @KurumAdminUsers TABLE
            (
                [Id] uniqueidentifier NOT NULL PRIMARY KEY
            );

            INSERT INTO @KurumAdminUsers ([Id])
            SELECT DISTINCT u.[Id]
            FROM [TODBase].[Users] u
            INNER JOIN [identity].[UserKurums] uk
                ON uk.[UserId] = u.[Id]
               AND uk.[IsDeleted] = 0
               AND uk.[AktifMi] = 1
               AND uk.[IsKurumAdmin] = 1
            WHERE u.[IsDeleted] = 0
              AND u.[UserName] <> N'admin';

            UPDATE uug
            SET [IsDeleted] = 1,
                [DeletedAt] = @Now,
                [DeletedBy] = @AuditTag,
                [UpdatedAt] = @Now,
                [UpdatedBy] = @AuditTag
            FROM [TODBase].[UserUserGroups] uug
            INNER JOIN @KurumAdminUsers kau ON kau.[Id] = uug.[UserId]
            WHERE uug.[UserGroupId] = @AdminGroupId
              AND uug.[IsDeleted] = 0;

            UPDATE uug
            SET [IsDeleted] = 0,
                [DeletedAt] = NULL,
                [DeletedBy] = NULL,
                [UpdatedAt] = @Now,
                [UpdatedBy] = @AuditTag
            FROM [TODBase].[UserUserGroups] uug
            INNER JOIN @KurumAdminUsers kau ON kau.[Id] = uug.[UserId]
            WHERE uug.[UserGroupId] = @KurumYoneticisiGroupId;

            INSERT INTO [TODBase].[UserUserGroups]
            (
                [Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy]
            )
            SELECT
                NEWID(),
                kau.[Id],
                @KurumYoneticisiGroupId,
                0,
                @Now,
                @Now,
                NULL,
                @AuditTag,
                @AuditTag,
                NULL
            FROM @KurumAdminUsers kau
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[UserUserGroups] uug
                WHERE uug.[UserId] = kau.[Id]
                  AND uug.[UserGroupId] = @KurumYoneticisiGroupId
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AuditTag nvarchar(128) = N'migration_seed_kurum_yonetici_group';
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @KurumYoneticisiGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222205';

            DECLARE @KurumAdminUsers TABLE
            (
                [Id] uniqueidentifier NOT NULL PRIMARY KEY
            );

            INSERT INTO @KurumAdminUsers ([Id])
            SELECT DISTINCT u.[Id]
            FROM [TODBase].[Users] u
            INNER JOIN [identity].[UserKurums] uk
                ON uk.[UserId] = u.[Id]
               AND uk.[IsDeleted] = 0
               AND uk.[AktifMi] = 1
               AND uk.[IsKurumAdmin] = 1
            WHERE u.[IsDeleted] = 0
              AND u.[UserName] <> N'admin';

            UPDATE uug
            SET [IsDeleted] = 1,
                [DeletedAt] = @Now,
                [DeletedBy] = @AuditTag,
                [UpdatedAt] = @Now,
                [UpdatedBy] = @AuditTag
            FROM [TODBase].[UserUserGroups] uug
            INNER JOIN @KurumAdminUsers kau ON kau.[Id] = uug.[UserId]
            WHERE uug.[UserGroupId] = @KurumYoneticisiGroupId
              AND uug.[IsDeleted] = 0;

            UPDATE uug
            SET [IsDeleted] = 0,
                [DeletedAt] = NULL,
                [DeletedBy] = NULL,
                [UpdatedAt] = @Now,
                [UpdatedBy] = @AuditTag
            FROM [TODBase].[UserUserGroups] uug
            INNER JOIN @KurumAdminUsers kau ON kau.[Id] = uug.[UserId]
            WHERE uug.[UserGroupId] = @AdminGroupId;

            DELETE FROM [TODBase].[UserGroupRoles]
            WHERE [UserGroupId] = @KurumYoneticisiGroupId;

            UPDATE [TODBase].[UserGroups]
            SET [IsDeleted] = 1,
                [DeletedAt] = @Now,
                [UpdatedAt] = @Now,
                [UpdatedBy] = @AuditTag
            WHERE [Id] = @KurumYoneticisiGroupId;
            """);
    }
}
