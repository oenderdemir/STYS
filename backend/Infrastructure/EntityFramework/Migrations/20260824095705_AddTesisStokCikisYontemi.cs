using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddTesisStokCikisYontemi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StokCikisYontemi",
                schema: "dbo",
                table: "Tesisler",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "TalepVeOnay");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tesis_StokCikisYontemi",
                schema: "dbo",
                table: "Tesisler",
                sql: "[StokCikisYontemi] IN (N'TalepVeOnay', N'DogrudanDepoCikisi')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Tesis_StokCikisYontemi",
                schema: "dbo",
                table: "Tesisler");

            migrationBuilder.DropColumn(
                name: "StokCikisYontemi",
                schema: "dbo",
                table: "Tesisler");
        }
    }
}
