using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomLevelCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kapasite",
                schema: "dbo",
                table: "Odalar",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE o
                SET o.Kapasite = ot.Kapasite
                FROM dbo.Odalar AS o
                INNER JOIN dbo.TesisOdaTipleri AS ot ON ot.Id = o.TesisOdaTipiId
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kapasite",
                schema: "dbo",
                table: "Odalar");
        }
    }
}
