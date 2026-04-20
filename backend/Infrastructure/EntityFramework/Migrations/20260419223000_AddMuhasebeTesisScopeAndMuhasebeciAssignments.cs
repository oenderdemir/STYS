using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260419223000_AddMuhasebeTesisScopeAndMuhasebeciAssignments")]
public partial class AddMuhasebeTesisScopeAndMuhasebeciAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            IF OBJECT_ID(N'[dbo].[TesisMuhasebecileri]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[TesisMuhasebecileri]
                (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [TesisId] int NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_TesisMuhasebecileri_IsDeleted] DEFAULT (0),
                    [CreatedAt] datetime2 NULL,
                    [UpdatedAt] datetime2 NULL,
                    [DeletedAt] datetime2 NULL,
                    [CreatedBy] nvarchar(max) NULL,
                    [UpdatedBy] nvarchar(max) NULL,
                    [DeletedBy] nvarchar(max) NULL,
                    CONSTRAINT [PK_TesisMuhasebecileri] PRIMARY KEY ([Id])
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_TesisMuhasebecileri_Tesisler_TesisId')
            BEGIN
                ALTER TABLE [dbo].[TesisMuhasebecileri]
                ADD CONSTRAINT [FK_TesisMuhasebecileri_Tesisler_TesisId]
                FOREIGN KEY ([TesisId]) REFERENCES [dbo].[Tesisler]([Id]) ON DELETE NO ACTION;
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_TesisMuhasebecileri_TesisId_UserId' AND [object_id] = OBJECT_ID(N'[dbo].[TesisMuhasebecileri]'))
            BEGIN
                CREATE UNIQUE INDEX [IX_TesisMuhasebecileri_TesisId_UserId]
                ON [dbo].[TesisMuhasebecileri]([TesisId], [UserId])
                WHERE [IsDeleted] = 0;
            END;

            IF COL_LENGTH(N'[muhasebe].[CariKartlar]', N'TesisId') IS NULL
                ALTER TABLE [muhasebe].[CariKartlar] ADD [TesisId] int NULL;

            IF COL_LENGTH(N'[muhasebe].[KasaBankaHesaplari]', N'TesisId') IS NULL
                ALTER TABLE [muhasebe].[KasaBankaHesaplari] ADD [TesisId] int NULL;

            IF COL_LENGTH(N'[muhasebe].[Hesaplar]', N'TesisId') IS NULL
                ALTER TABLE [muhasebe].[Hesaplar] ADD [TesisId] int NULL;

            IF COL_LENGTH(N'[muhasebe].[TasinirKartlar]', N'TesisId') IS NULL
                ALTER TABLE [muhasebe].[TasinirKartlar] ADD [TesisId] int NULL;
            """);

        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_CariKartlar_Tesisler_TesisId')
                ALTER TABLE [muhasebe].[CariKartlar] ADD CONSTRAINT [FK_CariKartlar_Tesisler_TesisId] FOREIGN KEY ([TesisId]) REFERENCES [dbo].[Tesisler]([Id]) ON DELETE NO ACTION;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_KasaBankaHesaplari_Tesisler_TesisId')
                ALTER TABLE [muhasebe].[KasaBankaHesaplari] ADD CONSTRAINT [FK_KasaBankaHesaplari_Tesisler_TesisId] FOREIGN KEY ([TesisId]) REFERENCES [dbo].[Tesisler]([Id]) ON DELETE NO ACTION;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Hesaplar_Tesisler_TesisId')
                ALTER TABLE [muhasebe].[Hesaplar] ADD CONSTRAINT [FK_Hesaplar_Tesisler_TesisId] FOREIGN KEY ([TesisId]) REFERENCES [dbo].[Tesisler]([Id]) ON DELETE NO ACTION;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_TasinirKartlar_Tesisler_TesisId')
                ALTER TABLE [muhasebe].[TasinirKartlar] ADD CONSTRAINT [FK_TasinirKartlar_Tesisler_TesisId] FOREIGN KEY ([TesisId]) REFERENCES [dbo].[Tesisler]([Id]) ON DELETE NO ACTION;

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CariKartlar_CariKodu' AND [object_id] = OBJECT_ID(N'[muhasebe].[CariKartlar]'))
                DROP INDEX [IX_CariKartlar_CariKodu] ON [muhasebe].[CariKartlar];
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CariKartlar_TesisId_CariKodu' AND [object_id] = OBJECT_ID(N'[muhasebe].[CariKartlar]'))
                CREATE UNIQUE INDEX [IX_CariKartlar_TesisId_CariKodu] ON [muhasebe].[CariKartlar]([TesisId], [CariKodu]) WHERE [IsDeleted] = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CariKartlar_TesisId' AND [object_id] = OBJECT_ID(N'[muhasebe].[CariKartlar]'))
                CREATE INDEX [IX_CariKartlar_TesisId] ON [muhasebe].[CariKartlar]([TesisId]) WHERE [IsDeleted] = 0 AND [TesisId] IS NOT NULL;

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_KasaBankaHesaplari_Kod' AND [object_id] = OBJECT_ID(N'[muhasebe].[KasaBankaHesaplari]'))
                DROP INDEX [IX_KasaBankaHesaplari_Kod] ON [muhasebe].[KasaBankaHesaplari];
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_KasaBankaHesaplari_TesisId_Kod' AND [object_id] = OBJECT_ID(N'[muhasebe].[KasaBankaHesaplari]'))
                CREATE UNIQUE INDEX [IX_KasaBankaHesaplari_TesisId_Kod] ON [muhasebe].[KasaBankaHesaplari]([TesisId], [Kod]) WHERE [IsDeleted] = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_KasaBankaHesaplari_TesisId' AND [object_id] = OBJECT_ID(N'[muhasebe].[KasaBankaHesaplari]'))
                CREATE INDEX [IX_KasaBankaHesaplari_TesisId] ON [muhasebe].[KasaBankaHesaplari]([TesisId]) WHERE [IsDeleted] = 0 AND [TesisId] IS NOT NULL;

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Hesaplar_Ad' AND [object_id] = OBJECT_ID(N'[muhasebe].[Hesaplar]'))
                DROP INDEX [IX_Hesaplar_Ad] ON [muhasebe].[Hesaplar];
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Hesaplar_TesisId_Ad' AND [object_id] = OBJECT_ID(N'[muhasebe].[Hesaplar]'))
                CREATE UNIQUE INDEX [IX_Hesaplar_TesisId_Ad] ON [muhasebe].[Hesaplar]([TesisId], [Ad]) WHERE [IsDeleted] = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Hesaplar_TesisId' AND [object_id] = OBJECT_ID(N'[muhasebe].[Hesaplar]'))
                CREATE INDEX [IX_Hesaplar_TesisId] ON [muhasebe].[Hesaplar]([TesisId]) WHERE [IsDeleted] = 0 AND [TesisId] IS NOT NULL;

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_TasinirKartlar_StokKodu' AND [object_id] = OBJECT_ID(N'[muhasebe].[TasinirKartlar]'))
                DROP INDEX [IX_TasinirKartlar_StokKodu] ON [muhasebe].[TasinirKartlar];
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_TasinirKartlar_TesisId_StokKodu' AND [object_id] = OBJECT_ID(N'[muhasebe].[TasinirKartlar]'))
                CREATE UNIQUE INDEX [IX_TasinirKartlar_TesisId_StokKodu] ON [muhasebe].[TasinirKartlar]([TesisId], [StokKodu]) WHERE [IsDeleted] = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_TasinirKartlar_TesisId' AND [object_id] = OBJECT_ID(N'[muhasebe].[TasinirKartlar]'))
                CREATE INDEX [IX_TasinirKartlar_TesisId] ON [muhasebe].[TasinirKartlar]([TesisId]) WHERE [IsDeleted] = 0 AND [TesisId] IS NOT NULL;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @Domain nvarchar(128) = N'KullaniciAtama';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = @Domain AND [Name] = N'MuhasebeciAtanabilir')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'MuhasebeciAtanabilir', @Domain, 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = @Domain AND [Name] = N'MuhasebeciAtayabilir')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), N'MuhasebeciAtayabilir', @Domain, 0, @Now, @Now);

            DECLARE @MuhasebeciGrupAdi nvarchar(128) = N'MuhasebeciGrubu';
            DECLARE @MuhasebeciGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222207';

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Name] = @MuhasebeciGrupAdi AND [IsDeleted] = 0)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @MuhasebeciGroupId)
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@MuhasebeciGroupId, @MuhasebeciGrupAdi, 0, @Now, @Now);
                ELSE
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeciGrupAdi, 0, @Now, @Now);
            END;

            SET @MuhasebeciGroupId = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = @MuhasebeciGrupAdi AND [IsDeleted] = 0 ORDER BY [CreatedAt]);

            DECLARE @AdminGroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] IN (N'YoneticiGrubu', N'Yönetici Grubu') AND [IsDeleted] = 0 ORDER BY [CreatedAt]);
            DECLARE @TesisManagerGroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'TesisYoneticiGrubu' AND [IsDeleted] = 0 ORDER BY [CreatedAt]);

            DECLARE @RequiredRoles TABLE ([Domain] nvarchar(128) NOT NULL, [Name] nvarchar(64) NOT NULL, [TargetGroup] nvarchar(32) NOT NULL);

            INSERT INTO @RequiredRoles ([Domain], [Name], [TargetGroup])
            VALUES
            (N'KullaniciAtama', N'MuhasebeciAtanabilir', N'Accountant'),
            (N'CariKartYonetimi', N'Menu', N'Accountant'),
            (N'CariKartYonetimi', N'View', N'Accountant'),
            (N'CariKartYonetimi', N'Manage', N'Accountant'),
            (N'CariHareketYonetimi', N'Menu', N'Accountant'),
            (N'CariHareketYonetimi', N'View', N'Accountant'),
            (N'CariHareketYonetimi', N'Manage', N'Accountant'),
            (N'KasaHareketYonetimi', N'Menu', N'Accountant'),
            (N'KasaHareketYonetimi', N'View', N'Accountant'),
            (N'KasaHareketYonetimi', N'Manage', N'Accountant'),
            (N'BankaHareketYonetimi', N'Menu', N'Accountant'),
            (N'BankaHareketYonetimi', N'View', N'Accountant'),
            (N'BankaHareketYonetimi', N'Manage', N'Accountant'),
            (N'TahsilatOdemeBelgesiYonetimi', N'Menu', N'Accountant'),
            (N'TahsilatOdemeBelgesiYonetimi', N'View', N'Accountant'),
            (N'TahsilatOdemeBelgesiYonetimi', N'Manage', N'Accountant'),
            (N'TasinirKodYonetimi', N'Menu', N'Accountant'),
            (N'TasinirKodYonetimi', N'View', N'Accountant'),
            (N'TasinirKodYonetimi', N'Manage', N'Accountant'),
            (N'TasinirKartYonetimi', N'Menu', N'Accountant'),
            (N'TasinirKartYonetimi', N'View', N'Accountant'),
            (N'TasinirKartYonetimi', N'Manage', N'Accountant'),
            (N'DepoYonetimi', N'Menu', N'Accountant'),
            (N'DepoYonetimi', N'View', N'Accountant'),
            (N'DepoYonetimi', N'Manage', N'Accountant'),
            (N'StokHareketYonetimi', N'Menu', N'Accountant'),
            (N'StokHareketYonetimi', N'View', N'Accountant'),
            (N'StokHareketYonetimi', N'Manage', N'Accountant'),
            (N'MuhasebeHesapPlaniYonetimi', N'Menu', N'Accountant'),
            (N'MuhasebeHesapPlaniYonetimi', N'View', N'Accountant'),
            (N'MuhasebeHesapPlaniYonetimi', N'Manage', N'Accountant'),
            (N'KasaBankaHesapYonetimi', N'Menu', N'Accountant'),
            (N'KasaBankaHesapYonetimi', N'View', N'Accountant'),
            (N'KasaBankaHesapYonetimi', N'Manage', N'Accountant'),
            (N'HesapYonetimi', N'Menu', N'Accountant'),
            (N'HesapYonetimi', N'View', N'Accountant'),
            (N'HesapYonetimi', N'Manage', N'Accountant'),
            (N'KullaniciAtama', N'MuhasebeciAtayabilir', N'TesisManager'),
            (N'KullaniciAtama', N'MuhasebeciAtayabilir', N'Admin');

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

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(),
                   CASE rr.[TargetGroup]
                       WHEN N'Accountant' THEN @MuhasebeciGroupId
                       WHEN N'TesisManager' THEN @TesisManagerGroupId
                       WHEN N'Admin' THEN @AdminGroupId
                   END,
                   r.[Id],
                   0,
                   @Now,
                   @Now
            FROM @RequiredRoles rr
            INNER JOIN [TODBase].[Roles] r
                ON r.[Domain] = rr.[Domain]
               AND r.[Name] = rr.[Name]
            WHERE CASE rr.[TargetGroup]
                      WHEN N'Accountant' THEN @MuhasebeciGroupId
                      WHEN N'TesisManager' THEN @TesisManagerGroupId
                      WHEN N'Admin' THEN @AdminGroupId
                  END IS NOT NULL
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [TODBase].[UserGroupRoles] ugr
                  WHERE ugr.[UserGroupId] =
                        CASE rr.[TargetGroup]
                            WHEN N'Accountant' THEN @MuhasebeciGroupId
                            WHEN N'TesisManager' THEN @TesisManagerGroupId
                            WHEN N'Admin' THEN @AdminGroupId
                        END
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

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CariKartlar_TesisId_CariKodu' AND [object_id] = OBJECT_ID(N'[muhasebe].[CariKartlar]'))
                DROP INDEX [IX_CariKartlar_TesisId_CariKodu] ON [muhasebe].[CariKartlar];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CariKartlar_TesisId' AND [object_id] = OBJECT_ID(N'[muhasebe].[CariKartlar]'))
                DROP INDEX [IX_CariKartlar_TesisId] ON [muhasebe].[CariKartlar];
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_CariKartlar_CariKodu' AND [object_id] = OBJECT_ID(N'[muhasebe].[CariKartlar]'))
                CREATE UNIQUE INDEX [IX_CariKartlar_CariKodu] ON [muhasebe].[CariKartlar]([CariKodu]) WHERE [IsDeleted] = 0;

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_KasaBankaHesaplari_TesisId_Kod' AND [object_id] = OBJECT_ID(N'[muhasebe].[KasaBankaHesaplari]'))
                DROP INDEX [IX_KasaBankaHesaplari_TesisId_Kod] ON [muhasebe].[KasaBankaHesaplari];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_KasaBankaHesaplari_TesisId' AND [object_id] = OBJECT_ID(N'[muhasebe].[KasaBankaHesaplari]'))
                DROP INDEX [IX_KasaBankaHesaplari_TesisId] ON [muhasebe].[KasaBankaHesaplari];
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_KasaBankaHesaplari_Kod' AND [object_id] = OBJECT_ID(N'[muhasebe].[KasaBankaHesaplari]'))
                CREATE UNIQUE INDEX [IX_KasaBankaHesaplari_Kod] ON [muhasebe].[KasaBankaHesaplari]([Kod]) WHERE [IsDeleted] = 0;

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Hesaplar_TesisId_Ad' AND [object_id] = OBJECT_ID(N'[muhasebe].[Hesaplar]'))
                DROP INDEX [IX_Hesaplar_TesisId_Ad] ON [muhasebe].[Hesaplar];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Hesaplar_TesisId' AND [object_id] = OBJECT_ID(N'[muhasebe].[Hesaplar]'))
                DROP INDEX [IX_Hesaplar_TesisId] ON [muhasebe].[Hesaplar];
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Hesaplar_Ad' AND [object_id] = OBJECT_ID(N'[muhasebe].[Hesaplar]'))
                CREATE UNIQUE INDEX [IX_Hesaplar_Ad] ON [muhasebe].[Hesaplar]([Ad]) WHERE [IsDeleted] = 0;

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_TasinirKartlar_TesisId_StokKodu' AND [object_id] = OBJECT_ID(N'[muhasebe].[TasinirKartlar]'))
                DROP INDEX [IX_TasinirKartlar_TesisId_StokKodu] ON [muhasebe].[TasinirKartlar];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_TasinirKartlar_TesisId' AND [object_id] = OBJECT_ID(N'[muhasebe].[TasinirKartlar]'))
                DROP INDEX [IX_TasinirKartlar_TesisId] ON [muhasebe].[TasinirKartlar];
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_TasinirKartlar_StokKodu' AND [object_id] = OBJECT_ID(N'[muhasebe].[TasinirKartlar]'))
                CREATE UNIQUE INDEX [IX_TasinirKartlar_StokKodu] ON [muhasebe].[TasinirKartlar]([StokKodu]) WHERE [IsDeleted] = 0;

            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_CariKartlar_Tesisler_TesisId')
                ALTER TABLE [muhasebe].[CariKartlar] DROP CONSTRAINT [FK_CariKartlar_Tesisler_TesisId];
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_KasaBankaHesaplari_Tesisler_TesisId')
                ALTER TABLE [muhasebe].[KasaBankaHesaplari] DROP CONSTRAINT [FK_KasaBankaHesaplari_Tesisler_TesisId];
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Hesaplar_Tesisler_TesisId')
                ALTER TABLE [muhasebe].[Hesaplar] DROP CONSTRAINT [FK_Hesaplar_Tesisler_TesisId];
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_TasinirKartlar_Tesisler_TesisId')
                ALTER TABLE [muhasebe].[TasinirKartlar] DROP CONSTRAINT [FK_TasinirKartlar_Tesisler_TesisId];

            IF COL_LENGTH(N'[muhasebe].[CariKartlar]', N'TesisId') IS NOT NULL
                ALTER TABLE [muhasebe].[CariKartlar] DROP COLUMN [TesisId];
            IF COL_LENGTH(N'[muhasebe].[KasaBankaHesaplari]', N'TesisId') IS NOT NULL
                ALTER TABLE [muhasebe].[KasaBankaHesaplari] DROP COLUMN [TesisId];
            IF COL_LENGTH(N'[muhasebe].[Hesaplar]', N'TesisId') IS NOT NULL
                ALTER TABLE [muhasebe].[Hesaplar] DROP COLUMN [TesisId];
            IF COL_LENGTH(N'[muhasebe].[TasinirKartlar]', N'TesisId') IS NOT NULL
                ALTER TABLE [muhasebe].[TasinirKartlar] DROP COLUMN [TesisId];

            IF OBJECT_ID(N'[dbo].[TesisMuhasebecileri]', N'U') IS NOT NULL
                DROP TABLE [dbo].[TesisMuhasebecileri];
            """);
    }
}
