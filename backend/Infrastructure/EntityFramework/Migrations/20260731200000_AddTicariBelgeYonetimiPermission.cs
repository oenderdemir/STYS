using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260731200000_AddTicariBelgeYonetimiPermission")]
public partial class AddTicariBelgeYonetimiPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();

            -- TicariBelgeYonetimi.View/Manage: operasyon modüllerinin (resepsiyon, rezervasyon,
            -- restoran, kamp vb.) ui/ticari-belgeler API sınırı için ayrı, atanabilir yetki
            -- tanımı - MuhasebeSatisBelgeleriYonetimi'nden BAĞIMSIZDIR. Bu turda hiçbir kullanıcı
            -- veya gruba OTOMATİK atanmaz (bkz. görev G) - yalnızca tanım eklenir.
            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'TicariBelgeYonetimi' AND [Name] = N'View')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), N'View', N'TicariBelgeYonetimi', 0, @Now, @Now);

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = N'TicariBelgeYonetimi' AND [Name] = N'Manage')
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), N'Manage', N'TicariBelgeYonetimi', 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @RoleIds TABLE ([RoleId] uniqueidentifier NOT NULL);
            INSERT INTO @RoleIds ([RoleId])
            SELECT [Id] FROM [TODBase].[Roles]
            WHERE [Domain] = N'TicariBelgeYonetimi' AND [Name] IN (N'View', N'Manage');

            DELETE mir
            FROM [TODBase].[MenuItemRoles] mir
            INNER JOIN @RoleIds rr ON rr.[RoleId] = mir.[RoleId];

            DELETE ugr
            FROM [TODBase].[UserGroupRoles] ugr
            INNER JOIN @RoleIds rr ON rr.[RoleId] = ugr.[RoleId];

            DELETE FROM [TODBase].[Roles]
            WHERE [Id] IN (SELECT [RoleId] FROM @RoleIds);
            """);
    }
}
