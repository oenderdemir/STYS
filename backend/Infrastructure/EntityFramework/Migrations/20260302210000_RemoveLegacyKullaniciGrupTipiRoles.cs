using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260302210000_RemoveLegacyKullaniciGrupTipiRoles")]
    public partial class RemoveLegacyKullaniciGrupTipiRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @TesisAtanabilir uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'TesisYoneticisiAtanabilir');
                IF @TesisAtanabilir IS NULL
                BEGIN
                    SET @TesisAtanabilir = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@TesisAtanabilir, 'TesisYoneticisiAtanabilir', 'KullaniciAtama', 0, @Now, @Now);
                END

                DECLARE @BinaAtanabilir uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'BinaYoneticisiAtanabilir');
                IF @BinaAtanabilir IS NULL
                BEGIN
                    SET @BinaAtanabilir = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@BinaAtanabilir, 'BinaYoneticisiAtanabilir', 'KullaniciAtama', 0, @Now, @Now);
                END

                DECLARE @ResepsiyonistAtanabilir uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'ResepsiyonistAtanabilir');
                IF @ResepsiyonistAtanabilir IS NULL
                BEGIN
                    SET @ResepsiyonistAtanabilir = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ResepsiyonistAtanabilir, 'ResepsiyonistAtanabilir', 'KullaniciAtama', 0, @Now, @Now);
                END

                DECLARE @LegacyTesis uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciGrupTipi' AND [Name] = 'TesisYoneticisi');
                DECLARE @LegacyBina uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciGrupTipi' AND [Name] = 'BinaYoneticisi');
                DECLARE @LegacyResepsiyonist uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciGrupTipi' AND [Name] = 'Resepsiyonist');

                IF @LegacyTesis IS NOT NULL
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    SELECT NEWID(), ugr.[UserGroupId], @TesisAtanabilir, 0, @Now, @Now
                    FROM [TODBase].[UserGroupRoles] ugr
                    WHERE ugr.[RoleId] = @LegacyTesis
                      AND NOT EXISTS (
                        SELECT 1
                        FROM [TODBase].[UserGroupRoles] target
                        WHERE target.[UserGroupId] = ugr.[UserGroupId]
                          AND target.[RoleId] = @TesisAtanabilir
                      );
                END

                IF @LegacyBina IS NOT NULL
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    SELECT NEWID(), ugr.[UserGroupId], @BinaAtanabilir, 0, @Now, @Now
                    FROM [TODBase].[UserGroupRoles] ugr
                    WHERE ugr.[RoleId] = @LegacyBina
                      AND NOT EXISTS (
                        SELECT 1
                        FROM [TODBase].[UserGroupRoles] target
                        WHERE target.[UserGroupId] = ugr.[UserGroupId]
                          AND target.[RoleId] = @BinaAtanabilir
                      );
                END

                IF @LegacyResepsiyonist IS NOT NULL
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    SELECT NEWID(), ugr.[UserGroupId], @ResepsiyonistAtanabilir, 0, @Now, @Now
                    FROM [TODBase].[UserGroupRoles] ugr
                    WHERE ugr.[RoleId] = @LegacyResepsiyonist
                      AND NOT EXISTS (
                        SELECT 1
                        FROM [TODBase].[UserGroupRoles] target
                        WHERE target.[UserGroupId] = ugr.[UserGroupId]
                          AND target.[RoleId] = @ResepsiyonistAtanabilir
                      );
                END

                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                WHERE r.[Domain] = 'KullaniciGrupTipi';

                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE r.[Domain] = 'KullaniciGrupTipi';

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] = 'KullaniciGrupTipi';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @LegacyTesis uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciGrupTipi' AND [Name] = 'TesisYoneticisi');
                IF @LegacyTesis IS NULL
                BEGIN
                    SET @LegacyTesis = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@LegacyTesis, 'TesisYoneticisi', 'KullaniciGrupTipi', 0, @Now, @Now);
                END

                DECLARE @LegacyBina uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciGrupTipi' AND [Name] = 'BinaYoneticisi');
                IF @LegacyBina IS NULL
                BEGIN
                    SET @LegacyBina = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@LegacyBina, 'BinaYoneticisi', 'KullaniciGrupTipi', 0, @Now, @Now);
                END

                DECLARE @LegacyResepsiyonist uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciGrupTipi' AND [Name] = 'Resepsiyonist');
                IF @LegacyResepsiyonist IS NULL
                BEGIN
                    SET @LegacyResepsiyonist = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@LegacyResepsiyonist, 'Resepsiyonist', 'KullaniciGrupTipi', 0, @Now, @Now);
                END

                DECLARE @TesisAtanabilir uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'TesisYoneticisiAtanabilir');
                DECLARE @BinaAtanabilir uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'BinaYoneticisiAtanabilir');
                DECLARE @ResepsiyonistAtanabilir uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciAtama' AND [Name] = 'ResepsiyonistAtanabilir');

                IF @TesisAtanabilir IS NOT NULL
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    SELECT NEWID(), ugr.[UserGroupId], @LegacyTesis, 0, @Now, @Now
                    FROM [TODBase].[UserGroupRoles] ugr
                    WHERE ugr.[RoleId] = @TesisAtanabilir
                      AND NOT EXISTS (
                        SELECT 1
                        FROM [TODBase].[UserGroupRoles] target
                        WHERE target.[UserGroupId] = ugr.[UserGroupId]
                          AND target.[RoleId] = @LegacyTesis
                      );
                END

                IF @BinaAtanabilir IS NOT NULL
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    SELECT NEWID(), ugr.[UserGroupId], @LegacyBina, 0, @Now, @Now
                    FROM [TODBase].[UserGroupRoles] ugr
                    WHERE ugr.[RoleId] = @BinaAtanabilir
                      AND NOT EXISTS (
                        SELECT 1
                        FROM [TODBase].[UserGroupRoles] target
                        WHERE target.[UserGroupId] = ugr.[UserGroupId]
                          AND target.[RoleId] = @LegacyBina
                      );
                END

                IF @ResepsiyonistAtanabilir IS NOT NULL
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    SELECT NEWID(), ugr.[UserGroupId], @LegacyResepsiyonist, 0, @Now, @Now
                    FROM [TODBase].[UserGroupRoles] ugr
                    WHERE ugr.[RoleId] = @ResepsiyonistAtanabilir
                      AND NOT EXISTS (
                        SELECT 1
                        FROM [TODBase].[UserGroupRoles] target
                        WHERE target.[UserGroupId] = ugr.[UserGroupId]
                          AND target.[RoleId] = @LegacyResepsiyonist
                      );
                END
                """);
        }
    }
}
