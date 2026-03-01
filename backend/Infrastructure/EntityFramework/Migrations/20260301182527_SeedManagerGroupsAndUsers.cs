using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class SeedManagerGroupsAndUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @UiUserRole uniqueidentifier = '11111111-1111-1111-1111-111111111110';
                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciTipi' AND [Name] = 'UIUser')
                    SELECT @UiUserRole = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciTipi' AND [Name] = 'UIUser';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@UiUserRole, 'UIUser', 'KullaniciTipi', 0, @Now, @Now);

                DECLARE @IlYonetimiView uniqueidentifier = (SELECT [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'IlYonetimi' AND [Name] = 'View');
                DECLARE @TesisYonetimiView uniqueidentifier = (SELECT [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'TesisYonetimi' AND [Name] = 'View');
                DECLARE @TesisYonetimiManage uniqueidentifier = (SELECT [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'TesisYonetimi' AND [Name] = 'Manage');
                DECLARE @BinaYonetimiView uniqueidentifier = (SELECT [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'BinaYonetimi' AND [Name] = 'View');
                DECLARE @BinaYonetimiManage uniqueidentifier = (SELECT [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'BinaYonetimi' AND [Name] = 'Manage');
                DECLARE @IsletmeAlaniYonetimiView uniqueidentifier = (SELECT [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'IsletmeAlaniYonetimi' AND [Name] = 'View');
                DECLARE @OdaTipiYonetimiView uniqueidentifier = (SELECT [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaTipiYonetimi' AND [Name] = 'View');
                DECLARE @OdaYonetimiView uniqueidentifier = (SELECT [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'View');
                DECLARE @OdaYonetimiManage uniqueidentifier = (SELECT [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Manage');
                DECLARE @OdaOzellikYonetimiView uniqueidentifier = (SELECT [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaOzellikYonetimi' AND [Name] = 'View');

                DECLARE @OdaYonetimiCreate uniqueidentifier = NEWID();
                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Create')
                    SELECT @OdaYonetimiCreate = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Create';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaYonetimiCreate, 'Create', 'OdaYonetimi', 0, @Now, @Now);

                DECLARE @OdaYonetimiDelete uniqueidentifier = NEWID();
                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Delete')
                    SELECT @OdaYonetimiDelete = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Delete';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaYonetimiDelete, 'Delete', 'OdaYonetimi', 0, @Now, @Now);

                DECLARE @TesisYoneticiGrupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Name] = 'TesisYoneticiGrubu')
                    SELECT @TesisYoneticiGrupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'TesisYoneticiGrubu';
                ELSE
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@TesisYoneticiGrupId, 'TesisYoneticiGrubu', 0, @Now, @Now);

                DECLARE @BinaYoneticiGrupId uniqueidentifier = '22222222-2222-2222-2222-222222222203';
                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Name] = 'BinaYoneticiGrubu')
                    SELECT @BinaYoneticiGrupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'BinaYoneticiGrubu';
                ELSE
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@BinaYoneticiGrupId, 'BinaYoneticiGrubu', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @UiUserRole)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisYoneticiGrupId, @UiUserRole, 0, @Now, @Now);
                IF @IlYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @IlYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisYoneticiGrupId, @IlYonetimiView, 0, @Now, @Now);
                IF @TesisYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @TesisYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisYoneticiGrupId, @TesisYonetimiView, 0, @Now, @Now);
                IF @TesisYonetimiManage IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @TesisYonetimiManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisYoneticiGrupId, @TesisYonetimiManage, 0, @Now, @Now);
                IF @BinaYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @BinaYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisYoneticiGrupId, @BinaYonetimiView, 0, @Now, @Now);
                IF @BinaYonetimiManage IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @BinaYonetimiManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisYoneticiGrupId, @BinaYonetimiManage, 0, @Now, @Now);
                IF @IsletmeAlaniYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @IsletmeAlaniYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisYoneticiGrupId, @IsletmeAlaniYonetimiView, 0, @Now, @Now);
                IF @OdaTipiYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @OdaTipiYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisYoneticiGrupId, @OdaTipiYonetimiView, 0, @Now, @Now);
                IF @OdaYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @OdaYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisYoneticiGrupId, @OdaYonetimiView, 0, @Now, @Now);
                IF @OdaOzellikYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @OdaOzellikYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisYoneticiGrupId, @OdaOzellikYonetimiView, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @BinaYoneticiGrupId AND [RoleId] = @UiUserRole)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @BinaYoneticiGrupId, @UiUserRole, 0, @Now, @Now);
                IF @BinaYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @BinaYoneticiGrupId AND [RoleId] = @BinaYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @BinaYoneticiGrupId, @BinaYonetimiView, 0, @Now, @Now);
                IF @OdaYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @BinaYoneticiGrupId AND [RoleId] = @OdaYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @BinaYoneticiGrupId, @OdaYonetimiView, 0, @Now, @Now);
                IF @OdaYonetimiManage IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @BinaYoneticiGrupId AND [RoleId] = @OdaYonetimiManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @BinaYoneticiGrupId, @OdaYonetimiManage, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @BinaYoneticiGrupId AND [RoleId] = @OdaYonetimiCreate)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @BinaYoneticiGrupId, @OdaYonetimiCreate, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @BinaYoneticiGrupId AND [RoleId] = @OdaYonetimiDelete)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @BinaYoneticiGrupId, @OdaYonetimiDelete, 0, @Now, @Now);
                IF @OdaTipiYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @BinaYoneticiGrupId AND [RoleId] = @OdaTipiYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @BinaYoneticiGrupId, @OdaTipiYonetimiView, 0, @Now, @Now);
                IF @OdaOzellikYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @BinaYoneticiGrupId AND [RoleId] = @OdaOzellikYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @BinaYoneticiGrupId, @OdaOzellikYonetimiView, 0, @Now, @Now);

                DECLARE @TesisYoneticisiId uniqueidentifier = '44444444-4444-4444-4444-444444444402';
                IF EXISTS (SELECT 1 FROM [TODBase].[Users] WHERE [UserName] = 'tesisYoneticisi')
                BEGIN
                    SELECT @TesisYoneticisiId = [Id] FROM [TODBase].[Users] WHERE [UserName] = 'tesisYoneticisi';
                    UPDATE [TODBase].[Users]
                    SET [PasswordHash] = 'PBKDF2$100000$c5Wm7dv20j1xBdw9yARCDg==$JtTQXywXOZAWHM92LFb3/4fwXVTUqjKtWscrEQhk4qw=',
                        [Status] = 0,
                        [IsDeleted] = 0,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @TesisYoneticisiId;
                END
                ELSE
                BEGIN
                    INSERT INTO [TODBase].[Users]
                        ([Id], [UserName], [FirstName], [LastName], [Email], [PasswordHash], [Status], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@TesisYoneticisiId, 'tesisYoneticisi', N'Tesis', N'Yoneticisi', 'tesis.yonetici@example.com', 'PBKDF2$100000$c5Wm7dv20j1xBdw9yARCDg==$JtTQXywXOZAWHM92LFb3/4fwXVTUqjKtWscrEQhk4qw=', 0, 0, @Now, @Now);
                END

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserUserGroups] WHERE [UserId] = @TesisYoneticisiId AND [UserGroupId] = @TesisYoneticiGrupId)
                    INSERT INTO [TODBase].[UserUserGroups] ([Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @TesisYoneticisiId, @TesisYoneticiGrupId, 0, @Now, @Now);

                DECLARE @BinaYoneticisiId uniqueidentifier = '44444444-4444-4444-4444-444444444403';
                IF EXISTS (SELECT 1 FROM [TODBase].[Users] WHERE [UserName] = 'binaYoneticisi')
                BEGIN
                    SELECT @BinaYoneticisiId = [Id] FROM [TODBase].[Users] WHERE [UserName] = 'binaYoneticisi';
                    UPDATE [TODBase].[Users]
                    SET [PasswordHash] = 'PBKDF2$100000$7aqhypZeirT4qgDg9yCGgA==$wuPhbWhnfV87Iw4hIE4sgcUDsmviEEhHRWg+NrnNHxY=',
                        [Status] = 0,
                        [IsDeleted] = 0,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @BinaYoneticisiId;
                END
                ELSE
                BEGIN
                    INSERT INTO [TODBase].[Users]
                        ([Id], [UserName], [FirstName], [LastName], [Email], [PasswordHash], [Status], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@BinaYoneticisiId, 'binaYoneticisi', N'Bina', N'Yoneticisi', 'bina.yonetici@example.com', 'PBKDF2$100000$7aqhypZeirT4qgDg9yCGgA==$wuPhbWhnfV87Iw4hIE4sgcUDsmviEEhHRWg+NrnNHxY=', 0, 0, @Now, @Now);
                END

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserUserGroups] WHERE [UserId] = @BinaYoneticisiId AND [UserGroupId] = @BinaYoneticiGrupId)
                    INSERT INTO [TODBase].[UserUserGroups] ([Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @BinaYoneticisiId, @BinaYoneticiGrupId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE uug
                FROM [TODBase].[UserUserGroups] uug
                INNER JOIN [TODBase].[Users] u ON u.[Id] = uug.[UserId]
                INNER JOIN [TODBase].[UserGroups] ug ON ug.[Id] = uug.[UserGroupId]
                WHERE u.[UserName] IN ('tesisYoneticisi', 'binaYoneticisi')
                  AND ug.[Name] IN ('TesisYoneticiGrubu', 'BinaYoneticiGrubu');

                DELETE FROM [TODBase].[Users]
                WHERE [UserName] IN ('tesisYoneticisi', 'binaYoneticisi');

                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[UserGroups] ug ON ug.[Id] = ugr.[UserGroupId]
                WHERE ug.[Name] IN ('TesisYoneticiGrubu', 'BinaYoneticiGrubu');

                DELETE FROM [TODBase].[UserGroups]
                WHERE [Name] IN ('TesisYoneticiGrubu', 'BinaYoneticiGrubu');

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] = 'OdaYonetimi'
                  AND [Name] IN ('Create', 'Delete')
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [RoleId] = [TODBase].[Roles].[Id])
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [RoleId] = [TODBase].[Roles].[Id]);
                """);
        }
    }
}
