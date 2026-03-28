using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260328223000_AddKonaklamaIcerikKullanimKurallari")]
public class AddKonaklamaIcerikKullanimKurallari : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "CheckInGunuGecerliMi",
            schema: "dbo",
            table: "RezervasyonKonaklamaHaklari",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "CheckOutGunuGecerliMi",
            schema: "dbo",
            table: "RezervasyonKonaklamaHaklari",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<TimeSpan>(
            name: "KullanimBaslangicSaati",
            schema: "dbo",
            table: "RezervasyonKonaklamaHaklari",
            type: "time",
            nullable: true);

        migrationBuilder.AddColumn<TimeSpan>(
            name: "KullanimBitisSaati",
            schema: "dbo",
            table: "RezervasyonKonaklamaHaklari",
            type: "time",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "KullanimNoktasi",
            schema: "dbo",
            table: "RezervasyonKonaklamaHaklari",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Genel");

        migrationBuilder.AddColumn<string>(
            name: "KullanimNoktasiAdiSnapshot",
            schema: "dbo",
            table: "RezervasyonKonaklamaHaklari",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "Genel");

        migrationBuilder.AddColumn<string>(
            name: "KullanimTipi",
            schema: "dbo",
            table: "RezervasyonKonaklamaHaklari",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Adetli");

        migrationBuilder.AddColumn<string>(
            name: "KullanimTipiAdiSnapshot",
            schema: "dbo",
            table: "RezervasyonKonaklamaHaklari",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "Adetli");

        migrationBuilder.AddColumn<bool>(
            name: "CheckInGunuGecerliMi",
            schema: "dbo",
            table: "KonaklamaTipiIcerikKalemleri",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "CheckOutGunuGecerliMi",
            schema: "dbo",
            table: "KonaklamaTipiIcerikKalemleri",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<TimeSpan>(
            name: "KullanimBaslangicSaati",
            schema: "dbo",
            table: "KonaklamaTipiIcerikKalemleri",
            type: "time",
            nullable: true);

        migrationBuilder.AddColumn<TimeSpan>(
            name: "KullanimBitisSaati",
            schema: "dbo",
            table: "KonaklamaTipiIcerikKalemleri",
            type: "time",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "KullanimNoktasi",
            schema: "dbo",
            table: "KonaklamaTipiIcerikKalemleri",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Genel");

        migrationBuilder.AddColumn<string>(
            name: "KullanimTipi",
            schema: "dbo",
            table: "KonaklamaTipiIcerikKalemleri",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Adetli");

        migrationBuilder.Sql(
            """
            UPDATE dbo.KonaklamaTipiIcerikKalemleri
            SET KullanimTipi = CASE
                    WHEN HizmetKodu IN ('Wifi', 'Otopark') THEN 'Sinirsiz'
                    ELSE 'Adetli'
                END,
                KullanimNoktasi = CASE
                    WHEN HizmetKodu IN ('Kahvalti', 'OgleYemegi', 'AksamYemegi') THEN 'Restoran'
                    WHEN HizmetKodu = 'GunlukTemizlik' THEN 'OdaServisi'
                    ELSE 'Genel'
                END,
                KullanimBaslangicSaati = CASE
                    WHEN HizmetKodu = 'Kahvalti' THEN CAST('07:00' AS time)
                    WHEN HizmetKodu = 'OgleYemegi' THEN CAST('12:00' AS time)
                    WHEN HizmetKodu = 'AksamYemegi' THEN CAST('19:00' AS time)
                    WHEN HizmetKodu IN ('Wifi', 'Otopark') THEN CAST('00:00' AS time)
                    WHEN HizmetKodu = 'GunlukTemizlik' THEN CAST('09:00' AS time)
                    ELSE NULL
                END,
                KullanimBitisSaati = CASE
                    WHEN HizmetKodu = 'Kahvalti' THEN CAST('10:00' AS time)
                    WHEN HizmetKodu = 'OgleYemegi' THEN CAST('14:00' AS time)
                    WHEN HizmetKodu = 'AksamYemegi' THEN CAST('21:00' AS time)
                    WHEN HizmetKodu IN ('Wifi', 'Otopark') THEN CAST('23:59' AS time)
                    WHEN HizmetKodu = 'GunlukTemizlik' THEN CAST('17:00' AS time)
                    ELSE NULL
                END,
                CheckInGunuGecerliMi = CASE
                    WHEN HizmetKodu = 'Kahvalti' THEN 0
                    ELSE 1
                END,
                CheckOutGunuGecerliMi = CASE
                    WHEN HizmetKodu IN ('OgleYemegi', 'AksamYemegi', 'GunlukTemizlik') THEN 0
                    ELSE 1
                END
            WHERE IsDeleted = 0;
            """);

        migrationBuilder.Sql(
            """
            UPDATE h
            SET h.KullanimTipi = c.KullanimTipi,
                h.KullanimTipiAdiSnapshot = CASE c.KullanimTipi WHEN 'Sinirsiz' THEN 'Sinirsiz' ELSE 'Adetli' END,
                h.KullanimNoktasi = c.KullanimNoktasi,
                h.KullanimNoktasiAdiSnapshot = CASE c.KullanimNoktasi
                    WHEN 'Restoran' THEN 'Restoran'
                    WHEN 'Bar' THEN 'Bar'
                    WHEN 'OdaServisi' THEN 'Oda Servisi'
                    ELSE 'Genel'
                END,
                h.KullanimBaslangicSaati = c.KullanimBaslangicSaati,
                h.KullanimBitisSaati = c.KullanimBitisSaati,
                h.CheckInGunuGecerliMi = c.CheckInGunuGecerliMi,
                h.CheckOutGunuGecerliMi = c.CheckOutGunuGecerliMi
            FROM dbo.RezervasyonKonaklamaHaklari h
            INNER JOIN dbo.Rezervasyonlar r ON r.Id = h.RezervasyonId
            INNER JOIN dbo.KonaklamaTipiIcerikKalemleri c
                ON c.KonaklamaTipiId = r.KonaklamaTipiId
               AND c.HizmetKodu = h.HizmetKodu
               AND c.IsDeleted = 0
            WHERE h.IsDeleted = 0;
            """);

        migrationBuilder.Sql(
            """
            UPDATE dbo.RezervasyonKonaklamaHaklari
            SET KullanimTipiAdiSnapshot = CASE KullanimTipi WHEN 'Sinirsiz' THEN 'Sinirsiz' ELSE 'Adetli' END,
                KullanimNoktasiAdiSnapshot = CASE KullanimNoktasi
                    WHEN 'Restoran' THEN 'Restoran'
                    WHEN 'Bar' THEN 'Bar'
                    WHEN 'OdaServisi' THEN 'Oda Servisi'
                    ELSE 'Genel'
                END
            WHERE IsDeleted = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CheckInGunuGecerliMi", schema: "dbo", table: "RezervasyonKonaklamaHaklari");
        migrationBuilder.DropColumn(name: "CheckOutGunuGecerliMi", schema: "dbo", table: "RezervasyonKonaklamaHaklari");
        migrationBuilder.DropColumn(name: "KullanimBaslangicSaati", schema: "dbo", table: "RezervasyonKonaklamaHaklari");
        migrationBuilder.DropColumn(name: "KullanimBitisSaati", schema: "dbo", table: "RezervasyonKonaklamaHaklari");
        migrationBuilder.DropColumn(name: "KullanimNoktasi", schema: "dbo", table: "RezervasyonKonaklamaHaklari");
        migrationBuilder.DropColumn(name: "KullanimNoktasiAdiSnapshot", schema: "dbo", table: "RezervasyonKonaklamaHaklari");
        migrationBuilder.DropColumn(name: "KullanimTipi", schema: "dbo", table: "RezervasyonKonaklamaHaklari");
        migrationBuilder.DropColumn(name: "KullanimTipiAdiSnapshot", schema: "dbo", table: "RezervasyonKonaklamaHaklari");

        migrationBuilder.DropColumn(name: "CheckInGunuGecerliMi", schema: "dbo", table: "KonaklamaTipiIcerikKalemleri");
        migrationBuilder.DropColumn(name: "CheckOutGunuGecerliMi", schema: "dbo", table: "KonaklamaTipiIcerikKalemleri");
        migrationBuilder.DropColumn(name: "KullanimBaslangicSaati", schema: "dbo", table: "KonaklamaTipiIcerikKalemleri");
        migrationBuilder.DropColumn(name: "KullanimBitisSaati", schema: "dbo", table: "KonaklamaTipiIcerikKalemleri");
        migrationBuilder.DropColumn(name: "KullanimNoktasi", schema: "dbo", table: "KonaklamaTipiIcerikKalemleri");
        migrationBuilder.DropColumn(name: "KullanimTipi", schema: "dbo", table: "KonaklamaTipiIcerikKalemleri");
    }
}
