using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260302200000_AddKullaniciAtayabilirPermissions")]
    public partial class AddKullaniciAtayabilirPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @TesisYoneticisiAtanabilir uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'TesisYoneticisiAtanabilir');
                IF @TesisYoneticisiAtanabilir IS NULL
                BEGIN
                    SET @TesisYoneticisiAtanabilir = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@TesisYoneticisiAtanabilir, 'TesisYoneticisiAtanabilir', 'KullaniciAtama', 0, @Now, @Now);
                END

                DECLARE @BinaYoneticisiAtanabilir uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'BinaYoneticisiAtanabilir');
                IF @BinaYoneticisiAtanabilir IS NULL
                BEGIN
                    SET @BinaYoneticisiAtanabilir = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@BinaYoneticisiAtanabilir, 'BinaYoneticisiAtanabilir', 'KullaniciAtama', 0, @Now, @Now);
                END

                DECLARE @ResepsiyonistAtanabilir uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'ResepsiyonistAtanabilir');
                IF @ResepsiyonistAtanabilir IS NULL
                BEGIN
                    SET @ResepsiyonistAtanabilir = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ResepsiyonistAtanabilir, 'ResepsiyonistAtanabilir', 'KullaniciAtama', 0, @Now, @Now);
                END

                DECLARE @TesisYoneticisiAtayabilir uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'TesisYoneticisiAtayabilir');
                IF @TesisYoneticisiAtayabilir IS NULL
                BEGIN
                    SET @TesisYoneticisiAtayabilir = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@TesisYoneticisiAtayabilir, 'TesisYoneticisiAtayabilir', 'KullaniciAtama', 0, @Now, @Now);
                END

                DECLARE @BinaYoneticisiAtayabilir uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'BinaYoneticisiAtayabilir');
                IF @BinaYoneticisiAtayabilir IS NULL
                BEGIN
                    SET @BinaYoneticisiAtayabilir = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@BinaYoneticisiAtayabilir, 'BinaYoneticisiAtayabilir', 'KullaniciAtama', 0, @Now, @Now);
                END

                DECLARE @ResepsiyonistAtayabilir uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'ResepsiyonistAtayabilir');
                IF @ResepsiyonistAtayabilir IS NULL
                BEGIN
                    SET @ResepsiyonistAtayabilir = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ResepsiyonistAtayabilir, 'ResepsiyonistAtayabilir', 'KullaniciAtama', 0, @Now, @Now);
                END

                DECLARE @AdminGroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'Yönetici Grubu');
                DECLARE @TesisYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'TesisYoneticiGrubu');
                DECLARE @BinaYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'BinaYoneticiGrubu');
                DECLARE @ResepsiyonistGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'ResepsiyonistGrubu');

                IF @TesisYoneticiGrupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @TesisYoneticisiAtanabilir)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @TesisYoneticiGrupId, @TesisYoneticisiAtanabilir, 0, @Now, @Now);

                IF @BinaYoneticiGrupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @BinaYoneticiGrupId AND [RoleId] = @BinaYoneticisiAtanabilir)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @BinaYoneticiGrupId, @BinaYoneticisiAtanabilir, 0, @Now, @Now);

                IF @ResepsiyonistGrupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGrupId AND [RoleId] = @ResepsiyonistAtanabilir)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @ResepsiyonistGrupId, @ResepsiyonistAtanabilir, 0, @Now, @Now);

                IF @AdminGroupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @TesisYoneticisiAtayabilir)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @AdminGroupId, @TesisYoneticisiAtayabilir, 0, @Now, @Now);

                IF @AdminGroupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @BinaYoneticisiAtayabilir)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @AdminGroupId, @BinaYoneticisiAtayabilir, 0, @Now, @Now);

                IF @AdminGroupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ResepsiyonistAtayabilir)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @AdminGroupId, @ResepsiyonistAtayabilir, 0, @Now, @Now);

                IF @TesisYoneticiGrupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @BinaYoneticisiAtayabilir)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @TesisYoneticiGrupId, @BinaYoneticisiAtayabilir, 0, @Now, @Now);

                IF @TesisYoneticiGrupId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @ResepsiyonistAtayabilir)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @TesisYoneticiGrupId, @ResepsiyonistAtayabilir, 0, @Now, @Now);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @TesisYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'TesisYoneticiGrubu');
                DECLARE @BinaYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'BinaYoneticiGrubu');
                DECLARE @ResepsiyonistGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'ResepsiyonistGrubu');

                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE r.[Domain] = 'KullaniciAtama'
                  AND (
                    (r.[Name] = 'TesisYoneticisiAtanabilir' AND ugr.[UserGroupId] = @TesisYoneticiGrupId)
                    OR (r.[Name] = 'BinaYoneticisiAtanabilir' AND ugr.[UserGroupId] = @BinaYoneticiGrupId)
                    OR (r.[Name] = 'ResepsiyonistAtanabilir' AND ugr.[UserGroupId] = @ResepsiyonistGrupId)
                  );

                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE r.[Domain] = 'KullaniciAtama'
                  AND r.[Name] IN ('TesisYoneticisiAtayabilir', 'BinaYoneticisiAtayabilir', 'ResepsiyonistAtayabilir');

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] = 'KullaniciAtama'
                  AND [Name] IN ('TesisYoneticisiAtayabilir', 'BinaYoneticisiAtayabilir', 'ResepsiyonistAtayabilir')
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [RoleId] = [TODBase].[Roles].[Id])
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [RoleId] = [TODBase].[Roles].[Id]);
                """);
        }
    }
}
