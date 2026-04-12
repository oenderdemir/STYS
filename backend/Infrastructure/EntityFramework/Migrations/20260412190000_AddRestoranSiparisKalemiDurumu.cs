using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260412190000_AddRestoranSiparisKalemiDurumu")]
public partial class AddRestoranSiparisKalemiDurumu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Durum",
            schema: "restoran",
            table: "RestoranSiparisKalemleri",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Beklemede");

        migrationBuilder.Sql(
            """
            UPDATE [restoran].[RestoranSiparisKalemleri]
            SET [Durum] = N'Beklemede'
            WHERE [Durum] IS NULL OR LTRIM(RTRIM([Durum])) = N'';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Durum",
            schema: "restoran",
            table: "RestoranSiparisKalemleri");
    }
}
