using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260331183500_BackfillOdaFiyatKullanimSekli")]
public partial class BackfillOdaFiyatKullanimSekli : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE dbo.OdaFiyatlari
            SET KullanimSekli = 'KisiBasi'
            WHERE KullanimSekli IS NULL OR LTRIM(RTRIM(KullanimSekli)) = '';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE dbo.OdaFiyatlari
            SET KullanimSekli = ''
            WHERE KullanimSekli = 'KisiBasi';
            """);
    }
}
