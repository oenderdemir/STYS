using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddOdaOverrideFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BalkonVarMiOverride",
                schema: "dbo",
                table: "Odalar",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EkOzellikler",
                schema: "dbo",
                table: "Odalar",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KlimaVarMiOverride",
                schema: "dbo",
                table: "Odalar",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MetrekareOverride",
                schema: "dbo",
                table: "Odalar",
                type: "decimal(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BalkonVarMiOverride",
                schema: "dbo",
                table: "Odalar");

            migrationBuilder.DropColumn(
                name: "EkOzellikler",
                schema: "dbo",
                table: "Odalar");

            migrationBuilder.DropColumn(
                name: "KlimaVarMiOverride",
                schema: "dbo",
                table: "Odalar");

            migrationBuilder.DropColumn(
                name: "MetrekareOverride",
                schema: "dbo",
                table: "Odalar");
        }
    }
}
