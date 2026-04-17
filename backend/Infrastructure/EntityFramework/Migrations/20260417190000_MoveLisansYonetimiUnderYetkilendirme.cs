using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260417190000_MoveLisansYonetimiUnderYetkilendirme")]
    public partial class MoveLisansYonetimiUnderYetkilendirme : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @MenuItemId uniqueidentifier = 'b1a2c3d4-e5f6-7890-abcd-ef0123456704';
                DECLARE @AuthMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666603';

                UPDATE [TODBase].[MenuItems]
                SET [ParentId] = @AuthMenuId,
                    [MenuOrder] = 4,
                    [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Id] = @MenuItemId;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @MenuItemId uniqueidentifier = 'b1a2c3d4-e5f6-7890-abcd-ef0123456704';
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';

                UPDATE [TODBase].[MenuItems]
                SET [ParentId] = @MainMenuId,
                    [MenuOrder] = 99,
                    [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Id] = @MenuItemId;
                """);
        }
    }
}
