using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260620160000_FixOdaRezervasyonTakvimiPermissions")]
public partial class FixOdaRezervasyonTakvimiPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();

            -- ---------------------------------------------------------------
            -- 1. Hatalı permission kayıtlarını temizle
            --    • Domain=RezervasyonYonetimi, Name=OdaRezervasyonTakvimi.View  (3-parçalı format)
            --    • Domain=RezervasyonYonetimi, Name=OdaRezervasyonTakvimiView   (yanlış domain)
            -- ---------------------------------------------------------------
            DECLARE @BadRoleIds TABLE ([Id] uniqueidentifier NOT NULL);

            INSERT INTO @BadRoleIds ([Id])
            SELECT [Id] FROM [TODBase].[Roles]
            WHERE [Domain] = N'RezervasyonYonetimi'
              AND [Name] IN (N'OdaRezervasyonTakvimi.View', N'OdaRezervasyonTakvimiView')
              AND [IsDeleted] = 0;

            -- UserGroupRoles bağlantılarını sil
            UPDATE ugr
            SET ugr.[IsDeleted] = 1, ugr.[UpdatedAt] = @Now
            FROM [TODBase].[UserGroupRoles] ugr
            INNER JOIN @BadRoleIds b ON ugr.[RoleId] = b.[Id]
            WHERE ugr.[IsDeleted] = 0;

            -- MenuItemRoles bağlantılarını sil
            UPDATE mir
            SET mir.[IsDeleted] = 1, mir.[UpdatedAt] = @Now
            FROM [TODBase].[MenuItemRoles] mir
            INNER JOIN @BadRoleIds b ON mir.[RoleId] = b.[Id]
            WHERE mir.[IsDeleted] = 0;

            -- Rolleri sil
            UPDATE [TODBase].[Roles]
            SET [IsDeleted] = 1, [UpdatedAt] = @Now
            WHERE [Id] IN (SELECT [Id] FROM @BadRoleIds);

            -- ---------------------------------------------------------------
            -- 2. Doğru permission rolleri: OdaRezervasyonTakvimiYonetimi.Menu / .View
            -- ---------------------------------------------------------------
            DECLARE @RequiredRoles TABLE ([Domain] nvarchar(128) NOT NULL, [Name] nvarchar(64) NOT NULL);
            INSERT INTO @RequiredRoles ([Domain], [Name]) VALUES
                (N'OdaRezervasyonTakvimiYonetimi', N'Menu'),
                (N'OdaRezervasyonTakvimiYonetimi', N'View');

            INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), rr.[Name], rr.[Domain], 0, @Now, @Now
            FROM @RequiredRoles rr
            WHERE NOT EXISTS (
                SELECT 1 FROM [TODBase].[Roles] r
                WHERE r.[Domain] = rr.[Domain] AND r.[Name] = rr.[Name] AND r.[IsDeleted] = 0
            );

            DECLARE @MenuRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'OdaRezervasyonTakvimiYonetimi' AND [Name] = N'Menu' AND [IsDeleted] = 0
            );
            DECLARE @ViewRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'OdaRezervasyonTakvimiYonetimi' AND [Name] = N'View' AND [IsDeleted] = 0
            );

            -- ---------------------------------------------------------------
            -- 3. Admin, TesisYöneticisi, Resepsiyonist gruplarına ata
            -- ---------------------------------------------------------------
            DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
            DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
            DECLARE @ResepsiyonistGroupId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[UserGroups]
                WHERE [Name] = N'ResepsiyonistGrubu' AND [IsDeleted] = 0
                ORDER BY [CreatedAt]
            );

            DECLARE @Groups TABLE ([GroupId] uniqueidentifier NOT NULL);
            INSERT INTO @Groups ([GroupId])
            SELECT v.[Id]
            FROM (VALUES (@AdminGroupId), (@TesisManagerGroupId)) AS v([Id])
            WHERE EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = v.[Id] AND [IsDeleted] = 0);

            IF @ResepsiyonistGroupId IS NOT NULL
                INSERT INTO @Groups ([GroupId]) VALUES (@ResepsiyonistGroupId);

            INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
            SELECT NEWID(), g.[GroupId], r.[RoleId], 0, @Now, @Now
            FROM @Groups g
            CROSS JOIN (SELECT @MenuRoleId AS [RoleId] UNION ALL SELECT @ViewRoleId) r
            WHERE r.[RoleId] IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM [TODBase].[UserGroupRoles] ugr
                  WHERE ugr.[UserGroupId] = g.[GroupId]
                    AND ugr.[RoleId] = r.[RoleId]
                    AND ugr.[IsDeleted] = 0
              );

            -- ---------------------------------------------------------------
            -- 4. Menü item varsa OdaRezervasyonTakvimiYonetimi.Menu rolünü bağla
            -- ---------------------------------------------------------------
            DECLARE @OdaTakvimiMenuItemId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[MenuItems]
                WHERE [Route] = N'oda-rezervasyon-takvimi' AND [IsDeleted] = 0
            );

            IF @OdaTakvimiMenuItemId IS NOT NULL AND @MenuRoleId IS NOT NULL
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM [TODBase].[MenuItemRoles]
                    WHERE [MenuItemId] = @OdaTakvimiMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0
                )
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @OdaTakvimiMenuItemId, @MenuRoleId, 0, @Now, @Now);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            UPDATE [TODBase].[UserGroupRoles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [RoleId] IN (
                SELECT [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'OdaRezervasyonTakvimiYonetimi' AND [IsDeleted] = 0
            ) AND [IsDeleted] = 0;

            UPDATE [TODBase].[MenuItemRoles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [RoleId] IN (
                SELECT [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'OdaRezervasyonTakvimiYonetimi' AND [IsDeleted] = 0
            ) AND [IsDeleted] = 0;

            UPDATE [TODBase].[Roles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Domain] = N'OdaRezervasyonTakvimiYonetimi' AND [IsDeleted] = 0;
            """);
    }
}
