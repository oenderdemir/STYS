using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

public partial class SeedMuhasebeAdminRoleAndGroup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @SeedPasswordHash nvarchar(max) = N'PBKDF2$100000$7EIkx3zl3+g/hx5ORM0tUw==$JCeyiS0ajdez/R1BKi3K5awsF1bs+D8b2neo0E6KW+k=';

            -- 1. Create MuhasebeAdmin role (Domain+MuhasebeAdmin name produces '.Admin' suffix for unlimited scope)
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeAdmin' AND [Name] = N'Admin')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), N'Admin', N'MuhasebeAdmin', 0, @Now, @Now);

            -- 2. Create muhasebe-yoneticileri-grubu user group
            DECLARE @MuhasebeAdminGroupId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
            DECLARE @MuhasebeAdminGroupName nvarchar(128) = N'muhasebe-yoneticileri-grubu';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Name] = @MuhasebeAdminGroupName AND [IsDeleted] = 0)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @MuhasebeAdminGroupId)
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MuhasebeAdminGroupId, @MuhasebeAdminGroupName, 0, @Now, @Now);
                ELSE
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @MuhasebeAdminGroupName, 0, @Now, @Now);
            END;

            SET @MuhasebeAdminGroupId = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = @MuhasebeAdminGroupName AND [IsDeleted] = 0 ORDER BY [CreatedAt]);

            -- 3. Create muhasebe-admin user
            DECLARE @MuhasebeAdminUserId uniqueidentifier = '55555555-5555-5555-5555-555555555501';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Users] WHERE [UserName] = N'muhasebe-admin')
            BEGIN
                IF EXISTS (SELECT 1 FROM [TODBase].[Users] WHERE [Id] = @MuhasebeAdminUserId)
                    SET @MuhasebeAdminUserId = NEWID();

                INSERT INTO [TODBase].[Users]
                (
                    [Id], [UserName], [NationalId], [FirstName], [LastName], [Email], [PasswordHash], [AvatarPath], [Status],
                    [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                )
                VALUES
                (
                    @MuhasebeAdminUserId, N'muhasebe-admin', NULL, N'Muhasebe', N'Admin', N'muhasebe-admin@stys.local',
                    @SeedPasswordHash, NULL, 0, 0, @Now, @Now, N'migration_seed_muhasebe_admin', N'migration_seed_muhasebe_admin'
                );
            END;
            ELSE
            BEGIN
                SET @MuhasebeAdminUserId = (SELECT TOP(1) [Id] FROM [TODBase].[Users] WHERE [UserName] = N'muhasebe-admin');
            END;

            -- 4. Assign user to group
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserUserGroups] WHERE [UserId] = @MuhasebeAdminUserId AND [UserGroupId] = @MuhasebeAdminGroupId)
            BEGIN
                INSERT INTO [TODBase].[UserUserGroups] ([Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                VALUES (NEWID(), @MuhasebeAdminUserId, @MuhasebeAdminGroupId, 0, @Now, @Now, N'migration_seed_muhasebe_admin', N'migration_seed_muhasebe_admin');
            END;

            -- 5. Define all permissions for the MuhasebeAdmin group
            DECLARE @RequiredRoles TABLE ([Domain] nvarchar(128) NOT NULL, [Name] nvarchar(64) NOT NULL);

            INSERT INTO @RequiredRoles ([Domain], [Name])
            VALUES
            -- MuhasebeAdmin identity role (unlimited scope via .Admin suffix)
            (N'MuhasebeAdmin', N'Admin'),

            -- Core Muhasebe domains: Menu, View, Manage
            (N'CariKartYonetimi', N'Menu'),
            (N'CariKartYonetimi', N'View'),
            (N'CariKartYonetimi', N'Manage'),
            (N'CariHareketYonetimi', N'Menu'),
            (N'CariHareketYonetimi', N'View'),
            (N'CariHareketYonetimi', N'Manage'),
            (N'KasaHareketYonetimi', N'Menu'),
            (N'KasaHareketYonetimi', N'View'),
            (N'KasaHareketYonetimi', N'Manage'),
            (N'BankaHareketYonetimi', N'Menu'),
            (N'BankaHareketYonetimi', N'View'),
            (N'BankaHareketYonetimi', N'Manage'),
            (N'TahsilatOdemeBelgesiYonetimi', N'Menu'),
            (N'TahsilatOdemeBelgesiYonetimi', N'View'),
            (N'TahsilatOdemeBelgesiYonetimi', N'Manage'),
            (N'TasinirKodYonetimi', N'Menu'),
            (N'TasinirKodYonetimi', N'View'),
            (N'TasinirKodYonetimi', N'Manage'),
            (N'TasinirKartYonetimi', N'Menu'),
            (N'TasinirKartYonetimi', N'View'),
            (N'TasinirKartYonetimi', N'Manage'),
            (N'DepoYonetimi', N'Menu'),
            (N'DepoYonetimi', N'View'),
            (N'DepoYonetimi', N'Manage'),
            (N'StokHareketYonetimi', N'Menu'),
            (N'StokHareketYonetimi', N'View'),
            (N'StokHareketYonetimi', N'Manage'),
            (N'MuhasebeHesapPlaniYonetimi', N'Menu'),
            (N'MuhasebeHesapPlaniYonetimi', N'View'),
            (N'MuhasebeHesapPlaniYonetimi', N'Manage'),
            (N'TasinirKodMuhasebeHesapEslemeYonetimi', N'Menu'),
            (N'TasinirKodMuhasebeHesapEslemeYonetimi', N'View'),
            (N'TasinirKodMuhasebeHesapEslemeYonetimi', N'Manage'),
            (N'MuhasebeVergiHesapEslemeYonetimi', N'Menu'),
            (N'MuhasebeVergiHesapEslemeYonetimi', N'View'),
            (N'MuhasebeVergiHesapEslemeYonetimi', N'Manage'),
            (N'MuhasebeDonemYonetimi', N'Menu'),
            (N'MuhasebeDonemYonetimi', N'View'),
            (N'MuhasebeDonemYonetimi', N'Manage'),
            (N'MuhasebeDonemYonetimi', N'ClosePeriod'),
            (N'MuhasebeFisYonetimi', N'Menu'),
            (N'MuhasebeFisYonetimi', N'View'),
            (N'MuhasebeFisYonetimi', N'Manage'),
            (N'MuhasebeHesapBakiyeYonetimi', N'Menu'),
            (N'MuhasebeHesapBakiyeYonetimi', N'View'),
            (N'MuhasebeHesapBakiyeYonetimi', N'Manage'),
            (N'MuhasebeHesapBakiyeYonetimi', N'Rebuild'),
            (N'KasaBankaHesapYonetimi', N'Menu'),
            (N'KasaBankaHesapYonetimi', N'View'),
            (N'KasaBankaHesapYonetimi', N'Manage'),
            (N'HesapYonetimi', N'Menu'),
            (N'HesapYonetimi', N'View'),
            (N'HesapYonetimi', N'Manage'),
            (N'PaketTuruYonetimi', N'Menu'),
            (N'PaketTuruYonetimi', N'View'),
            (N'PaketTuruYonetimi', N'Manage'),

            -- KullaniciAtama (muhasebeci assignment permissions)
            (N'KullaniciAtama', N'MuhasebeciAtanabilir'),
            (N'KullaniciAtama', N'MuhasebeciAtayabilir'),

            -- Related operational domains (financial context)
            (N'RezervasyonYonetimi', N'Menu'),
            (N'RezervasyonYonetimi', N'View'),
            (N'RezervasyonYonetimi', N'Manage'),

            (N'TesisYonetimi', N'View'),
            (N'BinaYonetimi', N'View'),
            (N'OdaYonetimi', N'View'),
            (N'OdaFiyatYonetimi', N'View'),

            -- Restoran domains (View for financial oversight)
            (N'RestoranYonetimi', N'View'),
            (N'RestoranSiparisYonetimi', N'View'),
            (N'RestoranOdemeYonetimi', N'View'),

            -- Kamp domains (View for financial oversight)
            (N'KampBasvuruYonetimi', N'View'),
            (N'KampRezervasyonYonetimi', N'View'),
            (N'KampIadeYonetimi', N'View'),
            (N'KampTarifeYonetimi', N'View'),

            -- LisansYonetimi
            (N'LisansYonetimi', N'Menu'),
            (N'LisansYonetimi', N'View'),
            (N'LisansYonetimi', N'Manage'),

            -- ErisimTeshisYonetimi
            (N'ErisimTeshisYonetimi', N'View');

            -- Create any missing roles
            INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), rr.[Name], rr.[Domain], 0, @Now, @Now
            FROM (SELECT DISTINCT [Domain], [Name] FROM @RequiredRoles) rr
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[Roles] r
                WHERE r.[Domain] = rr.[Domain]
                  AND r.[Name] = rr.[Name]
            );

            -- Assign all roles to the MuhasebeAdmin group
            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(),
                   @MuhasebeAdminGroupId,
                   r.[Id],
                   0,
                   @Now,
                   @Now
            FROM @RequiredRoles rr
            INNER JOIN [TODBase].[Roles] r
                ON r.[Domain] = rr.[Domain]
               AND r.[Name] = rr.[Name]
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [TODBase].[UserGroupRoles] ugr
                WHERE ugr.[UserGroupId] = @MuhasebeAdminGroupId
                  AND ugr.[RoleId] = r.[Id]
                  AND ugr.[IsDeleted] = 0
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @MuhasebeAdminUserName nvarchar(128) = N'muhasebe-admin';
            DECLARE @MuhasebeAdminGroupName nvarchar(128) = N'muhasebe-yoneticileri-grubu';

            DECLARE @UserId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Users] WHERE [UserName] = @MuhasebeAdminUserName);
            DECLARE @GroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = @MuhasebeAdminGroupName AND [IsDeleted] = 0);

            -- Remove user-group membership
            IF @UserId IS NOT NULL AND @GroupId IS NOT NULL
            BEGIN
                DELETE FROM [TODBase].[UserUserGroups] WHERE [UserId] = @UserId AND [UserGroupId] = @GroupId;
            END;

            -- Remove user
            IF @UserId IS NOT NULL
            BEGIN
                DELETE FROM [TODBase].[Users] WHERE [Id] = @UserId;
            END;

            -- Remove group-role assignments
            IF @GroupId IS NOT NULL
            BEGIN
                DELETE FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @GroupId;
            END;

            -- Remove group
            IF @GroupId IS NOT NULL
            BEGIN
                DELETE FROM [TODBase].[UserGroups] WHERE [Id] = @GroupId;
            END;

            -- Remove MuhasebeAdmin role
            DELETE FROM [TODBase].[Roles] WHERE [Domain] = N'MuhasebeAdmin' AND [Name] = N'Admin';
            """);
    }
}
