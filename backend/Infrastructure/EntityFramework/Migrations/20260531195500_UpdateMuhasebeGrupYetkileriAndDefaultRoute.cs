using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260531195500_UpdateMuhasebeGrupYetkileriAndDefaultRoute")]
public partial class UpdateMuhasebeGrupYetkileriAndDefaultRoute : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AdminGroupName nvarchar(128) = N'muhasebe-yoneticileri-grubu';
            DECLARE @MuhasebeciGroupName nvarchar(128) = N'MuhasebeciGrubu';
            DECLARE @AdminUserName nvarchar(128) = N'muhasebe-admin';
            DECLARE @DashboardRoute nvarchar(500) = N'/muhasebe/dashboard';

            DECLARE @AdminGroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = @AdminGroupName AND [IsDeleted] = 0 ORDER BY [CreatedAt]);
            DECLARE @MuhasebeciGroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = @MuhasebeciGroupName AND [IsDeleted] = 0 ORDER BY [CreatedAt]);
            DECLARE @AdminUserId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Users] WHERE [UserName] = @AdminUserName AND [IsDeleted] = 0 ORDER BY [CreatedAt]);

            DECLARE @AdminRoleId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeAdmin' AND [Name] = N'Admin' AND [IsDeleted] = 0 ORDER BY [CreatedAt]);
            IF @AdminRoleId IS NULL
            BEGIN
                SET @AdminRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@AdminRoleId, N'Admin', N'MuhasebeAdmin', 0, @Now, @Now);
            END;

            DECLARE @UiUserRoleId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'KullaniciTipi' AND [Name] = N'UIUser' AND [IsDeleted] = 0 ORDER BY [CreatedAt]);
            IF @UiUserRoleId IS NULL
            BEGIN
                SET @UiUserRoleId = NEWID();
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@UiUserRoleId, N'UIUser', N'KullaniciTipi', 0, @Now, @Now);
            END;

            IF @MuhasebeciGroupId IS NOT NULL
            BEGIN
                UPDATE [TODBase].[UserGroups]
                SET [DefaultRoute] = @DashboardRoute
                WHERE [Id] = @MuhasebeciGroupId
                  AND ([DefaultRoute] IS NULL OR [DefaultRoute] <> @DashboardRoute);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @MuhasebeciGroupId AND [RoleId] = @UiUserRoleId AND [IsDeleted] = 0)
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @MuhasebeciGroupId, @UiUserRoleId, 0, @Now, @Now);
                END;
            END;

            IF @AdminGroupId IS NOT NULL
            BEGIN
                DELETE FROM [TODBase].[UserGroupRoles]
                WHERE [UserGroupId] = @AdminGroupId
                  AND [IsDeleted] = 0
                  AND [RoleId] <> @AdminRoleId;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AdminRoleId AND [IsDeleted] = 0)
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @AdminGroupId, @AdminRoleId, 0, @Now, @Now);
                END;
            END;

            IF @AdminUserId IS NOT NULL
            BEGIN
                DECLARE @DesiredGroups TABLE ([UserGroupId] uniqueidentifier NOT NULL);

                IF @MuhasebeciGroupId IS NOT NULL
                    INSERT INTO @DesiredGroups ([UserGroupId]) VALUES (@MuhasebeciGroupId);

                IF @AdminGroupId IS NOT NULL
                    INSERT INTO @DesiredGroups ([UserGroupId]) VALUES (@AdminGroupId);

                DELETE uug
                FROM [TODBase].[UserUserGroups] uug
                WHERE uug.[UserId] = @AdminUserId
                  AND uug.[IsDeleted] = 0
                  AND NOT EXISTS (SELECT 1 FROM @DesiredGroups dg WHERE dg.[UserGroupId] = uug.[UserGroupId]);

                IF @MuhasebeciGroupId IS NOT NULL AND NOT EXISTS
                (
                    SELECT 1 FROM [TODBase].[UserUserGroups]
                    WHERE [UserId] = @AdminUserId AND [UserGroupId] = @MuhasebeciGroupId AND [IsDeleted] = 0
                )
                BEGIN
                    INSERT INTO [TODBase].[UserUserGroups] ([Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (NEWID(), @AdminUserId, @MuhasebeciGroupId, 0, @Now, @Now, N'migration_update_muhasebe_group_defaults', N'migration_update_muhasebe_group_defaults');
                END;

                IF @AdminGroupId IS NOT NULL AND NOT EXISTS
                (
                    SELECT 1 FROM [TODBase].[UserUserGroups]
                    WHERE [UserId] = @AdminUserId AND [UserGroupId] = @AdminGroupId AND [IsDeleted] = 0
                )
                BEGIN
                    INSERT INTO [TODBase].[UserUserGroups] ([Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (NEWID(), @AdminUserId, @AdminGroupId, 0, @Now, @Now, N'migration_update_muhasebe_group_defaults', N'migration_update_muhasebe_group_defaults');
                END;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @AdminGroupName nvarchar(128) = N'muhasebe-yoneticileri-grubu';
            DECLARE @MuhasebeciGroupName nvarchar(128) = N'MuhasebeciGrubu';
            DECLARE @AdminUserName nvarchar(128) = N'muhasebe-admin';

            DECLARE @AdminGroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = @AdminGroupName AND [IsDeleted] = 0 ORDER BY [CreatedAt]);
            DECLARE @MuhasebeciGroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = @MuhasebeciGroupName AND [IsDeleted] = 0 ORDER BY [CreatedAt]);
            DECLARE @AdminUserId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Users] WHERE [UserName] = @AdminUserName AND [IsDeleted] = 0 ORDER BY [CreatedAt]);

            IF @MuhasebeciGroupId IS NOT NULL
            BEGIN
                UPDATE [TODBase].[UserGroups]
                SET [DefaultRoute] = NULL
                WHERE [Id] = @MuhasebeciGroupId AND [DefaultRoute] = N'/muhasebe/dashboard';

                DELETE FROM [TODBase].[UserGroupRoles]
                WHERE [UserGroupId] = @MuhasebeciGroupId
                  AND [IsDeleted] = 0
                  AND [RoleId] IN
                  (
                      SELECT [Id]
                      FROM [TODBase].[Roles]
                      WHERE [Domain] = N'KullaniciTipi' AND [Name] = N'UIUser'
                  );
            END;

            IF @AdminUserId IS NOT NULL
            BEGIN
                IF @MuhasebeciGroupId IS NOT NULL
                    DELETE FROM [TODBase].[UserUserGroups] WHERE [UserId] = @AdminUserId AND [UserGroupId] = @MuhasebeciGroupId;

                -- MuhasebeAdmin grubundaki eski rol seti bu geri dönüşte tam olarak yeniden inşa edilmiyor.
                -- Gerekirse 20260524181128_SeedMuhasebeAdminRoleAndGroup içindeki seed SQL tekrar uygulanmalıdır.
            END;
            """);
    }
}
