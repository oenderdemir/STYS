using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260407220000_AddKampDonemiTesisAtamaMenuAndPermissions")]
public partial class AddKampDonemiTesisAtamaMenuAndPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @KampParentMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d101';
            DECLARE @KampDonemiTesisAtamaMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d1a1';

            IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampParentMenuId)
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KampDonemiTesisAtamaMenuId)
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@KampDonemiTesisAtamaMenuId, N'Tesis Atamalari', N'fa-solid fa-building', N'kamp-donemi-atamalari', NULL, @KampParentMenuId, 3, 0, @Now, @Now);
                ELSE
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Tesis Atamalari', [Icon] = N'fa-solid fa-building', [Route] = N'kamp-donemi-atamalari', [ParentId] = @KampParentMenuId, [MenuOrder] = 3, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                    WHERE [Id] = @KampDonemiTesisAtamaMenuId;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @KampDonemiTesisAtamaMenuId uniqueidentifier = '4b7f9b29-0bc0-4a95-b95f-3cd0a3b8d1a1';

            DELETE FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @KampDonemiTesisAtamaMenuId;
            DELETE FROM [TODBase].[MenuItems] WHERE [Id] = @KampDonemiTesisAtamaMenuId;
            """);
    }
}
