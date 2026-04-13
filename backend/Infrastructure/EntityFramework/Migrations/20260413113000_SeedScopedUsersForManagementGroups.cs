using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260413113000_SeedScopedUsersForManagementGroups")]
public partial class SeedScopedUsersForManagementGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @SeedPasswordHash nvarchar(max) = N'PBKDF2$100000$7EIkx3zl3+g/hx5ORM0tUw==$JCeyiS0ajdez/R1BKi3K5awsF1bs+D8b2neo0E6KW+k=';

            DECLARE @TesisYoneticiGroupId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'TesisYoneticiGrubu'
            );

            DECLARE @BinaYoneticiGroupId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'BinaYoneticiGrubu'
            );

            DECLARE @ResepsiyonistGroupId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'ResepsiyonistGrubu'
            );

            DECLARE @RestoranYoneticiGroupId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'RestoranYoneticiGrubu'
            );

            DECLARE @GarsonGroupId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'GarsonGrubu'
            );

            DECLARE @AnyActiveTesisId int =
            (
                SELECT TOP(1) [Id]
                FROM [dbo].[Tesisler]
                WHERE [AktifMi] = 1
                ORDER BY [Id]
            );

            DECLARE @AnyActiveBinaId int =
            (
                SELECT TOP(1) b.[Id]
                FROM [dbo].[Binalar] b
                INNER JOIN [dbo].[Tesisler] t ON t.[Id] = b.[TesisId]
                WHERE b.[AktifMi] = 1
                  AND t.[AktifMi] = 1
                ORDER BY b.[Id]
            );

            DECLARE @AnyActiveBinaTesisId int =
            (
                SELECT TOP(1) b.[TesisId]
                FROM [dbo].[Binalar] b
                INNER JOIN [dbo].[Tesisler] t ON t.[Id] = b.[TesisId]
                WHERE b.[AktifMi] = 1
                  AND t.[AktifMi] = 1
                ORDER BY b.[Id]
            );

            DECLARE @AnyActiveRestoranId int =
            (
                SELECT TOP(1) [Id]
                FROM [restoran].[Restoranlar]
                WHERE [AktifMi] = 1
                ORDER BY [Id]
            );

            DECLARE @AnyActiveRestoranTesisId int =
            (
                SELECT TOP(1) [TesisId]
                FROM [restoran].[Restoranlar]
                WHERE [AktifMi] = 1
                ORDER BY [Id]
            );

            -- tesisyoneticisi.demo
            DECLARE @TesisYoneticisiUserId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[Users]
                WHERE [UserName] = N'tesisyoneticisi.demo'
            );

            IF @TesisYoneticisiUserId IS NULL
            BEGIN
                SET @TesisYoneticisiUserId = 'aaaaaaa1-1111-1111-1111-111111111111';
                IF EXISTS (SELECT 1 FROM [TODBase].[Users] WHERE [Id] = @TesisYoneticisiUserId)
                    SET @TesisYoneticisiUserId = NEWID();

                INSERT INTO [TODBase].[Users]
                (
                    [Id], [UserName], [NationalId], [FirstName], [LastName], [Email], [PasswordHash], [AvatarPath], [Status],
                    [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                )
                VALUES
                (
                    @TesisYoneticisiUserId, N'tesisyoneticisi.demo', NULL, N'Tesis', N'Yonetici Demo', N'tesisyoneticisi.demo@stys.local',
                    @SeedPasswordHash, NULL, 0, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users'
                );
            END;

            IF @TesisYoneticiGroupId IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserUserGroups] WHERE [UserId] = @TesisYoneticisiUserId AND [UserGroupId] = @TesisYoneticiGroupId)
            BEGIN
                INSERT INTO [TODBase].[UserUserGroups] ([Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (NEWID(), @TesisYoneticisiUserId, @TesisYoneticiGroupId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;

            IF @AnyActiveTesisId IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [dbo].[TesisYoneticileri] WHERE [UserId] = @TesisYoneticisiUserId AND [TesisId] = @AnyActiveTesisId AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [dbo].[TesisYoneticileri] ([TesisId], [UserId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@AnyActiveTesisId, @TesisYoneticisiUserId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;

            -- binayoneticisi.demo
            DECLARE @BinaYoneticisiUserId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[Users]
                WHERE [UserName] = N'binayoneticisi.demo'
            );

            IF @BinaYoneticisiUserId IS NULL
            BEGIN
                SET @BinaYoneticisiUserId = 'aaaaaaa2-2222-2222-2222-222222222222';
                IF EXISTS (SELECT 1 FROM [TODBase].[Users] WHERE [Id] = @BinaYoneticisiUserId)
                    SET @BinaYoneticisiUserId = NEWID();

                INSERT INTO [TODBase].[Users]
                (
                    [Id], [UserName], [NationalId], [FirstName], [LastName], [Email], [PasswordHash], [AvatarPath], [Status],
                    [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                )
                VALUES
                (
                    @BinaYoneticisiUserId, N'binayoneticisi.demo', NULL, N'Bina', N'Yonetici Demo', N'binayoneticisi.demo@stys.local',
                    @SeedPasswordHash, NULL, 0, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users'
                );
            END;

            IF @BinaYoneticiGroupId IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserUserGroups] WHERE [UserId] = @BinaYoneticisiUserId AND [UserGroupId] = @BinaYoneticiGroupId)
            BEGIN
                INSERT INTO [TODBase].[UserUserGroups] ([Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (NEWID(), @BinaYoneticisiUserId, @BinaYoneticiGroupId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;

            IF @AnyActiveBinaId IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [dbo].[BinaYoneticileri] WHERE [UserId] = @BinaYoneticisiUserId AND [BinaId] = @AnyActiveBinaId AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [dbo].[BinaYoneticileri] ([BinaId], [UserId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@AnyActiveBinaId, @BinaYoneticisiUserId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;

            -- resepsiyonist.demo
            DECLARE @ResepsiyonistUserId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[Users]
                WHERE [UserName] = N'resepsiyonist.demo'
            );

            IF @ResepsiyonistUserId IS NULL
            BEGIN
                SET @ResepsiyonistUserId = 'aaaaaaa3-3333-3333-3333-333333333333';
                IF EXISTS (SELECT 1 FROM [TODBase].[Users] WHERE [Id] = @ResepsiyonistUserId)
                    SET @ResepsiyonistUserId = NEWID();

                INSERT INTO [TODBase].[Users]
                (
                    [Id], [UserName], [NationalId], [FirstName], [LastName], [Email], [PasswordHash], [AvatarPath], [Status],
                    [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                )
                VALUES
                (
                    @ResepsiyonistUserId, N'resepsiyonist.demo', NULL, N'Resepsiyon', N'Demo', N'resepsiyonist.demo@stys.local',
                    @SeedPasswordHash, NULL, 0, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users'
                );
            END;

            IF @ResepsiyonistGroupId IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserUserGroups] WHERE [UserId] = @ResepsiyonistUserId AND [UserGroupId] = @ResepsiyonistGroupId)
            BEGIN
                INSERT INTO [TODBase].[UserUserGroups] ([Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (NEWID(), @ResepsiyonistUserId, @ResepsiyonistGroupId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;

            IF @AnyActiveTesisId IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [dbo].[TesisResepsiyonistleri] WHERE [UserId] = @ResepsiyonistUserId AND [TesisId] = @AnyActiveTesisId AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [dbo].[TesisResepsiyonistleri] ([TesisId], [UserId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@AnyActiveTesisId, @ResepsiyonistUserId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;

            -- restoranyoneticisi.demo
            DECLARE @RestoranYoneticiUserId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[Users]
                WHERE [UserName] = N'restoranyoneticisi.demo'
            );

            IF @RestoranYoneticiUserId IS NULL
            BEGIN
                SET @RestoranYoneticiUserId = 'aaaaaaa4-4444-4444-4444-444444444444';
                IF EXISTS (SELECT 1 FROM [TODBase].[Users] WHERE [Id] = @RestoranYoneticiUserId)
                    SET @RestoranYoneticiUserId = NEWID();

                INSERT INTO [TODBase].[Users]
                (
                    [Id], [UserName], [NationalId], [FirstName], [LastName], [Email], [PasswordHash], [AvatarPath], [Status],
                    [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                )
                VALUES
                (
                    @RestoranYoneticiUserId, N'restoranyoneticisi.demo', NULL, N'Restoran', N'Yonetici Demo', N'restoranyoneticisi.demo@stys.local',
                    @SeedPasswordHash, NULL, 0, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users'
                );
            END;

            IF @RestoranYoneticiGroupId IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserUserGroups] WHERE [UserId] = @RestoranYoneticiUserId AND [UserGroupId] = @RestoranYoneticiGroupId)
            BEGIN
                INSERT INTO [TODBase].[UserUserGroups] ([Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (NEWID(), @RestoranYoneticiUserId, @RestoranYoneticiGroupId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;

            IF @AnyActiveRestoranId IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [restoran].[RestoranYoneticileri] WHERE [UserId] = @RestoranYoneticiUserId AND [RestoranId] = @AnyActiveRestoranId AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [restoran].[RestoranYoneticileri] ([RestoranId], [UserId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@AnyActiveRestoranId, @RestoranYoneticiUserId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;

            -- garson.demo
            DECLARE @GarsonUserId uniqueidentifier =
            (
                SELECT TOP(1) [Id]
                FROM [TODBase].[Users]
                WHERE [UserName] = N'garson.demo'
            );

            IF @GarsonUserId IS NULL
            BEGIN
                SET @GarsonUserId = 'aaaaaaa5-5555-5555-5555-555555555555';
                IF EXISTS (SELECT 1 FROM [TODBase].[Users] WHERE [Id] = @GarsonUserId)
                    SET @GarsonUserId = NEWID();

                INSERT INTO [TODBase].[Users]
                (
                    [Id], [UserName], [NationalId], [FirstName], [LastName], [Email], [PasswordHash], [AvatarPath], [Status],
                    [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                )
                VALUES
                (
                    @GarsonUserId, N'garson.demo', NULL, N'Garson', N'Demo', N'garson.demo@stys.local',
                    @SeedPasswordHash, NULL, 0, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users'
                );
            END;

            IF @GarsonGroupId IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserUserGroups] WHERE [UserId] = @GarsonUserId AND [UserGroupId] = @GarsonGroupId)
            BEGIN
                INSERT INTO [TODBase].[UserUserGroups] ([Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (NEWID(), @GarsonUserId, @GarsonGroupId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;

            IF @AnyActiveRestoranId IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [restoran].[RestoranGarsonlari] WHERE [UserId] = @GarsonUserId AND [RestoranId] = @AnyActiveRestoranId AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [restoran].[RestoranGarsonlari] ([RestoranId], [UserId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@AnyActiveRestoranId, @GarsonUserId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;

            -- Owner/Tesis baglari
            IF @AnyActiveTesisId IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [dbo].[KullaniciTesisSahiplikleri] WHERE [UserId] = @TesisYoneticisiUserId)
                    INSERT INTO [dbo].[KullaniciTesisSahiplikleri] ([UserId], [TesisId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (@TesisYoneticisiUserId, @AnyActiveTesisId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');

                IF NOT EXISTS (SELECT 1 FROM [dbo].[KullaniciTesisSahiplikleri] WHERE [UserId] = @ResepsiyonistUserId)
                    INSERT INTO [dbo].[KullaniciTesisSahiplikleri] ([UserId], [TesisId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (@ResepsiyonistUserId, @AnyActiveTesisId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;

            IF @AnyActiveBinaTesisId IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [dbo].[KullaniciTesisSahiplikleri] WHERE [UserId] = @BinaYoneticisiUserId)
            BEGIN
                INSERT INTO [dbo].[KullaniciTesisSahiplikleri] ([UserId], [TesisId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (@BinaYoneticisiUserId, @AnyActiveBinaTesisId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;

            IF @AnyActiveRestoranTesisId IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [dbo].[KullaniciTesisSahiplikleri] WHERE [UserId] = @RestoranYoneticiUserId)
                    INSERT INTO [dbo].[KullaniciTesisSahiplikleri] ([UserId], [TesisId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (@RestoranYoneticiUserId, @AnyActiveRestoranTesisId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');

                IF NOT EXISTS (SELECT 1 FROM [dbo].[KullaniciTesisSahiplikleri] WHERE [UserId] = @GarsonUserId)
                    INSERT INTO [dbo].[KullaniciTesisSahiplikleri] ([UserId], [TesisId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (@GarsonUserId, @AnyActiveRestoranTesisId, 0, @Now, @Now, N'migration_seed_group_users', N'migration_seed_group_users');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @UserIds TABLE ([UserId] uniqueidentifier NOT NULL);
            INSERT INTO @UserIds ([UserId])
            SELECT [Id]
            FROM [TODBase].[Users]
            WHERE [UserName] IN
            (
                N'tesisyoneticisi.demo',
                N'binayoneticisi.demo',
                N'resepsiyonist.demo',
                N'restoranyoneticisi.demo',
                N'garson.demo'
            );

            DELETE ry
            FROM [restoran].[RestoranYoneticileri] ry
            INNER JOIN @UserIds u ON u.[UserId] = ry.[UserId];

            DELETE rg
            FROM [restoran].[RestoranGarsonlari] rg
            INNER JOIN @UserIds u ON u.[UserId] = rg.[UserId];

            DELETE byn
            FROM [dbo].[BinaYoneticileri] byn
            INNER JOIN @UserIds u ON u.[UserId] = byn.[UserId];

            DELETE tr
            FROM [dbo].[TesisResepsiyonistleri] tr
            INNER JOIN @UserIds u ON u.[UserId] = tr.[UserId];

            DELETE ty
            FROM [dbo].[TesisYoneticileri] ty
            INNER JOIN @UserIds u ON u.[UserId] = ty.[UserId];

            DELETE kts
            FROM [dbo].[KullaniciTesisSahiplikleri] kts
            INNER JOIN @UserIds u ON u.[UserId] = kts.[UserId];

            DELETE uug
            FROM [TODBase].[UserUserGroups] uug
            INNER JOIN @UserIds u ON u.[UserId] = uug.[UserId];

            DELETE usr
            FROM [TODBase].[Users] usr
            INNER JOIN @UserIds u ON u.[UserId] = usr.[Id];
            """);
    }
}
