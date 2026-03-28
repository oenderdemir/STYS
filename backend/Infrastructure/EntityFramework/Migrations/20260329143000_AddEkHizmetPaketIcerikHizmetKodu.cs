using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260329143000_AddEkHizmetPaketIcerikHizmetKodu")]
public class AddEkHizmetPaketIcerikHizmetKodu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PaketIcerikHizmetKodu",
            schema: "dbo",
            table: "EkHizmetler",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE EH
            SET EH.[PaketIcerikHizmetKodu] =
                CASE
                    WHEN EH.[Ad] LIKE N'%Kahvalt%' OR EH.[Ad] LIKE N'%Breakfast%' THEN N'Kahvalti'
                    WHEN EH.[Ad] LIKE N'%Ogle%' OR EH.[Ad] LIKE N'%Öğle%' OR EH.[Ad] LIKE N'%Lunch%' THEN N'OgleYemegi'
                    WHEN EH.[Ad] LIKE N'%Aksam%' OR EH.[Ad] LIKE N'%Akşam%' OR EH.[Ad] LIKE N'%Dinner%' THEN N'AksamYemegi'
                    WHEN EH.[Ad] LIKE N'%Wi-Fi%' OR EH.[Ad] LIKE N'%Wifi%' OR EH.[Ad] LIKE N'%Internet%' THEN N'Wifi'
                    WHEN EH.[Ad] LIKE N'%Otopark%' OR EH.[Ad] LIKE N'%Vale%' OR EH.[Ad] LIKE N'%Parking%' THEN N'Otopark'
                    WHEN EH.[Ad] LIKE N'%Havaalani Transfer%' OR EH.[Ad] LIKE N'%Havalimani Transfer%' OR EH.[Ad] LIKE N'%Havalimanı Transfer%' OR EH.[Ad] LIKE N'%Airport Transfer%' THEN N'HavaalaniTransferi'
                    WHEN EH.[Ad] LIKE N'%Gunluk Temizlik%' OR EH.[Ad] LIKE N'%Günlük Temizlik%' OR EH.[Ad] LIKE N'%Housekeeping%' THEN N'GunlukTemizlik'
                    ELSE NULL
                END
            FROM [dbo].[EkHizmetler] EH
            WHERE EH.[IsDeleted] = 0
              AND EH.[PaketIcerikHizmetKodu] IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PaketIcerikHizmetKodu",
            schema: "dbo",
            table: "EkHizmetler");
    }
}
