using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class SeedOdaSinifiMenuItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                DECLARE @OdaSinifiMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666615';
                DECLARE @OdaTipiYonetimiView uniqueidentifier = NULL;

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Oda Siniflari' AND [ParentId] = @MainMenuId)
                    SELECT @OdaSinifiMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Oda Siniflari' AND [ParentId] = @MainMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems]
                        ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@OdaSinifiMenuId, N'Oda Siniflari', 'fa-solid fa-layer-group', 'oda-siniflari', NULL, 7, @MainMenuId, 0, @Now, @Now);

                SELECT @OdaTipiYonetimiView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaTipiYonetimi' AND [Name] = 'View';

                IF @OdaTipiYonetimiView IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @OdaSinifiMenuId AND [RoleId] = @OdaTipiYonetimiView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES ('77777777-7777-7777-7777-777777777712', @OdaSinifiMenuId, @OdaTipiYonetimiView, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                DECLARE @OdaSinifiMenuId uniqueidentifier = NULL;

                SELECT @OdaSinifiMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [ParentId] = @MainMenuId AND [Route] = 'oda-siniflari';

                IF @OdaSinifiMenuId IS NOT NULL
                BEGIN
                    DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @OdaSinifiMenuId;
                    DELETE FROM [TODBase].[MenuItems] WHERE [Id] = @OdaSinifiMenuId;
                END
                """);
        }
    }
}
