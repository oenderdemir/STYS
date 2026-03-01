using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class SeedOdaOzellikYetkileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Name] = N'Yönetici Grubu')
                    SELECT @AdminGroupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'Yönetici Grubu';
                ELSE
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@AdminGroupId, N'Yönetici Grubu', 0, @Now, @Now);

                DECLARE @OdaOzellikYonetimiView uniqueidentifier = '11111111-1111-1111-1111-111111111124';
                DECLARE @OdaOzellikYonetimiManage uniqueidentifier = '11111111-1111-1111-1111-111111111125';

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'OdaOzellikYonetimi' AND [Name] = 'View')
                    SELECT @OdaOzellikYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaOzellikYonetimi' AND [Name] = 'View';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaOzellikYonetimiView, 'View', 'OdaOzellikYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'OdaOzellikYonetimi' AND [Name] = 'Manage')
                    SELECT @OdaOzellikYonetimiManage = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaOzellikYonetimi' AND [Name] = 'Manage';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaOzellikYonetimiManage, 'Manage', 'OdaOzellikYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @OdaOzellikYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @AdminGroupId, @OdaOzellikYonetimiView, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @OdaOzellikYonetimiManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @AdminGroupId, @OdaOzellikYonetimiManage, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE r.[Domain] = 'OdaOzellikYonetimi'
                  AND r.[Name] IN ('View', 'Manage');

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] = 'OdaOzellikYonetimi'
                  AND [Name] IN ('View', 'Manage');
                """);
        }
    }
}
