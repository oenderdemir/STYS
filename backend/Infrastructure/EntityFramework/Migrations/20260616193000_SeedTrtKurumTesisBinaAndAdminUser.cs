using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260616193000_SeedTrtKurumTesisBinaAndAdminUser")]
public partial class SeedTrtKurumTesisBinaAndAdminUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AuditTag nvarchar(128) = N'migration_seed_trt_kurum_admin';
            DECLARE @SeedPasswordHash nvarchar(max) = N'PBKDF2$100000$RFpRL3G82m+YFbhu6szuuQ==$QP5heD3o4rtZaopfF8Rll9kx0zqyz509E/CVrwbS3p4=';

            DECLARE @TrabzonIlId int = (
                SELECT TOP (1) [Id]
                FROM [dbo].[Iller]
                WHERE [Ad] = N'Trabzon'
                ORDER BY [Id]
            );

            IF (@TrabzonIlId IS NULL)
            BEGIN
                THROW 50000, N'Trabzon ili bulunamadi. Once merkezi il seed migration''i calismali.', 1;
            END;

            DECLARE @TrtKurumId int = 1000;
            DECLARE @TrtKurumExists bit = 0;

            SELECT TOP (1)
                @TrtKurumId = [Id],
                @TrtKurumExists = 1
            FROM [dbo].[Kurumlar]
            WHERE [Kod] = N'TRT'
            ORDER BY [Id];

            IF (@TrtKurumExists = 1)
            BEGIN
                UPDATE [dbo].[Kurumlar]
                SET [Kod] = N'TRT',
                    [Ad] = N'TRT',
                    [AktifMi] = 1,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now,
                    [UpdatedBy] = @AuditTag
                WHERE [Id] = @TrtKurumId;
            END
            ELSE
            BEGIN
                SET IDENTITY_INSERT [dbo].[Kurumlar] ON;

                INSERT INTO [dbo].[Kurumlar]
                    ([Id], [Kod], [Ad], [VergiNo], [Telefon], [Eposta], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                VALUES
                    (@TrtKurumId, N'TRT', N'TRT', NULL, N'+90 312 000 00 00', N'iletisim@trt.test', 1, 0, @Now, @Now, NULL, @AuditTag, @AuditTag, NULL);

                SET IDENTITY_INSERT [dbo].[Kurumlar] OFF;
            END;

            DECLARE @TrabzonMisafirhaneTesisId int = 1001;
            DECLARE @TrabzonMisafirhaneExists bit = 0;

            SELECT TOP (1)
                @TrabzonMisafirhaneTesisId = [Id],
                @TrabzonMisafirhaneExists = 1
            FROM [dbo].[Tesisler]
            WHERE [Ad] = N'Trabzon Misafirhane'
              AND [KurumId] = @TrtKurumId
            ORDER BY [Id];

            IF (@TrabzonMisafirhaneExists = 1)
            BEGIN
                UPDATE [dbo].[Tesisler]
                SET [Ad] = N'Trabzon Misafirhane',
                    [KurumId] = @TrtKurumId,
                    [IlId] = @TrabzonIlId,
                    [Telefon] = N'+90 462 000 00 00',
                    [Adres] = N'Trabzon Merkez',
                    [Eposta] = N'trabzon.misafirhane@trt.test',
                    [AktifMi] = 1,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now,
                    [UpdatedBy] = @AuditTag
                WHERE [Id] = @TrabzonMisafirhaneTesisId;
            END
            ELSE
            BEGIN
                SET IDENTITY_INSERT [dbo].[Tesisler] ON;

                INSERT INTO [dbo].[Tesisler]
                    ([Id], [Ad], [IlId], [Telefon], [Adres], [Eposta], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy], [KurumId])
                VALUES
                    (@TrabzonMisafirhaneTesisId, N'Trabzon Misafirhane', @TrabzonIlId, N'+90 462 000 00 00', N'Trabzon Merkez', N'trabzon.misafirhane@trt.test', 1, 0, @Now, @Now, NULL, @AuditTag, @AuditTag, NULL, @TrtKurumId);

                SET IDENTITY_INSERT [dbo].[Tesisler] OFF;
            END;

            DECLARE @TrabzonMisafirhaneBinaId int = 1002;
            DECLARE @TrabzonMisafirhaneBinaExists bit = 0;

            SELECT TOP (1)
                @TrabzonMisafirhaneBinaId = [Id],
                @TrabzonMisafirhaneBinaExists = 1
            FROM [dbo].[Binalar]
            WHERE [Ad] = N'Ana Bina'
              AND [TesisId] = @TrabzonMisafirhaneTesisId
            ORDER BY [Id];

            IF (@TrabzonMisafirhaneBinaExists = 1)
            BEGIN
                UPDATE [dbo].[Binalar]
                SET [Ad] = N'Ana Bina',
                    [TesisId] = @TrabzonMisafirhaneTesisId,
                    [KatSayisi] = 4,
                    [AktifMi] = 1,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now,
                    [UpdatedBy] = @AuditTag
                WHERE [Id] = @TrabzonMisafirhaneBinaId;
            END
            ELSE
            BEGIN
                SET IDENTITY_INSERT [dbo].[Binalar] ON;

                INSERT INTO [dbo].[Binalar]
                    ([Id], [Ad], [TesisId], [KatSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                VALUES
                    (@TrabzonMisafirhaneBinaId, N'Ana Bina', @TrabzonMisafirhaneTesisId, 4, 1, 0, @Now, @Now, NULL, @AuditTag, @AuditTag, NULL);

                SET IDENTITY_INSERT [dbo].[Binalar] OFF;
            END;

            DECLARE @KurumYoneticisiGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222205';
            DECLARE @KurumYoneticisiGroupExists bit = 0;

            SELECT TOP (1)
                @KurumYoneticisiGroupId = [Id],
                @KurumYoneticisiGroupExists = 1
            FROM [TODBase].[UserGroups]
            WHERE [Name] = N'Kurum Yöneticisi Grubu'
            ORDER BY [Id];

            IF (@KurumYoneticisiGroupExists = 1)
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
                    ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                VALUES
                    (@KurumYoneticisiGroupId, N'Kurum Yöneticisi Grubu', 0, @Now, @Now, NULL, @AuditTag, @AuditTag, NULL);
            END;

            DECLARE @TrtAdminUserId uniqueidentifier = 'f1f1f1f1-1111-1111-1111-111111111101';
            DECLARE @TrtAdminUserExists bit = 0;

            SELECT TOP (1)
                @TrtAdminUserId = [Id],
                @TrtAdminUserExists = 1
            FROM [TODBase].[Users]
            WHERE [UserName] = N'trt-admin'
            ORDER BY [Id];

            IF (@TrtAdminUserExists = 1)
            BEGIN
                UPDATE [TODBase].[Users]
                SET [UserName] = N'trt-admin',
                    [NationalId] = NULL,
                    [FirstName] = N'TRT',
                    [LastName] = N'Admin',
                    [Email] = N'trt-admin@trt.test',
                    [PasswordHash] = @SeedPasswordHash,
                    [AvatarPath] = NULL,
                    [Status] = 0,
                    [TokenVersion] = 0,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now,
                    [UpdatedBy] = @AuditTag
                WHERE [Id] = @TrtAdminUserId;
            END
            ELSE
            BEGIN
                INSERT INTO [TODBase].[Users]
                    ([Id], [UserName], [NationalId], [FirstName], [LastName], [Email], [PasswordHash], [AvatarPath], [Status], [TokenVersion], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                VALUES
                    (@TrtAdminUserId, N'trt-admin', NULL, N'TRT', N'Admin', N'trt-admin@trt.test', @SeedPasswordHash, NULL, 0, 0, 0, @Now, @Now, NULL, @AuditTag, @AuditTag, NULL);
            END;

            UPDATE [identity].[UserKurums]
            SET [VarsayilanMi] = 0,
                [UpdatedAt] = @Now,
                [UpdatedBy] = @AuditTag
            WHERE [UserId] = @TrtAdminUserId
              AND [KurumId] <> @TrtKurumId
              AND [IsDeleted] = 0
              AND [VarsayilanMi] = 1;

            UPDATE [identity].[UserKurums]
            SET [IsKurumAdmin] = 0,
                [UpdatedAt] = @Now,
                [UpdatedBy] = @AuditTag
            WHERE [UserId] = @TrtAdminUserId
              AND [KurumId] <> @TrtKurumId
              AND [IsDeleted] = 0
              AND [IsKurumAdmin] = 1;

            DECLARE @TrtAdminUserGroupMembershipId uniqueidentifier = 'f1f1f1f1-1111-1111-1111-111111111102';
            DECLARE @TrtAdminUserGroupMembershipExists bit = 0;

            SELECT TOP (1)
                @TrtAdminUserGroupMembershipId = [Id],
                @TrtAdminUserGroupMembershipExists = 1
            FROM [TODBase].[UserUserGroups]
            WHERE [UserId] = @TrtAdminUserId
              AND [UserGroupId] = @KurumYoneticisiGroupId
            ORDER BY [Id];

            IF (@TrtAdminUserGroupMembershipExists = 1)
            BEGIN
                UPDATE [TODBase].[UserUserGroups]
                SET [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now,
                    [UpdatedBy] = @AuditTag
                WHERE [Id] = @TrtAdminUserGroupMembershipId;
            END
            ELSE
            BEGIN
                INSERT INTO [TODBase].[UserUserGroups]
                    ([Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                VALUES
                    (@TrtAdminUserGroupMembershipId, @TrtAdminUserId, @KurumYoneticisiGroupId, 0, @Now, @Now, NULL, @AuditTag, @AuditTag, NULL);
            END;

            DECLARE @TrtAdminUserKurumId uniqueidentifier = 'f1f1f1f1-1111-1111-1111-111111111103';
            DECLARE @TrtAdminUserKurumExists bit = 0;

            SELECT TOP (1)
                @TrtAdminUserKurumId = [Id],
                @TrtAdminUserKurumExists = 1
            FROM [identity].[UserKurums]
            WHERE [UserId] = @TrtAdminUserId
              AND [KurumId] = @TrtKurumId
            ORDER BY [Id];

            IF (@TrtAdminUserKurumExists = 1)
            BEGIN
                UPDATE [identity].[UserKurums]
                SET [KurumId] = @TrtKurumId,
                    [VarsayilanMi] = 1,
                    [AktifMi] = 1,
                    [IsKurumAdmin] = 1,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [UpdatedAt] = @Now,
                    [UpdatedBy] = @AuditTag
                WHERE [Id] = @TrtAdminUserKurumId;
            END
            ELSE
            BEGIN
                INSERT INTO [identity].[UserKurums]
                    ([Id], [UserId], [KurumId], [VarsayilanMi], [AktifMi], [IsKurumAdmin], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                VALUES
                    (@TrtAdminUserKurumId, @TrtAdminUserId, @TrtKurumId, 1, 1, 1, 0, @Now, @Now, NULL, @AuditTag, @AuditTag, NULL);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @AuditTag nvarchar(128) = N'migration_seed_trt_kurum_admin';

            DECLARE @TrtAdminUserId uniqueidentifier = (
                SELECT TOP (1) [Id]
                FROM [TODBase].[Users]
                WHERE [UserName] = N'trt-admin'
                ORDER BY [Id]
            );

            DECLARE @TrtKurumId int = (
                SELECT TOP (1) [Id]
                FROM [dbo].[Kurumlar]
                WHERE [Kod] = N'TRT'
                ORDER BY [Id]
            );

            DECLARE @TrabzonMisafirhaneTesisId int = (
                SELECT TOP (1) [Id]
                FROM [dbo].[Tesisler]
                WHERE [Ad] = N'Trabzon Misafirhane'
                  AND [KurumId] = @TrtKurumId
                ORDER BY [Id]
            );

            DECLARE @TrabzonIlId int = (
                SELECT TOP (1) [Id]
                FROM [dbo].[Iller]
                WHERE [Ad] = N'Trabzon'
                ORDER BY [Id]
            );

            IF (@TrtAdminUserId IS NOT NULL)
            BEGIN
                DELETE FROM [identity].[UserKurums]
                WHERE [UserId] = @TrtAdminUserId
                  AND [KurumId] = @TrtKurumId
                  AND [CreatedBy] = @AuditTag;

                DELETE FROM [TODBase].[UserUserGroups]
                WHERE [UserId] = @TrtAdminUserId
                  AND [CreatedBy] = @AuditTag;

                DELETE FROM [TODBase].[Users]
                WHERE [Id] = @TrtAdminUserId
                  AND [CreatedBy] = @AuditTag;
            END;

            IF (@TrabzonMisafirhaneTesisId IS NOT NULL)
            BEGIN
                DELETE FROM [dbo].[Binalar]
                WHERE [TesisId] = @TrabzonMisafirhaneTesisId
                  AND [CreatedBy] = @AuditTag;

                DELETE FROM [dbo].[Tesisler]
                WHERE [Id] = @TrabzonMisafirhaneTesisId
                  AND [CreatedBy] = @AuditTag;
            END;

            IF (@TrtKurumId IS NOT NULL)
            BEGIN
                DELETE FROM [dbo].[Kurumlar]
                WHERE [Id] = @TrtKurumId
                  AND [CreatedBy] = @AuditTag;
            END;

            IF (@TrabzonIlId IS NOT NULL)
            BEGIN
                DELETE FROM [dbo].[Iller]
                WHERE [Id] = @TrabzonIlId
                  AND [CreatedBy] = @AuditTag
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [dbo].[Tesisler] t
                      WHERE t.[IlId] = @TrabzonIlId
                  );
            END;
            """);
    }
}
