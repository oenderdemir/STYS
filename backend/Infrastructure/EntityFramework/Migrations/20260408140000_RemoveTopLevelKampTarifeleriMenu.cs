using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260408140000_RemoveTopLevelKampTarifeleriMenu")]
public partial class RemoveTopLevelKampTarifeleriMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @TopLevelMenuItemId uniqueidentifier = '44444444-4444-4444-4444-444444444901';

            -- Soft-delete the top-level "Kamp Tarifeler" menu item
            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 1, [DeletedAt] = @Now
            WHERE [Id] = @TopLevelMenuItemId;

            -- Also soft-delete its role associations
            UPDATE [TODBase].[MenuItemRoles]
            SET [IsDeleted] = 1, [UpdatedAt] = @Now
            WHERE [MenuItemId] = @TopLevelMenuItemId;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @TopLevelMenuItemId uniqueidentifier = '44444444-4444-4444-4444-444444444901';

            -- Restore the top-level "Kamp Tarifeler" menu item
            UPDATE [TODBase].[MenuItems]
            SET [IsDeleted] = 0, [DeletedAt] = NULL
            WHERE [Id] = @TopLevelMenuItemId;

            -- Restore its role associations
            UPDATE [TODBase].[MenuItemRoles]
            SET [IsDeleted] = 0
            WHERE [MenuItemId] = @TopLevelMenuItemId;
            """);
    }
}
