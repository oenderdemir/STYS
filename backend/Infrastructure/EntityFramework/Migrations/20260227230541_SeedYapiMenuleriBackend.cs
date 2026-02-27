using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class SeedYapiMenuleriBackend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
                BEGIN
                    INSERT INTO [TODBase].[MenuItems]
                        ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@MainMenuId, N'Ana Menü', 'fa-solid fa-person-digging', '', NULL, 999, NULL, 0, @Now, @Now);
                END

                DECLARE @IlMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666609';
                DECLARE @TesisMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666610';
                DECLARE @BinaMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666611';
                DECLARE @IsletmeAlaniMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666612';
                DECLARE @OdaTipiMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666613';
                DECLARE @OdaMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666614';

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Il Yonetimi' AND [ParentId] = @MainMenuId)
                    SELECT @IlMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Il Yonetimi' AND [ParentId] = @MainMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems]
                        ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@IlMenuId, N'Il Yonetimi', 'fa-solid fa-map-location-dot', 'iller', NULL, 1, @MainMenuId, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Tesis Yonetimi' AND [ParentId] = @MainMenuId)
                    SELECT @TesisMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Tesis Yonetimi' AND [ParentId] = @MainMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems]
                        ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@TesisMenuId, N'Tesis Yonetimi', 'fa-solid fa-building', 'tesisler', NULL, 2, @MainMenuId, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Bina Yonetimi' AND [ParentId] = @MainMenuId)
                    SELECT @BinaMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Bina Yonetimi' AND [ParentId] = @MainMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems]
                        ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@BinaMenuId, N'Bina Yonetimi', 'fa-solid fa-city', 'binalar', NULL, 3, @MainMenuId, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Isletme Alanlari' AND [ParentId] = @MainMenuId)
                    SELECT @IsletmeAlaniMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Isletme Alanlari' AND [ParentId] = @MainMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems]
                        ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@IsletmeAlaniMenuId, N'Isletme Alanlari', 'fa-solid fa-store', 'isletme-alanlari', NULL, 4, @MainMenuId, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Oda Tipleri' AND [ParentId] = @MainMenuId)
                    SELECT @OdaTipiMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Oda Tipleri' AND [ParentId] = @MainMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems]
                        ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@OdaTipiMenuId, N'Oda Tipleri', 'fa-solid fa-bed', 'oda-tipleri', NULL, 5, @MainMenuId, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Odalar' AND [ParentId] = @MainMenuId)
                    SELECT @OdaMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Odalar' AND [ParentId] = @MainMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems]
                        ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@OdaMenuId, N'Odalar', 'fa-solid fa-door-closed', 'odalar', NULL, 6, @MainMenuId, 0, @Now, @Now);

                DECLARE @IlYonetimiView uniqueidentifier = NULL;
                DECLARE @TesisYonetimiView uniqueidentifier = NULL;
                DECLARE @BinaYonetimiView uniqueidentifier = NULL;
                DECLARE @IsletmeAlaniYonetimiView uniqueidentifier = NULL;
                DECLARE @OdaTipiYonetimiView uniqueidentifier = NULL;
                DECLARE @OdaYonetimiView uniqueidentifier = NULL;

                SELECT @IlYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'IlYonetimi' AND [Name] = 'View';
                SELECT @TesisYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'TesisYonetimi' AND [Name] = 'View';
                SELECT @BinaYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'BinaYonetimi' AND [Name] = 'View';
                SELECT @IsletmeAlaniYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'IsletmeAlaniYonetimi' AND [Name] = 'View';
                SELECT @OdaTipiYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaTipiYonetimi' AND [Name] = 'View';
                SELECT @OdaYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'View';

                IF @IlYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @IlMenuId AND [RoleId] = @IlYonetimiView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('77777777-7777-7777-7777-777777777706', @IlMenuId, @IlYonetimiView, 0, @Now, @Now);

                IF @TesisYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @TesisMenuId AND [RoleId] = @TesisYonetimiView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('77777777-7777-7777-7777-777777777707', @TesisMenuId, @TesisYonetimiView, 0, @Now, @Now);

                IF @BinaYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @BinaMenuId AND [RoleId] = @BinaYonetimiView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('77777777-7777-7777-7777-777777777708', @BinaMenuId, @BinaYonetimiView, 0, @Now, @Now);

                IF @IsletmeAlaniYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @IsletmeAlaniMenuId AND [RoleId] = @IsletmeAlaniYonetimiView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('77777777-7777-7777-7777-777777777709', @IsletmeAlaniMenuId, @IsletmeAlaniYonetimiView, 0, @Now, @Now);

                IF @OdaTipiYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @OdaTipiMenuId AND [RoleId] = @OdaTipiYonetimiView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('77777777-7777-7777-7777-777777777710', @OdaTipiMenuId, @OdaTipiYonetimiView, 0, @Now, @Now);

                IF @OdaYonetimiView IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @OdaMenuId AND [RoleId] = @OdaYonetimiView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('77777777-7777-7777-7777-777777777711', @OdaMenuId, @OdaYonetimiView, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';

                DELETE mr
                FROM [TODBase].[MenuItemRoles] mr
                INNER JOIN [TODBase].[MenuItems] m ON m.[Id] = mr.[MenuItemId]
                WHERE m.[ParentId] = @MainMenuId
                  AND m.[Route] IN ('iller', 'tesisler', 'binalar', 'isletme-alanlari', 'oda-tipleri', 'odalar');

                DELETE FROM [TODBase].[MenuItems]
                WHERE [ParentId] = @MainMenuId
                  AND [Route] IN ('iller', 'tesisler', 'binalar', 'isletme-alanlari', 'oda-tipleri', 'odalar');
                """);
        }
    }
}
