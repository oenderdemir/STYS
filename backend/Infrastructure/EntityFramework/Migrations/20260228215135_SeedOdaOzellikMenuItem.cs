using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class SeedOdaOzellikMenuItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                DECLARE @OdaOzellikMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666616';
                DECLARE @OdaOzellikYonetimiView uniqueidentifier = NULL;

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Oda Ozellikleri' AND [ParentId] = @MainMenuId)
                    SELECT @OdaOzellikMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Oda Ozellikleri' AND [ParentId] = @MainMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems]
                        ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@OdaOzellikMenuId, N'Oda Ozellikleri', 'fa-solid fa-sliders', 'oda-ozellikler', NULL, 8, @MainMenuId, 0, @Now, @Now);

                SELECT @OdaOzellikYonetimiView = [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = 'OdaOzellikYonetimi'
                  AND [Name] = 'View';

                IF @OdaOzellikYonetimiView IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @OdaOzellikMenuId AND [RoleId] = @OdaOzellikYonetimiView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES ('77777777-7777-7777-7777-777777777713', @OdaOzellikMenuId, @OdaOzellikYonetimiView, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                DECLARE @OdaOzellikMenuId uniqueidentifier = NULL;

                SELECT @OdaOzellikMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [ParentId] = @MainMenuId AND [Route] = 'oda-ozellikler';

                IF @OdaOzellikMenuId IS NOT NULL
                BEGIN
                    DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @OdaOzellikMenuId;
                    DELETE FROM [TODBase].[MenuItems] WHERE [Id] = @OdaOzellikMenuId;
                END
                """);
        }
    }
}
