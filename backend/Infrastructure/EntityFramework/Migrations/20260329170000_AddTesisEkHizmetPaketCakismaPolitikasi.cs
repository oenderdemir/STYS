using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260329170000_AddTesisEkHizmetPaketCakismaPolitikasi")]
public class AddTesisEkHizmetPaketCakismaPolitikasi : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EkHizmetPaketCakismaPolitikasi",
            schema: "dbo",
            table: "Tesisler",
            type: "nvarchar(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "OnayIste");

        migrationBuilder.Sql(
            """
            UPDATE [dbo].[Tesisler]
            SET [EkHizmetPaketCakismaPolitikasi] = N'OnayIste'
            WHERE [EkHizmetPaketCakismaPolitikasi] IS NULL
               OR LTRIM(RTRIM([EkHizmetPaketCakismaPolitikasi])) = N'';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EkHizmetPaketCakismaPolitikasi",
            schema: "dbo",
            table: "Tesisler");
    }
}
