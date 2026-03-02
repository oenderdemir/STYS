using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260302183000_AddKullaniciAtamaPermissions")]
    public partial class AddKullaniciAtamaPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @KullaniciAtamaTesisYonetici uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'TesisYoneticisiAtanabilir');
                IF @KullaniciAtamaTesisYonetici IS NULL
                BEGIN
                    SET @KullaniciAtamaTesisYonetici = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KullaniciAtamaTesisYonetici, 'TesisYoneticisiAtanabilir', 'KullaniciAtama', 0, @Now, @Now);
                END

                DECLARE @KullaniciAtamaBinaYonetici uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'BinaYoneticisiAtanabilir');
                IF @KullaniciAtamaBinaYonetici IS NULL
                BEGIN
                    SET @KullaniciAtamaBinaYonetici = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KullaniciAtamaBinaYonetici, 'BinaYoneticisiAtanabilir', 'KullaniciAtama', 0, @Now, @Now);
                END

                DECLARE @KullaniciAtamaResepsiyonist uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'ResepsiyonistAtanabilir');
                IF @KullaniciAtamaResepsiyonist IS NULL
                BEGIN
                    SET @KullaniciAtamaResepsiyonist = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KullaniciAtamaResepsiyonist, 'ResepsiyonistAtanabilir', 'KullaniciAtama', 0, @Now, @Now);
                END

                DECLARE @GrupTipiTesisYonetici uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciGrupTipi' AND [Name] = 'TesisYoneticisi');
                IF @GrupTipiTesisYonetici IS NULL
                BEGIN
                    SET @GrupTipiTesisYonetici = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@GrupTipiTesisYonetici, 'TesisYoneticisi', 'KullaniciGrupTipi', 0, @Now, @Now);
                END

                DECLARE @GrupTipiBinaYonetici uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciGrupTipi' AND [Name] = 'BinaYoneticisi');
                IF @GrupTipiBinaYonetici IS NULL
                BEGIN
                    SET @GrupTipiBinaYonetici = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@GrupTipiBinaYonetici, 'BinaYoneticisi', 'KullaniciGrupTipi', 0, @Now, @Now);
                END

                DECLARE @GrupTipiResepsiyonist uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciGrupTipi' AND [Name] = 'Resepsiyonist');
                IF @GrupTipiResepsiyonist IS NULL
                BEGIN
                    SET @GrupTipiResepsiyonist = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@GrupTipiResepsiyonist, 'Resepsiyonist', 'KullaniciGrupTipi', 0, @Now, @Now);
                END

                DECLARE @AdminGroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'Yönetici Grubu');
                DECLARE @TesisYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'TesisYoneticiGrubu');
                DECLARE @BinaYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'BinaYoneticiGrubu');
                DECLARE @ResepsiyonistGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'ResepsiyonistGrubu');

                IF @TesisYoneticiGrupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @GrupTipiTesisYonetici)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @TesisYoneticiGrupId, @GrupTipiTesisYonetici, 0, @Now, @Now);

                IF @BinaYoneticiGrupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @BinaYoneticiGrupId AND [RoleId] = @GrupTipiBinaYonetici)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @BinaYoneticiGrupId, @GrupTipiBinaYonetici, 0, @Now, @Now);

                IF @ResepsiyonistGrupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGrupId AND [RoleId] = @GrupTipiResepsiyonist)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @ResepsiyonistGrupId, @GrupTipiResepsiyonist, 0, @Now, @Now);

                IF @AdminGroupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @KullaniciAtamaTesisYonetici)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @AdminGroupId, @KullaniciAtamaTesisYonetici, 0, @Now, @Now);

                IF @AdminGroupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @KullaniciAtamaBinaYonetici)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @AdminGroupId, @KullaniciAtamaBinaYonetici, 0, @Now, @Now);

                IF @AdminGroupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @KullaniciAtamaResepsiyonist)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @AdminGroupId, @KullaniciAtamaResepsiyonist, 0, @Now, @Now);

                IF @TesisYoneticiGrupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @KullaniciAtamaBinaYonetici)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @TesisYoneticiGrupId, @KullaniciAtamaBinaYonetici, 0, @Now, @Now);

                IF @TesisYoneticiGrupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @KullaniciAtamaResepsiyonist)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @TesisYoneticiGrupId, @KullaniciAtamaResepsiyonist, 0, @Now, @Now);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE r.[Domain] IN ('KullaniciAtama', 'KullaniciGrupTipi');

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] IN ('KullaniciAtama', 'KullaniciGrupTipi')
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [RoleId] = [TODBase].[Roles].[Id])
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [RoleId] = [TODBase].[Roles].[Id]);
                """);
        }
    }
}
