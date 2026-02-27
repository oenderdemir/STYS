using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class SeedYapiYetkileriBackend : Migration
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

                DECLARE @IlYonetimiView uniqueidentifier = '11111111-1111-1111-1111-111111111112';
                DECLARE @IlYonetimiManage uniqueidentifier = '11111111-1111-1111-1111-111111111113';
                DECLARE @TesisYonetimiView uniqueidentifier = '11111111-1111-1111-1111-111111111114';
                DECLARE @TesisYonetimiManage uniqueidentifier = '11111111-1111-1111-1111-111111111115';
                DECLARE @BinaYonetimiView uniqueidentifier = '11111111-1111-1111-1111-111111111116';
                DECLARE @BinaYonetimiManage uniqueidentifier = '11111111-1111-1111-1111-111111111117';
                DECLARE @IsletmeAlaniYonetimiView uniqueidentifier = '11111111-1111-1111-1111-111111111118';
                DECLARE @IsletmeAlaniYonetimiManage uniqueidentifier = '11111111-1111-1111-1111-111111111119';
                DECLARE @OdaTipiYonetimiView uniqueidentifier = '11111111-1111-1111-1111-111111111120';
                DECLARE @OdaTipiYonetimiManage uniqueidentifier = '11111111-1111-1111-1111-111111111121';
                DECLARE @OdaYonetimiView uniqueidentifier = '11111111-1111-1111-1111-111111111122';
                DECLARE @OdaYonetimiManage uniqueidentifier = '11111111-1111-1111-1111-111111111123';

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'IlYonetimi' AND [Name] = 'View')
                    SELECT @IlYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'IlYonetimi' AND [Name] = 'View';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@IlYonetimiView, 'View', 'IlYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'IlYonetimi' AND [Name] = 'Manage')
                    SELECT @IlYonetimiManage = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'IlYonetimi' AND [Name] = 'Manage';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@IlYonetimiManage, 'Manage', 'IlYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'TesisYonetimi' AND [Name] = 'View')
                    SELECT @TesisYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'TesisYonetimi' AND [Name] = 'View';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@TesisYonetimiView, 'View', 'TesisYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'TesisYonetimi' AND [Name] = 'Manage')
                    SELECT @TesisYonetimiManage = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'TesisYonetimi' AND [Name] = 'Manage';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@TesisYonetimiManage, 'Manage', 'TesisYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'BinaYonetimi' AND [Name] = 'View')
                    SELECT @BinaYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'BinaYonetimi' AND [Name] = 'View';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@BinaYonetimiView, 'View', 'BinaYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'BinaYonetimi' AND [Name] = 'Manage')
                    SELECT @BinaYonetimiManage = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'BinaYonetimi' AND [Name] = 'Manage';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@BinaYonetimiManage, 'Manage', 'BinaYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'IsletmeAlaniYonetimi' AND [Name] = 'View')
                    SELECT @IsletmeAlaniYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'IsletmeAlaniYonetimi' AND [Name] = 'View';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@IsletmeAlaniYonetimiView, 'View', 'IsletmeAlaniYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'IsletmeAlaniYonetimi' AND [Name] = 'Manage')
                    SELECT @IsletmeAlaniYonetimiManage = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'IsletmeAlaniYonetimi' AND [Name] = 'Manage';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@IsletmeAlaniYonetimiManage, 'Manage', 'IsletmeAlaniYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'OdaTipiYonetimi' AND [Name] = 'View')
                    SELECT @OdaTipiYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaTipiYonetimi' AND [Name] = 'View';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaTipiYonetimiView, 'View', 'OdaTipiYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'OdaTipiYonetimi' AND [Name] = 'Manage')
                    SELECT @OdaTipiYonetimiManage = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaTipiYonetimi' AND [Name] = 'Manage';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaTipiYonetimiManage, 'Manage', 'OdaTipiYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'View')
                    SELECT @OdaYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'View';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaYonetimiView, 'View', 'OdaYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Manage')
                    SELECT @OdaYonetimiManage = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Manage';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaYonetimiManage, 'Manage', 'OdaYonetimi', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @IlYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @IlYonetimiView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @IlYonetimiManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @IlYonetimiManage, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TesisYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @TesisYonetimiView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TesisYonetimiManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @TesisYonetimiManage, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @BinaYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @BinaYonetimiView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @BinaYonetimiManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @BinaYonetimiManage, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @IsletmeAlaniYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @IsletmeAlaniYonetimiView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @IsletmeAlaniYonetimiManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @IsletmeAlaniYonetimiManage, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @OdaTipiYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @OdaTipiYonetimiView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @OdaTipiYonetimiManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @OdaTipiYonetimiManage, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @OdaYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @OdaYonetimiView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @OdaYonetimiManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @OdaYonetimiManage, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE r.[Domain] IN (
                    'IlYonetimi',
                    'TesisYonetimi',
                    'BinaYonetimi',
                    'IsletmeAlaniYonetimi',
                    'OdaTipiYonetimi',
                    'OdaYonetimi')
                  AND r.[Name] IN ('View', 'Manage');

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] IN (
                    'IlYonetimi',
                    'TesisYonetimi',
                    'BinaYonetimi',
                    'IsletmeAlaniYonetimi',
                    'OdaTipiYonetimi',
                    'OdaYonetimi')
                  AND [Name] IN ('View', 'Manage');
                """);
        }
    }
}
